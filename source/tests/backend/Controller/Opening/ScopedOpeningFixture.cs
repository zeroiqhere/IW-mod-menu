using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// The controlled session supplies immutable proofs while the real controller owns every transition
internal sealed class ScopedOpeningFixture {
    internal static readonly PlayerIdentity Actor = new(2, 0x10000, 0x20000, 0x30000,
        4, "Local player", "1234567890ABCDEF") { ServerClient = 0x40000 };
    public ManualTimeProvider Clock { get; } = new();
    public OpeningSession Session { get; }
    public OverlayNavigation Navigation { get; }
    public BackendController Controller { get; }
    private ulong sequence;
    public bool Focus { get; set; } = true;

    public ScopedOpeningFixture(IReadOnlyList<Apparition.Backend.OpenControls.OpeningAction>? actions = null,
        OverlayNavigation? navigation = null, TimeProvider? clock = null, ulong booleanPaintRevision = 0,
        bool choiceControlsAvailable = false, LocalMenuPreferenceScope? preferenceScope = null) {
        Session = new(preferenceScope);
        Navigation = navigation ?? new();
        var verified = QuickOpeningTestSupport.AllCapabilities();
        if (actions is not null) {
            TestAssert.True(Apparition.Backend.OpenControls.VerifiedOpeningActions.TryCreate(actions, out var custom, out _), "Fixture actions must be valid");
            verified = custom!;
        }
        Controller = new(42, Session, Navigation, false, choiceControlsAvailable: choiceControlsAvailable, clock: clock ?? Clock,
            openingActions: verified, booleanPaintRevision: booleanPaintRevision);
    }

    public void Send(OverlayInput input) => Controller.Consume(new(++sequence, 1, 42, input, true), () => Focus);
    public void StageChoice(OverlayInput input) {
        Send(input);
        Clock.Advance(TimeSpan.FromMilliseconds(180));
        Controller.Snapshot();
    }
    public void Queue(OverlayInput input, ulong? local = null, ulong? policy = null, ulong? navigation = null) {
        var frame = Controller.Snapshot();
        Controller.Consume(new(++sequence, 1, 42, input, true, navigation ?? frame.NavigationRevision,
            input == OverlayInput.JumpBack ? null : policy ?? frame.OpeningPolicies?.Revision ?? frame.MainOpeningControls!.Revision,
            local ?? Session.Admission.Revision), () => Focus);
    }
    public void Open(OverlayInput input) {
        Queue(input);
        TestAssert.True(Controller.Busy && !Controller.Snapshot().IsOpen,
            "A scoped opening must publish pending state before reading its session proof");
        Controller.StartQueuedAction();
        Finish();
    }
    public void Finish() {
        TestAssert.True(SpinWait.SpinUntil(() => { Controller.ObserveCompletion(); return !Controller.Busy; }, TimeSpan.FromSeconds(3)),
            "The controlled opening did not complete");
    }

    internal sealed class OpeningSession : IBackendSession {
        private readonly LocalMenuPreferenceScope? preferenceScope;

        public OpeningSession(LocalMenuPreferenceScope? preferenceScope = null) => this.preferenceScope = preferenceScope;
        private object owner = new();
        private ulong revision = 10;
        private LocalPlayerObservation observation = new(42, Actor, LocalPlayerLife.Alive, true, null, "Verified", true);
        public LocalMenuAdmissionSnapshot Admission => new(revision, observation, owner);
        public TaskCompletionSource<LocalMenuAdmissionSnapshot?>? Pending { get; set; }
        public int Validations;
        public List<MenuAction> Executed { get; } = new();
        public OverlayMenuState State { get; set; } = new(false, true, false, false, true, "OFF", "Off",
            true, "OFF", "Off", true, true, new[] { new OverlayPlayer(1, "Friend") },
            1, "Friend", true, "ON", "Teammate On", false, "") {
            PersonalGodmodeIdentity = Actor, PersonalGodmodeRequiresVerifiedTarget = true
        };
        private ulong selectionEpoch;
        public PlayerMenuSelectionScope? ReadPlayerSelectionScope() => new(owner, selectionEpoch);
        public PlayerMenuTarget? ReadSelectedPlayerTarget() => State.SelectedPlayerTarget is { } target
            && ReferenceEquals(target.OwnerToken, owner) ? target : null;
        public OverlayMenuState ReadMenuState() => State with { SelectedPlayerTarget = ReadSelectedPlayerTarget() };
        public LocalPlayerObservation ReadLocalObservation() => observation;
        public bool RequiresLocalMenuAdmission { get; set; }
        public bool AdmissionUnavailable { get; set; }
        public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() => AdmissionUnavailable ? null : Admission;
        public LocalMenuPreferenceScope? ReadLocalMenuPreferenceScope() => preferenceScope;
        public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong expectedRevision, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref Validations);
            return Pending?.Task ?? Task.FromResult<LocalMenuAdmissionSnapshot?>(expectedRevision == revision ? Admission : null);
        }
        // Controlled completion changes task timing without a process or native writer
        public TaskCompletionSource<bool>? PendingAction { get; set; }
        public Task ExecuteAsync(MenuAction action) {
            Executed.Add(action);
            if (action.Kind == MenuActionKind.SelectPlayer && action.SelectionScope is { } scope
                && scope.Equals(ReadPlayerSelectionScope()) && selectionEpoch < ulong.MaxValue) {
                bool personal = Actor.Slot == action.Slot && Actor.Name == action.ExpectedName;
                var identity = personal ? Actor : new PlayerIdentity(action.Slot, 0x11000, 0x22000, 0x33000,
                    4, action.ExpectedName!, "ABCDEF0123456789") { ServerClient = 0x44000 };
                State = State with { SelectedSlot = identity.Slot, SelectedName = identity.Name,
                    SelectedPlayerTarget = new(identity, personal ? PlayerMenuRoute.Personal : PlayerMenuRoute.Teammate,
                        owner, ++selectionEpoch) };
            }
            return PendingAction?.Task ?? Task.CompletedTask;
        }
        public Task<SessionShutdownResult> ShutdownAsync() => Task.FromResult(new SessionShutdownResult(true, true, null));
        public void Change(LocalPlayerLife life, bool? hasMenu = true, bool replaceOwner = false) {
            // A life epoch changes without transferring the original account and connection owner
            revision++;
            if (replaceOwner) owner = new();
            observation = observation with { Life = life, HasMenu = hasMenu };
        }
    }
}
