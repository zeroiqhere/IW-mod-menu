using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// Whole-cohort reads are bracketed by the original local actor and current host proof
public sealed partial class CohortGodmodeSession {
    private sealed record Proof(PlayerCohortObservation Observation, PlayerCohortCapture Capture,
        LocalActorSnapshot Actor);

    private LocalActorSnapshot ReadActorProof(LocalAccountSessionBinding? expected, CancellationToken token,
        bool requireAlive = false) {
        token.ThrowIfCancellationRequested();
        if (Volatile.Read(ref closing) != 0 || expectedProcessId <= 0 || readActor is null || readCohort is null
            || !connectionCheck() || !localRegistered()) throw new IOException("The original cohort source is unavailable");
        LocalGodmodeView? local = readLocalView();
        if (local?.HasBoundTarget != true || local.Binding is null || local.Identity != local.Binding.Identity
            || !LocalObservationBindingShape.IsSupported(local.Binding))
            throw new IOException("The original local flag owner is not registered");
        if (expected is not null && expected != local.Binding)
            throw new IOException("The retained local account binding changed");
        LocalActorSnapshot actor = readActor(token);
        token.ThrowIfCancellationRequested();
        if (!connectionCheck() || !LocalObservationBindingShape.IsSupported(actor.Binding)
            || actor.Binding != local.Binding || expected is not null && actor.Binding != expected
            || actor.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)
            || !actor.IsSessionHost || requireAlive && actor.Life != LocalPlayerLife.Alive)
            throw new IOException("The original local actor or current host proof changed");
        return actor;
    }

    private Proof ReadProof(LocalAccountSessionBinding? expected, CohortGodmodeTarget? continued,
        CancellationToken token) {
        LocalActorSnapshot before = ReadActorProof(expected, token);
        PlayerCohortObservation observed = readCohort!();
        token.ThrowIfCancellationRequested();
        PlayerCohortCapture captured = PlayerCohortCapture.TryCreate(observed)
            ?? throw new IOException("The complete lobby membership is unknown");
        LocalActorSnapshot after = ReadActorProof(before.Binding, token);
        var local = after.Binding.Identity;
        if (captured.Session.ProcessId != expectedProcessId
            || captured.Session.ServerClients != local.ServerClients || captured.Session.Capacity != local.ServerCount
            || !captured.Identities.Contains(local))
            throw new IOException("The cohort does not contain the original authenticated local member");
        if (continued is not null && (continued.Binding != after.Binding || !continued.Capture.Matches(observed)))
            throw new IOException("The captured lobby membership changed");
        // Withdrawing a private menu target must not erase the original actor's observed time
        foreach (var slot in observed.Slots) {
            var player = slot.Player;
            if (player is not null && latestTimes.TryGetValue(player.Identity, out int latest)
                && player.CommandTime < latest) throw new IOException("The cohort command time rolled back");
        }
        // Each repeated guard advances its watermark after the full source bracket succeeds
        foreach (var slot in observed.Slots) {
            if (slot.Player is { } player) latestTimes[player.Identity] = player.CommandTime;
        }
        return new(observed, captured, after);
    }

    private bool AdmitRollbackEnable(LocalAccountSessionBinding expected) {
        try {
            // Cleanup may outlive caller cancellation, but new ON still requires live connection authority
            _ = ReadActorProof(expected, lifetime.Token, true);
            return true;
        } catch (Exception) { return false; }
    }
}
