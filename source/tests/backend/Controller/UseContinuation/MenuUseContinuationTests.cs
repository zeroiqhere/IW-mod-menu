using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// The real controller owns callback admission, clocks and copied private actions
internal static class MenuUseContinuationTests {
    private static ScopedOpeningFixture Ready(MenuPage page, int index = 0) {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(page);
        LocalOptionCallbackControllerTests.SelectIndex(rig, index);
        return rig;
    }
    private static MenuUseContinuation Receipt(StateDocument frame) {
        TestAssert.True(frame.MenuUseContinuation is not null, "An admitted source callback must carry its original Use receipt");
        return frame.MenuUseContinuation!;
    }
    public static void PlainReselectionKeepsItsOriginalSequenceThroughNormalExpiry() {
        var rig = Ready(MenuPage.Preferences, 1); rig.Send(OverlayInput.Select);
        var first = rig.Controller.Snapshot(); var receipt = Receipt(first);
        TestAssert.Equal(first.LastSequence, receipt.Sequence); TestAssert.Equal(first.MenuOptionCallback!.Revision, receipt.Revision);
        TestAssert.Equal("plain", receipt.Kind);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); TestAssert.Equal(receipt, Receipt(rig.Controller.Snapshot()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null, "Only normal callback expiry retires the wait");
        TestAssert.Equal(receipt, Receipt(ready)); rig.Send(OverlayInput.Select);
        var second = rig.Controller.Snapshot(); TestAssert.Equal(receipt.Revision + 1, Receipt(second).Revision);
        TestAssert.Equal(receipt.Sequence + 1, Receipt(second).Sequence);
    }
    public static void BooleanAndImmediateInstructionRedrawKeepTheSamePrivateInvocation() {
        foreach (var page in new[] { MenuPage.ThemeColors, MenuPage.Instructions }) {
            var rig = Ready(page); var before = rig.Controller.Snapshot(); rig.Send(OverlayInput.Select);
            var started = rig.Controller.Snapshot(); var receipt = Receipt(started);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); var drawn = rig.Controller.Snapshot();
            TestAssert.Equal(receipt, Receipt(drawn)); TestAssert.True(drawn.MenuRevision > before.MenuRevision,
                "The owned redraw must not be mistaken for independent navigation");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); var retired = rig.Controller.Snapshot();
            TestAssert.True(retired.MenuOptionCallback is null, "The boolean wait completes at430ms");
            TestAssert.Equal(receipt, Receipt(retired)); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }
    public static void IgnoredAcknowledgementsCannotReplaceTheOriginalQueuedSelectSequence() {
        var rig = Ready(MenuPage.Personal); rig.Send(OverlayInput.Select); ulong accepted = rig.Controller.LastSequence;
        rig.Send(OverlayInput.Select); ulong ignored = rig.Controller.LastSequence;
        TestAssert.Equal(accepted + 1, ignored); rig.Controller.StartQueuedAction();
        var started = rig.Controller.Snapshot(); TestAssert.Equal(accepted, Receipt(started).Sequence);
        TestAssert.Equal(ignored, started.LastSequence); TestAssert.Equal(1, rig.Session.Executed.Count);
        rig.Send(OverlayInput.Select); var waiting = rig.Controller.Snapshot();
        TestAssert.Equal(accepted, Receipt(waiting).Sequence);
    }
    public static void IndependentChoiceNavigationAndRebuildRetireReadyContinuation() {
        var rig = Ready(MenuPage.Preferences, 1); rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); TestAssert.True(Receipt(rig.Controller.Snapshot()) is not null, "The original invocation is ready");
        rig.Send(OverlayInput.NextChoice); TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
            "A new choice invocation cannot inherit an older ready Select receipt");
        var moved = Ready(MenuPage.ThemeColors); moved.Send(OverlayInput.Select);
        moved.Clock.Advance(TimeSpan.FromMilliseconds(430)); moved.Controller.Snapshot(); moved.Send(OverlayInput.Down);
        TestAssert.True(moved.Controller.Snapshot().MenuUseContinuation is null, "Cursor changes retire the original row proof");
        var rebuilt = Ready(MenuPage.ThemeColors); rebuilt.Send(OverlayInput.Select);
        rebuilt.Clock.Advance(TimeSpan.FromMilliseconds(430)); rebuilt.Controller.Snapshot(); rebuilt.Navigation.OpenPage(MenuPage.Preferences);
        TestAssert.True(rebuilt.Controller.Snapshot().MenuUseContinuation is null, "An independent page cannot borrow a palette invocation");
    }
    public static void ActorLifeFocusAndShutdownWithdrawWithoutTurningCancellationIntoReady() {
        foreach (string change in new[] { "life", "owner", "focus", "shutdown" }) {
            var rig = Ready(MenuPage.ThemeColors); rig.Send(OverlayInput.Select); Receipt(rig.Controller.Snapshot());
            if (change == "life") rig.Session.Change(LocalPlayerLife.Unknown);
            if (change == "owner") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "focus") rig.Focus = false;
            if (change == "shutdown") rig.Controller.RequestShutdown();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(500)); var lost = rig.Controller.Snapshot();
            TestAssert.True(lost.MenuUseContinuation is null, "A canceled invocation cannot become ready after its nominal deadline");
            if (change == "focus") { rig.Focus = true; TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null, "Focus restoration requires a new admitted Select"); }
        }
    }
    public static void PrivatePersonalIdentityAndActionChangesRetireWithoutCursorMovement() {
        VerifySelectedTargetReplacement();
        var rig = Ready(MenuPage.Personal); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.ObserveCompletion();
        Receipt(rig.Controller.Snapshot()); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { PersonalGodmodeIdentity = ScopedOpeningFixture.Actor with { Guid = "FEDCBA0987654321" } };
        TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null, "The same personal label cannot conceal a changed full actor identity");
        var action = Ready(MenuPage.Personal); action.Send(OverlayInput.Select); action.Controller.StartQueuedAction(); action.Controller.ObserveCompletion();
        Receipt(action.Controller.Snapshot()); action.Clock.Advance(TimeSpan.FromMilliseconds(430)); action.Controller.Snapshot();
        action.Session.State = action.Session.State with { CanPersonalGodmode = false, CanRestorePersonalGodmode = true };
        TestAssert.True(action.Controller.Snapshot().MenuUseContinuation is null, "Changing Toggle to cleanup-only Restore changes the private action");
    }
    private static void VerifySelectedTargetReplacement() {
        var rig = Ready(MenuPage.Players, 1); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.ObserveCompletion();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Controller.Snapshot(); LocalOptionCallbackControllerTests.SelectIndex(rig, 1); rig.Send(OverlayInput.Select); rig.Controller.Snapshot();
        TestAssert.Equal(MenuPage.SelectedBasicScripts, rig.Navigation.Page);
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction(); rig.Controller.ObserveCompletion(); Receipt(rig.Controller.Snapshot());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
        var old = rig.Session.State.SelectedPlayerTarget!;
        rig.Session.State = rig.Session.State with { SelectedPlayerTarget = new(old.Identity with { Guid = "FEDCBA0987654321" },
            old.Route, old.OwnerToken, old.SelectionRevision + 1) };
        TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
            "Private target replacement must retire the same displayed Basic Scripts row");
        var cohort = new CohortGodmodeMenuFixture(); cohort.EnterAllPlayers(); cohort.SelectGodmode(); cohort.Controller.StartQueuedAction();
        Receipt(cohort.Controller.Snapshot()); cohort.Clock.Advance(TimeSpan.FromMilliseconds(430)); cohort.Controller.Snapshot();
        cohort.Session.State = cohort.Session.State with { AllPlayersGodmodeTarget = CohortGodmodeMenuFixture.Target() };
        TestAssert.True(cohort.Controller.Snapshot().MenuUseContinuation is null,
            "A replacement cohort cannot inherit the original invocation");
    }
    public static void FailedClockPreparationCommitsNeitherCallbackNorContinuation() {
        var clock = new LocalOptionCallbackControllerTests.SwitchableClock();
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors, clock: clock);
        var before = rig.Controller.Snapshot(); clock.Fail = true;
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Select)); clock.Fail = false;
        var after = rig.Controller.Snapshot(); TestAssert.True(after.MenuOptionCallback is null && after.MenuUseContinuation is null,
            "A failed callback preflight cannot publish an invocation receipt"); TestAssert.Equal(before.Accent, after.Accent);
        var exhausted = Ready(MenuPage.ThemeColors); var original = exhausted.Controller.Snapshot();
        var field = typeof(Apparition.Backend.Controller.BackendController).GetField("optionCallbackRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(exhausted.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => exhausted.Send(OverlayInput.Select));
        var unchanged = exhausted.Controller.Snapshot(); TestAssert.Equal(original.Accent, unchanged.Accent);
        TestAssert.True(unchanged.MenuOptionCallback is null && unchanged.MenuUseContinuation is null,
            "Exhausted callback identity cannot spend local mutations or continuation history");
    }
    public static void WireCopiesRejectWrongSequenceAuthorityAndCompetingPhases() {
        var rig = Ready(MenuPage.Preferences, 1); rig.Send(OverlayInput.Select); var frame = rig.Controller.Snapshot();
        // Literal copies reach wire validation even before the controller publishes receipts
        var valid = frame with { MenuUseContinuation = new(frame.MenuOptionCallback!.Revision, frame.LastSequence,
            frame.LocalMenuEligibility!.Revision, "plain") };
        TestAssert.True(valid.Serialize().Length > 0, "A current matching invocation is supported");
        // Independent presentation headers are omitted in this isolated unsigned-domain wire copy
        var largest = valid with { InstructionHud = null, InstructionTheme = null, ChromeTheme = null, LastSequence = ulong.MaxValue, MenuOptionCallback = valid.MenuOptionCallback! with { Revision = ulong.MaxValue },
            LocalMenuEligibility = valid.LocalMenuEligibility! with { Revision = ulong.MaxValue },
            MenuUseContinuation = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, "plain") };
        TestAssert.True(largest.Serialize().Length > 0, "The receipt preserves the full positive unsigned64 domain");
        foreach (var bad in new[] { valid.MenuUseContinuation! with { Revision = 0 }, valid.MenuUseContinuation! with { Sequence = frame.LastSequence + 1 },
            valid.MenuUseContinuation! with { AdmissionRevision = frame.LocalMenuEligibility.Revision + 1 }, valid.MenuUseContinuation! with { Kind = "boolean" }, valid.MenuUseContinuation! with { Sequence = 0 },
            valid.MenuUseContinuation! with { AdmissionRevision = 0 }, valid.MenuUseContinuation! with { Kind = "invalid" } }) {
            TestAssert.Throws<InvalidOperationException>(() => (valid with { MenuUseContinuation = bad }).Serialize());
        }
        TestAssert.Throws<InvalidOperationException>(() => (valid with { IsOpen = false }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { MenuOptionCallback = null, Rows = Array.Empty<StateRow>() }).Serialize());
        var spacerRows = valid.Rows.ToArray(); spacerRows[valid.SelectedIndex] = spacerRows[valid.SelectedIndex] with { IsSpacer = true, Enabled = false, Label = "", Value = "" };
        TestAssert.Throws<InvalidOperationException>(() => (valid with { MenuOptionCallback = null, Rows = spacerRows }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { MenuOptionCallback = null, LocalMenuEligibility = valid.LocalMenuEligibility! with { Life = "unknown" } }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { MenuMonitorWait = new(1, "choice") }).Serialize());
        TestAssert.True(StateFocusProjection.Apply(valid, false).MenuUseContinuation is null, "Hidden focus carries no continuation authority");
    }
}
