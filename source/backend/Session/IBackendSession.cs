using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.SessionLifecycle;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// The controller can be tested without opening a process or loading a window
public interface IBackendSession {
    OverlayMenuState ReadMenuState();
    Task ExecuteAsync(MenuAction action);
    Task<SessionShutdownResult> ShutdownAsync();

    // This owner-loop hint starts no observation reads by itself
    void SetAllPlayersGodmodeObservationEnabled(bool enabled) { }

    // Legacy adapters cannot claim an owned roster selection
    PlayerMenuSelectionScope? ReadPlayerSelectionScope() => null;
    PlayerMenuTarget? ReadSelectedPlayerTarget() => null;

    // Production provenance cannot fall back to legacy input after a connected read fails
    bool RequiresLocalMenuAdmission => false;

    // Cached proof and fresh command validation are separate from legacy display observations
    LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() => null;
    // A cached original owner permits administrative Exit, never an actor or gameplay action
    object? ReadOwnedCleanupOwner() => null;
    // Stored local preferences never supply actor authority or native input capability
    LocalMenuPreferenceScope? ReadLocalMenuPreferenceScope() => null;
    Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong expectedRevision,
        CancellationToken cancellationToken = default) => Task.FromResult<LocalMenuAdmissionSnapshot?>(null);

    // Legacy sessions expose no local binding and never start an observation read
    LocalPlayerObservation ReadLocalObservation() => LocalPlayerObservation.BindingUnavailable;
    Task RefreshLocalObservationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
