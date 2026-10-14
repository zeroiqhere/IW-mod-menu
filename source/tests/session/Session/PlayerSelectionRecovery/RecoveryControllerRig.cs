using Apparition.Backend.Controller;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Session.Tests;

internal sealed class RecoveryControllerRig : IAsyncDisposable {
    private const int ProcessId = 4242;
    private readonly LocalAdmissionFixture local = new();
    private ulong sequence;
    internal readonly PlayerStateReaderRetailTransport Transport = new();
    internal readonly ManualTimeProvider Clock = new();
    internal readonly RetailBackendSession Session;
    internal readonly RecoverySessionInterleaver ControllerSession;
    internal readonly BackendController Controller;

    internal RecoveryControllerRig() {
        // Menu admission uses the existing production local binding reader and its owned bytes
        if (!local.Transport.Open(ProcessId)) throw new InvalidOperationException("The owned local fixture did not open");
        Session = new(ProcessId, () => true, () => Transport, () => false, _ => local.Provider);
        ControllerSession = new(Session);
        if (!VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee], out var actions, out _))
            throw new InvalidOperationException("The fixture opening actions were rejected");
        Controller = new(ProcessId, ControllerSession, clock: Clock, openingActions: actions,
            preferenceStore: new MemoryCommittedMenuPreferencesStore());
    }

    internal async Task OpenAsync() {
        await Session.ExecuteAsync(new(MenuActionKind.Connect));
        await Session.RefreshLocalObservationAsync();
        var admission = Session.ReadLocalMenuAdmission()
            ?? throw new InvalidOperationException("The actual local actor reader did not admit a menu visit");
        var frame = Controller.Snapshot();
        Controller.Consume(new(++sequence, 1, ProcessId, OverlayInput.OpenMain, true,
            frame.NavigationRevision, frame.MainOpeningControls!.Revision, admission.Revision), () => true);
        Controller.StartQueuedAction();
        await Controller.DrainOperationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Controller.Snapshot();
        Clock.Advance(TimeSpan.FromMilliseconds(50));
        Controller.Snapshot();
        Clock.Advance(TimeSpan.FromSeconds(2));
        if (!Controller.Snapshot().IsOpen || Controller.Busy)
            throw new InvalidOperationException("The controller did not finish opening the owned menu");
    }

    internal async Task SelectLabelAsync(string label, Action? beforeDispatch = null) {
        var frame = Controller.Snapshot();
        int target = frame.Rows.Select((row, index) => (row, index))
            .Single(pair => pair.row.Label == label).index;
        for (int steps = 0; Controller.Snapshot().SelectedIndex != target && steps < frame.Rows.Count; steps++)
            Send(OverlayInput.Down);
        frame = Controller.Snapshot();
        var admission = Session.ReadLocalMenuAdmission();
        beforeDispatch?.Invoke();
        Controller.Consume(new(++sequence, 1, ProcessId, OverlayInput.Select, true,
            NavigationRevision: frame.NavigationRevision, LocalAdmissionRevision: admission?.Revision), () => true);
        if (Controller.Busy) {
            Controller.StartQueuedAction();
            await Controller.DrainOperationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        var completed = Controller.Snapshot();
        if (completed.MenuOptionCallback is { } callback) {
            Clock.Advance(TimeSpan.FromMilliseconds(callback.Kind == "boolean" ? 430 : 250));
            Controller.Snapshot();
        }
    }

    internal void Back() {
        var frame = Controller.Snapshot();
        var admission = Session.ReadLocalMenuAdmission()
            ?? throw new InvalidOperationException("A scoped Back command requires current local admission");
        Controller.Consume(new(++sequence, 1, ProcessId, OverlayInput.Back, true,
            NavigationRevision: frame.NavigationRevision, LocalAdmissionRevision: admission.Revision,
            MenuRevision: frame.MenuRevision), () => true);
        Controller.Snapshot();
        Clock.Advance(TimeSpan.FromMilliseconds(250));
        Controller.Snapshot();
    }

    private void Send(OverlayInput input) {
        var frame = Controller.Snapshot();
        Controller.Consume(new(++sequence, 1, ProcessId, input, true,
            NavigationRevision: frame.NavigationRevision), () => true);
    }

    public async ValueTask DisposeAsync() {
        await Session.ShutdownAsync();
        local.Transport.Dispose();
        await local.DisposeAsync();
    }
}
