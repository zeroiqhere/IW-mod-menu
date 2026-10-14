using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using Apparition.Backend.Preferences;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Backend.Tests;

// Account ownership survives a permission change, while its canceled menu caller cannot save
internal static class ScopedPreferenceCancellationTests {
    public static void CanceledEditorUnlockCannotSaveUnderAnUnchangedAccountScope() => CheckCanceledSave(false);

    public static void ReadyRedrawCannotSaveAfterItsCallerLosesPermission() => CheckCanceledSave(true);

    private static void CheckCanceledSave(bool rebuild) {
            var session = new PreferenceSession();var clock = new ManualTimeProvider();
            var store = new MemoryCommittedMenuPreferencesStore();
            var controller = new BackendController(42, session, true, true, clock,
                openingActions: QuickOpeningTestSupport.AllCapabilities(), preferenceStore: store);
            ulong sequence = 1;
            void Send(OverlayInput input) {
                var frame = controller.Snapshot();
                // Opening gestures carry actor and policy proof; editor commands retain their visit
                bool opening = input == OverlayInput.OpenMain;
                ulong? policy = opening ? frame.OpeningPolicies?.Revision ?? frame.MainOpeningControls?.Revision : null;
                controller.Consume(new(sequence++, 1, 42, input, true, frame.NavigationRevision,
                    policy, opening ? session.Owned.Admission.Revision : null), () => true);
            }
            void Select(string label, bool settle = true) {
                var rows = controller.Snapshot().Rows;int row = rows.ToList().FindIndex(value => value.Label == label);
                TestAssert.True(row >= 0, "The scoped source row is missing: " + label);
                for (int step = 0; step < rows.Count && controller.Snapshot().SelectedIndex != row; step++) Send(OverlayInput.Down);
                TestAssert.Equal(row, controller.Snapshot().SelectedIndex);
                Send(OverlayInput.Select);
                if (settle) { clock.Advance(TimeSpan.FromMilliseconds(430));controller.Snapshot(); }
            }
            Send(OverlayInput.OpenMain);
            TestAssert.True(controller.Busy && !controller.Snapshot().IsOpen,
                "The test must admit a real scoped opening before navigating");
            controller.StartQueuedAction();
            TestAssert.True(SpinWait.SpinUntil(() => { controller.ObserveCompletion();return !controller.Busy; }, TimeSpan.FromSeconds(3)),
                "The actual scoped opening did not finish");
            MenuOpenTestHarness.Settle(controller, clock);Select("Menu Customization");
            if (rebuild) {
                Select("Menu Preferences");Select("Option Counter", settle: false);
                clock.Advance(TimeSpan.FromMilliseconds(50));controller.ReadMenuPublicationPhase();
            } else {
                Select("Width Editor");Send(OverlayInput.IncreaseWidth);Send(OverlayInput.FinishWidthEditor);
                clock.Advance(TimeSpan.FromMilliseconds(25));
            }
            var scope = session.Scope;
            session.Owned.Change(LocalPlayerLife.Alive, hasMenu: false);
            var canceled = controller.Snapshot();
            TestAssert.True(!canceled.IsOpen && ReferenceEquals(scope, session.ReadLocalMenuPreferenceScope()),
                "The cancellation must close its caller while preserving the authenticated account scope");
            TestAssert.True(store.TryRead(scope, out var retained) && retained!.Appearance == MenuAppearance.Default,
                "A canceled timer or ready-only redraw was treated as a completed source save");
            session.Owned.Change(LocalPlayerLife.Alive);clock.Advance(TimeSpan.FromMilliseconds(1000));controller.Snapshot();
            TestAssert.True(store.TryRead(scope, out retained) && retained!.Appearance == MenuAppearance.Default,
                "Recovered permission revived an already canceled pending save");
    }

    private sealed class PreferenceSession : IBackendSession {
        public ScopedOpeningFixture.OpeningSession Owned { get; } = new();
        public LocalMenuPreferenceScope Scope { get; }
        public PreferenceSession() => Scope = new(123, Owned.Admission.OwnerToken);
        public bool RequiresLocalMenuAdmission => true;
        public LocalMenuPreferenceScope ReadLocalMenuPreferenceScope() => Scope;
        public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() => Owned.ReadLocalMenuAdmission();
        public LocalPlayerObservation ReadLocalObservation() => Owned.ReadLocalObservation();
        public OverlayMenuState ReadMenuState() => Owned.ReadMenuState();
        public Task ExecuteAsync(MenuAction action) => Owned.ExecuteAsync(action);
        public Task<SessionShutdownResult> ShutdownAsync() => Owned.ShutdownAsync();
        public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong revision, CancellationToken token = default) =>
            Owned.ValidateLocalMenuOpeningAsync(revision, token);
    }
}
