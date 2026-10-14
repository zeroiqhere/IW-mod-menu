using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The real typed controller repeats only the same admitted no-op slider invocation
internal static class UnchangedDesignUseTests {
    private static MenuUseContinuation Receipt(StateDocument frame) {
        TestAssert.True(frame.MenuUseContinuation is not null, "An unchanged Design selection needs its own held-Use receipt");
        return frame.MenuUseContinuation!;
    }

    public static void EveryUnchangedDesignUsesOnePlainWaitAndRepeatsWithoutRedrawOrGameplayWork() {
        foreach (string name in MenuDesignTests.Names) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
            // Only setup changes a design; the checked invocations reselect the already applied value
            for (int moves = 0; moves < MenuDesignTests.Names.Length && rig.Controller.Snapshot().Rows[0].Value != name; moves++)
                rig.StageChoice(OverlayInput.NextChoice);
            if (name != "Apparition") {
                rig.Send(OverlayInput.Select);
                rig.Clock.Advance(TimeSpan.FromMilliseconds(2000));
                rig.Controller.Snapshot();
            }
            var before = rig.Controller.Snapshot();
            TestAssert.Equal(name, before.Appearance!.Design);
            rig.Send(OverlayInput.Select);
            var pending = rig.Controller.Snapshot();
            var receipt = Receipt(pending);
            TestAssert.Equal("plain", receipt.Kind);
            TestAssert.Equal(pending.LastSequence, receipt.Sequence);
            TestAssert.Equal(pending.LocalMenuEligibility!.Revision, receipt.AdmissionRevision);
            TestAssert.Equal(receipt.Revision, pending.MenuOptionCallback!.Revision);
            TestAssert.Equal("cooldown", pending.MenuOptionCallback.Phase);
            TestAssert.True(pending.MenuDesignTransition is null, "A no-op must not retain a duplicate design timer");
            TestAssert.Equal(before.Appearance, pending.Appearance);
            TestAssert.Equal(before.MenuRevision, pending.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, pending.NavigationRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
            rig.Send(OverlayInput.Down);
            TestAssert.Equal(0, rig.Navigation.SelectedIndex);
            TestAssert.Equal(receipt, Receipt(rig.Controller.Snapshot()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var ready = rig.Controller.Snapshot();
            TestAssert.True(ready.MenuOptionCallback is null, "Normal expiry permits the next Use at 250 ms");
            TestAssert.Equal(receipt, Receipt(ready));
            rig.Send(OverlayInput.Select);
            var second = rig.Controller.Snapshot();
            TestAssert.Equal(receipt.Revision + 1, Receipt(second).Revision);
            TestAssert.Equal(second.LastSequence, Receipt(second).Sequence);
            TestAssert.Equal(before.Appearance, second.Appearance);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            var finished = rig.Controller.Snapshot();
            TestAssert.True(finished.MenuOptionCallback is null && finished.MenuDesignTransition is null,
                "The second no-op retires its single caller clock at 500 ms");
            TestAssert.Equal(before.MenuRevision, finished.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, finished.NavigationRevision);
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void NavigationActorFocusAndShutdownRetireTheNoOpReceiptInsteadOfMakingItReady() {
        foreach (string change in new[] { "navigation", "actor", "focus", "shutdown" }) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
            rig.Send(OverlayInput.Select);
            Receipt(rig.Controller.Snapshot());
            if (change == "navigation") rig.Navigation.OpenPage(MenuPage.Instructions);
            if (change == "actor") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "focus") rig.Focus = false;
            if (change == "shutdown") rig.Controller.RequestShutdown();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
            TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                "A foreign visit or cancelled no-op cannot supply a ready receipt");
            if (change == "focus") {
                rig.Focus = true;
                TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                    "Focus return cannot revive an earlier cancelled invocation");
            }
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void NoOpClockRollbackKeepsOneFiniteCallerWaitAndNeverRebuilds() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        var before = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select);
        var receipt = Receipt(rig.Controller.Snapshot());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        rig.Controller.Snapshot();
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal(receipt, Receipt(rig.Controller.Snapshot()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is not null, "Recovered plain wait still owns 249 ms");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null && ready.MenuDesignTransition is null,
            "Recovered no-op completes after its finite 250 ms caller wait");
        TestAssert.Equal(receipt, Receipt(ready));
        TestAssert.Equal(before.MenuRevision, ready.MenuRevision);
        TestAssert.Equal(before.Appearance, ready.Appearance);
    }

    public static void FailedNoOpClockPreparationCreatesNeitherTimerNorReceipt() {
        var clock = new LocalOptionCallbackControllerTests.SwitchableClock();
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, clock: clock);
        var before = rig.Controller.Snapshot();
        clock.Fail = true;
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Select));
        clock.Fail = false;
        var after = rig.Controller.Snapshot();
        TestAssert.True(after.MenuUseContinuation is null && after.MenuOptionCallback is null && after.MenuDesignTransition is null,
            "Clock rejection cannot leave a later no-op invocation running");
        TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
    }

    public static void ExhaustedCallbackEpochRejectsNoOpButItDoesNotRequireARedrawEpoch() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        var before = rig.Controller.Snapshot();
        var callback = typeof(BackendController).GetField("optionCallbackRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        callback.SetValue(rig.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        var rejected = rig.Controller.Snapshot();
        TestAssert.True(rejected.MenuUseContinuation is null && rejected.MenuOptionCallback is null && rejected.MenuDesignTransition is null,
            "Exhausted callback identity cannot arm a no-op clock or receipt");
        TestAssert.Equal(before.Appearance, rejected.Appearance);
        // The plain caller has no redraw and can still use a saturated presentation counter
        var saturated = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        typeof(OverlayNavigation).GetProperty(nameof(OverlayNavigation.MenuRevision))!
            .SetValue(saturated.Navigation, ulong.MaxValue);
        saturated.Send(OverlayInput.Select);
        var admitted = saturated.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, admitted.MenuRevision!.Value);
        TestAssert.Equal("plain", Receipt(admitted).Kind);
    }
}
