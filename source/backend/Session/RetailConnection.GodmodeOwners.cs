using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Apparition.Backend.Session;

internal sealed partial class RetailConnection {
    public PlayerGodmodeRegistry GodmodeOwners { get; }

    internal bool IsRetainedPersonalOwner(PlayerIdentity identity) {
        var view = LocalGodmode?.ReadView();
        return view?.HasBoundTarget == true && view.Identity == identity;
    }

    internal async Task<bool> CapturePersonalGodmodeAsync(CancellationToken cancellationToken = default) {
        var personal = LocalGodmode;
        if (personal is null) return false;
        var retained = personal.ReadView();
        if (retained.Binding is { } binding) {
            // A retained identity is known before admission; its strict proof belongs inside that turn
            return await GodmodeOwners.CapturePreparedAsync(new[] { binding.Identity },
                () => personal.CaptureAsync(cancellationToken), waitForAvailable: true,
                cancellationToken).ConfigureAwait(false);
        }
        // First capture must discover the account-bound identity before registry admission is possible
        if (!await personal.CaptureAsync(cancellationToken).ConfigureAwait(false)) return false;
        var identity = personal.ReadView().Identity;
        if (identity is null) return false;
        return (await GodmodeOwners.CaptureAsync(new[] { identity }, cancellationToken).ConfigureAwait(false)).Succeeded;
    }

    private bool ValidateGodmodeOwner(PlayerIdentity identity) {
        if (!IsRetainedPersonalOwner(identity)) return Teammates.MatchesRetainedIdentity(identity);
        // Disconnected cleanup eligibility is not current identity authority for a new operation
        return LocalGodmode!.Connected && LocalGodmode.Preflight(false).Succeeded;
    }

    private bool ValidateGodmodeOwnerForDisplay(PlayerIdentity identity) {
        if (!IsRetainedPersonalOwner(identity)) return Teammates.MatchesRetainedIdentity(identity);
        var view = LocalGodmode!.ReadView();
        // Local frame publication stays cached; actual commands always use the fresh validator
        return view.Identity == identity && view.Value is GodmodeValue.On or GodmodeValue.Off;
    }

    private IPlayerGodmodeFeature ResolveGodmodeOwner(PlayerIdentity identity) {
        var personal = LocalGodmode?.ReadView();
        if (personal?.Identity == identity) {
            if (personal.HasBoundTarget) return LocalGodmode!;
            // An authenticated self without a usable adapter cannot become a new peer writer
            throw new InvalidOperationException("The original personal Godmode adapter is unavailable");
        }
        object token;
        lock (teammateTokensLock) {
            if (!teammateTokens.TryGetValue(identity, out token!)) {
                token = new();
                teammateTokens.Add(identity, token);
            }
        }
        reservations.Acquire(token, identity);
        lock (teammateTokensLock) {
            if (teammateFeatures.TryGetValue(identity, out var retained)) return retained;
        }
        try {
            // Construction retains the same guarded adapter used by individual peer actions
            var retained = new FlagsPlayerGodmodeFeature(
                Teammates.GuardOwnedTransport(identity,
                    new OriginalConnectionFlagsTransport(Transport.BindTeammate(identity), AdmitNewWrite)));
            reservations.Retain(token);
            lock (teammateTokensLock) teammateFeatures.Add(identity, retained);
            return retained;
        } catch {
            reservations.ReleaseUnbound(token);
            throw;
        }
    }
}
