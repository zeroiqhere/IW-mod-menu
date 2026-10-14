using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Backend.Tests;

// Real navigation and copied cohort facts keep these checks independent of a native writer
internal sealed class CohortGodmodeMenuFixture {
    internal sealed class Boundary : IBackendSession {
        private readonly LocalMenuAdmissionSnapshot admission;
        public OverlayMenuState State { get; set; } = new SessionBoundary().State with {
            Connected = true, CanConnect = false, CanAllPlayersGodmode = true,
            AllPlayersGodmodeValue = "OFF", AllPlayersGodmodeStatus = "All observed players are OFF",
            AllPlayersGodmodeTarget = Target()
        };
        public Boundary() {
            var target = State.AllPlayersGodmodeTarget!;
            // The controlled menu actor and cohort share the same copied local identity and owner
            admission = new(10, new LocalPlayerObservation(42, target.Binding.Identity,
                LocalPlayerLife.Alive, true, null, "Verified", true), target.OwnerToken);
        }
        public List<MenuAction> Executed { get; } = new();
        public List<bool> ObservationFlags { get; } = new();
        public TaskCompletionSource? Pending { get; set; }
        public bool RequiresLocalMenuAdmission => true;
        public OverlayMenuState ReadMenuState() => State;
        public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() => admission;
        public LocalPlayerObservation ReadLocalObservation() => admission.Observation;
        public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong revision, CancellationToken token = default) =>
            Task.FromResult<LocalMenuAdmissionSnapshot?>(revision == admission.Revision ? admission : null);
        public void SetAllPlayersGodmodeObservationEnabled(bool enabled) => ObservationFlags.Add(enabled);
        public Task ExecuteAsync(MenuAction action) {
            Executed.Add(action);
            return Pending?.Task ?? Task.CompletedTask;
        }
        public Task<SessionShutdownResult> ShutdownAsync() => Task.FromResult(new SessionShutdownResult(true, true, null));
    }

    public Boundary Session { get; } = new();
    public ManualTimeProvider Clock { get; } = new();
    public OverlayNavigation Navigation { get; } = new();
    public BackendController Controller { get; }
    private ulong sequence;

    public CohortGodmodeMenuFixture() {
        Controller = new(42, Session, Navigation, false, clock: Clock,
            openingActions: QuickOpeningTestSupport.AllCapabilities());
    }

    internal static CohortGodmodeTarget Target() {
        var memory = new PlayerStateMemoryFixture();
        var capture = PlayerCohortCapture.TryCreate(PlayerStateReader.ReadCohort(
            PlayerStateMemoryFixture.ModuleBase, 42, memory.ReadBytes))!;
        var binding = new LocalAccountSessionBinding(0, 42, 0x500000, 0, 0x600000,
            capture.Identities[2].Slot, capture.Identities[2]);
        return new(new object(), capture, binding);
    }

    public void Send(OverlayInput input) => Controller.Consume(new(++sequence, 1, 42, input, true), () => true);
    public void OpenMain() {
        var frame = Controller.Snapshot();
        Controller.Consume(new(++sequence, 1, 42, OverlayInput.OpenMain, true, frame.NavigationRevision,
            frame.OpeningPolicies?.Revision ?? frame.MainOpeningControls!.Revision,
            Session.ReadLocalMenuAdmission()!.Revision), () => true);
        Controller.StartQueuedAction();
        TestAssert.True(SpinWait.SpinUntil(() => { Controller.ObserveCompletion(); return !Controller.Busy; },
            TimeSpan.FromSeconds(3)), "The actual opening validation worker must complete before advancing source time");
        TestAssert.True(Navigation.IsOpen, "The verified Main opening did not commit");
        Clock.Advance(TimeSpan.FromMilliseconds(50));
        Controller.Snapshot();
        Clock.Advance(TimeSpan.FromMilliseconds(700));
        Controller.Snapshot();
    }

    public void EnterAllPlayers() {
        if (!Navigation.IsOpen) OpenMain();
        // Select the real root page action instead of fabricating a child snapshot
        var frame = Controller.Snapshot();
        int index = frame.Rows.ToList().FindIndex(row => row.Label == "All Players Menu");
        for (int moved = 0; moved < 128 && Navigation.SelectedIndex != index; moved++) Send(OverlayInput.Down);
        TestAssert.Equal(index, Navigation.SelectedIndex);
        Send(OverlayInput.Select);
        Controller.Snapshot();
    }

    public void SelectGodmode() {
        for (int moved = 0; moved < 128 && Navigation.SelectedIndex != 5; moved++) Send(OverlayInput.Down);
        TestAssert.Equal(5, Navigation.SelectedIndex);
        Send(OverlayInput.Select);
    }
}
