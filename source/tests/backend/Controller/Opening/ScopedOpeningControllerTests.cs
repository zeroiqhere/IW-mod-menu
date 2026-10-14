using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// These cases use the real command queue, clocks, snapshots and family navigation
internal static class ScopedOpeningControllerTests {
    public static void ClosedSnapshotPublishesOwnedEligibilityAndExplicitFamilyWithoutReadingTheSource() {
        var rig = new ScopedOpeningFixture(); var frame = rig.Controller.Snapshot();
        TestAssert.Equal("main", frame.MenuSurface!);
        TestAssert.Equal(10UL, frame.LocalMenuEligibility!.Revision);
        TestAssert.Equal("alive", frame.LocalMenuEligibility.Life);
        TestAssert.True(frame.LocalMenuEligibility.HasMenu == true && frame.LocalMenuEligibility.ControlsDisabled == false
            && !frame.LocalMenuEligibility.Blocked, "The public descriptor must use actual menu-owner facts");
        TestAssert.Equal(0, rig.Session.Validations);
    }

    public static void QuickDrawsImmediatelyAndLocksInputsForExactlyFiveHundredFiftyMilliseconds() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
        var frame = rig.Controller.Snapshot();
        TestAssert.True(frame.IsOpen && frame.Rows.Count == 14, "Quick HUD and source declarations must appear at admission");
        TestAssert.Equal("quick", frame.MenuSurface!);
        TestAssert.Equal("cooldown", frame.MenuOpenTransition!.Phase);
        TestAssert.Equal(0, frame.MenuOpening!.ElapsedMilliseconds);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(549));
        var paused = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Down);
        TestAssert.Equal(0, rig.Controller.Snapshot().SelectedIndex);
        TestAssert.True(paused.LocalMenuEligibility!.ControlsDisabled == true, "Quick wait belongs to the source monitor lock");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        frame = rig.Controller.Snapshot();
        TestAssert.True(frame.MenuOpenTransition is null && frame.MenuOpening is not null,
            "Row opacity must outlast the independent input wait");
        rig.Send(OverlayInput.Down);
        TestAssert.Equal(1, rig.Controller.Snapshot().SelectedIndex);
        TestAssert.True(rig.Controller.Snapshot().MenuOpening is null, "An accepted scroll cancels only the remaining fade");
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    public static void QuickTargetsTheLocalPlayerWithoutReplacingTheMainTeammateSelection() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, rig.Session.Executed.Single().Kind);
        TestAssert.Equal(1, rig.Session.State.SelectedSlot);
        TestAssert.Equal("Friend", rig.Session.State.SelectedName!);
    }

    public static void DeadQuickKeepsItsFamilyAndJumpBackUsesOnlyTheCurrentLocalEpoch() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "Recognized death must not close the Quick family");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        var frame = rig.Controller.Snapshot();
        TestAssert.Equal(3, frame.Rows.Count);
        TestAssert.True(frame.MenuOpening is null, "A life-driven row replacement cannot reuse the old fade");
        rig.Queue(OverlayInput.JumpBack, local: 10);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "A stale Jump back cannot close the current menu");
        rig.Queue(OverlayInput.JumpBack);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "Fresh dead Jump follows the root back/close route");
        TestAssert.Equal("quick", rig.Controller.Snapshot().MenuSurface!);
    }

    public static void MainClosesOnRecognizedDeathOnlyWhenItsOrdinaryMonitorResumes() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        TestAssert.Equal("rebuild", rig.Controller.Snapshot().MenuOpenTransition!.Phase);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Session.Change(LocalPlayerLife.Dead);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(549));
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "Main death close cannot bypass its source input wait");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "The ordinary Main monitor must close its dead actor");
    }

    public static void PendingOpeningCannotCommitAfterOwnerScopeFocusOrNavigationChanges() {
        foreach (string change in new[] { "owner", "life", "focus", "navigation", "shutdown" }) {
            var rig = new ScopedOpeningFixture();
            var original = rig.Session.Admission;
            rig.Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Queue(OverlayInput.OpenQuick); rig.Controller.StartQueuedAction();
            TestAssert.True(SpinWait.SpinUntil(() => Volatile.Read(ref rig.Session.Validations) == 1, TimeSpan.FromSeconds(3)),
                "The validation worker did not start");
            switch (change) {
                case "owner": rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); break;
                case "life": rig.Session.Change(LocalPlayerLife.Dead); break;
                case "focus": rig.Focus = false; break;
                case "navigation": rig.Navigation.Open(); rig.Navigation.Close(); break;
                case "shutdown": rig.Controller.RequestShutdown(); break;
            }
            rig.Session.Pending.SetResult(original);
            // Exit remains a separate owned busy state; the promise is drained explicitly
            if (change == "shutdown") { rig.Controller.DrainOperationAsync().GetAwaiter().GetResult(); }
            else rig.Finish();
            TestAssert.True(!rig.Controller.Snapshot().IsOpen, "An old completion cannot mutate the replacement menu context");
        }
    }

    public static void InvalidScopesAndLivingJumpCannotBorrowTheLegacyBootstrapRoute() {
        var rig = new ScopedOpeningFixture();
        foreach (var (local, policy, nav) in new[] { (9UL, 1UL, 0UL), (10UL, 2UL, 0UL), (10UL, 1UL, 1UL) }) {
            rig.Queue(OverlayInput.OpenQuick, local, policy, nav); rig.Controller.StartQueuedAction();
        }
        rig.Send(OverlayInput.Toggle); rig.Controller.StartQueuedAction();
        TestAssert.True(!rig.Controller.Snapshot().IsOpen && rig.Session.Validations == 0,
            "A verified source cannot be opened by unscoped or stale input");
        rig.Open(OverlayInput.OpenQuick); rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Queue(OverlayInput.JumpBack);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "Living Jump cannot become Back");
    }
    public static void ConnectedProductionReadFailureCannotBorrowLegacyToggleButDisconnectedBootstrapCanOpen() {
        var rig = new ScopedOpeningFixture();
        rig.Session.RequiresLocalMenuAdmission = true;
        rig.Session.AdmissionUnavailable = true;
        rig.Send(OverlayInput.Toggle);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "Connected production cannot bypass missing owned proof");
        rig.Session.State = rig.Session.State with { Connected = false };
        rig.Send(OverlayInput.Toggle);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "Disconnected Connect bootstrap must remain reachable");
    }

    public static void QuickOwnerReplacementAndMenuGrantRevocationCloseWithoutBorrowingAnOldFamily() {
        foreach (bool owner in new[] { true, false }) {
            var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
            rig.Session.Change(LocalPlayerLife.Alive, hasMenu: owner ? true : false, replaceOwner: owner);
            var frame = rig.Controller.Snapshot();
            TestAssert.True(!frame.IsOpen && frame.MenuOpening is null && frame.MenuOpenTransition is null,
                "A revoked menu owner retained old presentation or controls");
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void DeadQuickAdmissionRequiresSavedJumpEvenWhenOnlyMainOpeningIsAvailable() {
        foreach (bool jump in new[] { false, true }) {
            var rig = new ScopedOpeningFixture(jump ? new[] { Apparition.Backend.OpenControls.OpeningAction.Aim,
                Apparition.Backend.OpenControls.OpeningAction.Melee, Apparition.Backend.OpenControls.OpeningAction.Jump }
                : new[] { Apparition.Backend.OpenControls.OpeningAction.Aim, Apparition.Backend.OpenControls.OpeningAction.Melee });
            rig.Session.Change(LocalPlayerLife.Dead);
            rig.Queue(OverlayInput.OpenQuick); rig.Controller.StartQueuedAction(); rig.Finish();
            TestAssert.Equal(jump, rig.Controller.Snapshot().IsOpen);
            TestAssert.Equal(jump ? 1 : 0, rig.Session.Validations);
        }
    }

    public static void ProductionConnectionRetiresItsUnboundBootstrapBeforeScopedMainCanOpen() {
        var rig = new ScopedOpeningFixture();
        rig.Session.RequiresLocalMenuAdmission = true;
        rig.Session.AdmissionUnavailable = true;
        rig.Session.State = rig.Session.State with { Connected = false };
        rig.Send(OverlayInput.Toggle);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "The disconnected bootstrap must be visible");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Session.State = rig.Session.State with { Connected = true };
        rig.Session.AdmissionUnavailable = false;
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "An unbound bootstrap cannot become an authenticated Main visit");
        rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "The subsequent scoped Main must retain its ordinary death monitor");
    }

    public static void DeadQuickWithoutASavedJumpCannotBorrowAJumpBackCommand() {
        var actions = Enum.GetValues<Apparition.Backend.OpenControls.OpeningAction>()
            .Where(action => action != Apparition.Backend.OpenControls.OpeningAction.Jump).ToArray();
        var rig = new ScopedOpeningFixture(actions);
        rig.Open(OverlayInput.OpenQuick);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Session.Change(LocalPlayerLife.Dead);
        rig.Queue(OverlayInput.JumpBack);
        TestAssert.True(rig.Controller.Snapshot().IsOpen, "Unverified dead jump cannot become melee Back");
        rig.Send(OverlayInput.Back);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "The verified ordinary melee route remains available");
    }

    public static void FailedCleanupFromQuickReturnsToAnImmediateMainExitWithoutGrantingGameActions() {
        var rig = new ScopedOpeningFixture();
        rig.Session.RequiresLocalMenuAdmission = true;
        rig.Open(OverlayInput.OpenQuick);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Controller.RequestShutdown();
        rig.Controller.RetryAfterShutdownFailure("Controlled cleanup failure");
        var frame = rig.Controller.Snapshot();
        TestAssert.True(frame.IsOpen && frame.IsRootMenu && frame.MenuSurface == "main",
            "Cleanup retry must expose the administrative Main root");
        TestAssert.True(frame.MenuOpening is null && frame.MenuOpenTransition is null,
            "Retry Exit cannot wait for opening animation");
        TestAssert.True(frame.LocalMenuEligibility is null && frame.Notice.Contains("Controlled cleanup failure", StringComparison.Ordinal),
            "Recovery cannot authorize another actor-scoped opening");
        rig.Send(OverlayInput.Back);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "Recovery local close remains available");
        rig.Send(OverlayInput.Toggle);
        TestAssert.True(rig.Controller.Snapshot().IsOpen && rig.Controller.Snapshot().LocalMenuEligibility is null,
            "A local recovery reopen cannot become an actor-scoped visit");
        rig.Send(OverlayInput.Down); rig.Send(OverlayInput.Select);
        TestAssert.True(rig.Controller.Snapshot().Rows.All(row => !row.Enabled), "Recovery cannot enable game features");
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(0, rig.Session.Executed.Count);
        rig.Send(OverlayInput.Back);
        var root = rig.Controller.Snapshot();
        for (int index = 0; index < root.Rows.Count && rig.Controller.Snapshot().Rows[rig.Controller.Snapshot().SelectedIndex].Label != "Exit"; index++)
            rig.Send(OverlayInput.Down);
        rig.Send(OverlayInput.Select);
        TestAssert.True(rig.Controller.ExitRequested, "Cleanup retry must remain available");
    }

    public static void AdmittedQuickAndVerifiedOpeningSettingsDoNotDisplayObsoleteUnavailableGuidance() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenQuick);
        var frame = rig.Controller.Snapshot();
        TestAssert.True(!frame.Notice.Contains("not enabled yet", StringComparison.Ordinal), "An admitted Quick visit must not deny its own opening path");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Send(OverlayInput.Back);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Navigation.OpenPage(MenuPage.OpenControls);
        frame = rig.Controller.Snapshot();
        TestAssert.True(!frame.Notice.Contains("Quick opening is unavailable", StringComparison.Ordinal), "Verified opening settings must disclose their actual configured controls");
        TestAssert.True(frame.Notice.Contains("while alive", StringComparison.Ordinal), "Opening help must state its life restriction");
    }

}
