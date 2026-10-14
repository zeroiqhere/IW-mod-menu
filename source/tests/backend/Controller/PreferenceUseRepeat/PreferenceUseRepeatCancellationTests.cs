using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static partial class PreferenceUseRepeatControllerTests {
    public static void TypedPreferenceUseReceiptsRetireOnOwnerFocusTopologyAndShutdownChanges() {
        foreach (string change in new[] { "owner", "focus", "topology", "shutdown" }) {
            var rig = Ready("Quick Exit");
            rig.Send(OverlayInput.Select);
            var started = rig.Controller.Snapshot();
            AssertTypedWait(started);

            if (change == "owner") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "focus") rig.Focus = false;
            if (change == "topology") rig.Navigation.OpenPage(MenuPage.Instructions);
            if (change == "shutdown") rig.Controller.RequestShutdown();

            // Scope loss withdraws readiness even when the original callback can still finish
            var withdrawn = rig.Controller.Snapshot();
            TestAssert.True(withdrawn.MenuUseContinuation is null, "A changed owner scope cannot keep a held-Use receipt");
            if (change is "owner" or "topology" or "shutdown") {
                TestAssert.True(withdrawn.MenuOptionCallback is null, "A callback cannot outlive its captured owner topology");
            }
            if (change == "focus") {
                TestAssert.Equal("boolean", withdrawn.MenuOptionCallback!.Kind);
                rig.Focus = true;
                TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                    "Focus restoration requires a new deliberate preference Select");
            }
            // Expiry must not resurrect a canceled held input
            rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
            TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                "A canceled preference invocation cannot become ready after its original deadline");
        }
    }

    public static void TypedPreferenceClockRollbackRestartsOnlyTheCurrentRefreshStage() {
        foreach (bool rollbackBeforeRefresh in new[] { true, false }) {
            var rig = Ready("Spotlight Cursor");
            var before = rig.Controller.Snapshot();
            string oldValue = RowValue(before, "Spotlight Cursor");
            rig.Send(OverlayInput.Select);
            var started = rig.Controller.Snapshot();
            var receipt = AssertTypedWait(started);

            // Before redraw, restart the pending stage without spending a navigation revision
            if (rollbackBeforeRefresh) {
                rig.Clock.Advance(TimeSpan.FromMilliseconds(100));
                rig.Controller.Snapshot();
                rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
                var restarted = rig.Controller.Snapshot();
                TestAssert.Equal("pending", restarted.MenuOptionCallback!.Phase);
                TestAssert.Equal(oldValue, RowValue(restarted, "Spotlight Cursor"));
                TestAssert.Equal(before.MenuRevision, restarted.MenuRevision);
                TestAssert.Equal(receipt, restarted.MenuUseContinuation);
                rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
                TestAssert.Equal(before.MenuRevision, rig.Controller.Snapshot().MenuRevision);
                rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
                var refreshed = rig.Controller.Snapshot();
                TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
                TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
                TestAssert.True(RowValue(refreshed, "Spotlight Cursor") != oldValue,
                    "A rollback before redraw keeps one finite180ms refresh");
            } else {
                // After redraw, only the cooldown restarts and the new style stays committed
                rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
                var refreshed = rig.Controller.Snapshot();
                TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
                TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
                TestAssert.True(RowValue(refreshed, "Spotlight Cursor") != oldValue,
                    "The first owned redraw commits the changed cursor preference");
                rig.Clock.Advance(TimeSpan.FromMilliseconds(100));
                rig.Controller.Snapshot();
                rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
                var restarted = rig.Controller.Snapshot();
                TestAssert.Equal("cooldown", restarted.MenuOptionCallback!.Phase);
                TestAssert.Equal(refreshed.MenuRevision, restarted.MenuRevision);
                TestAssert.True(RowValue(restarted, "Spotlight Cursor") != oldValue,
                    "A rollback after redraw cannot restore the retained old style");
                TestAssert.Equal(receipt, restarted.MenuUseContinuation);
            }

            // Both paths finish their remaining cooldown under the same receipt
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
            TestAssert.Equal("cooldown", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var ready = rig.Controller.Snapshot();
            TestAssert.True(ready.MenuOptionCallback is null, "The recovered callback expires after its remaining250ms stage");
            TestAssert.Equal(receipt, ready.MenuUseContinuation);
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }
}
