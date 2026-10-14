using Apparition.Backend.Session;
using Apparition.Backend.Preferences;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Session.Tests;

internal sealed class RecoverySessionInterleaver(IBackendSession inner)
    : IBackendSession {
    private Action? afterNextStateRead;

    internal void AfterNextStateRead(Action action) => Volatile.Write(ref afterNextStateRead,
        action ?? throw new ArgumentNullException(nameof(action)));

    public OverlayMenuState ReadMenuState() {
        // Return a real captured view while changing its backing identity before dispatch revalidation
        var captured = inner.ReadMenuState();
        Interlocked.Exchange(ref afterNextStateRead, null)?.Invoke();
        return captured;
    }

    public Task ExecuteAsync(MenuAction action) => inner.ExecuteAsync(action);
    public Task<SessionShutdownResult> ShutdownAsync() => inner.ShutdownAsync();
    public void SetAllPlayersGodmodeObservationEnabled(bool enabled) =>
        inner.SetAllPlayersGodmodeObservationEnabled(enabled);
    public PlayerMenuSelectionScope? ReadPlayerSelectionScope() => inner.ReadPlayerSelectionScope();
    public PlayerMenuTarget? ReadSelectedPlayerTarget() => inner.ReadSelectedPlayerTarget();
    public bool RequiresLocalMenuAdmission => inner.RequiresLocalMenuAdmission;
    public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() => inner.ReadLocalMenuAdmission();
    public LocalMenuPreferenceScope? ReadLocalMenuPreferenceScope() => inner.ReadLocalMenuPreferenceScope();
    public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong expectedRevision,
        CancellationToken cancellationToken = default) =>
        inner.ValidateLocalMenuOpeningAsync(expectedRevision, cancellationToken);
    public LocalPlayerObservation ReadLocalObservation() => inner.ReadLocalObservation();
    public Task RefreshLocalObservationAsync(CancellationToken cancellationToken = default) =>
        inner.RefreshLocalObservationAsync(cancellationToken);
}
