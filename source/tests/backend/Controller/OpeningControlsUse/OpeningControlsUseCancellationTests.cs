using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static partial class OpeningControlsUseControllerTests {
    public static void BindingUseReceiptsAreWithdrawnByOwnerFocusAndPageChanges() {
        foreach (string change in new[] { "owner", "focus", "page" }) {
            var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
            var capture = rig.Capture(OverlayInput.Select);
            var started = capture.ImmediateFrame;
            var receipt = AssertReceipt(started, capture.WireCommand);

            if (change == "owner") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "focus") rig.Focus = false;
            if (change == "page") rig.Navigation.OpenPage(MenuPage.Preferences);

            // A changed owner scope cannot turn the old Select into a held-input repeat
            var withdrawn = rig.Frame;
            TestAssert.True(withdrawn.MenuUseContinuation is null,
                "Owner, focus, and page changes withdraw the original Use receipt");
            if (change == "focus") {
                rig.Focus = true;
                TestAssert.True(rig.Frame.MenuUseContinuation is null,
                    "Restored focus requires a new admitted Select");
            }
            TestAssert.Equal(0, rig.SessionActionCount);

            _ = rig.Wait(500);
            TestAssert.True(rig.Frame.MenuUseContinuation is null,
                "A withdrawn receipt cannot become ready at its former deadline");
        }
    }

    public static void OpeningControlsClockRollbackRestartsOnlyTheUnfinishedWaitStage() {
        foreach (bool rollbackBeforeRedraw in new[] { true, false }) {
            var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
            var started = rig.Capture(OverlayInput.Select).ImmediateFrame;
            TestAssert.True(started.MenuUseContinuation is not null, "The binding Select must publish its causal Use receipt");
            var receipt = started.MenuUseContinuation!;

            if (rollbackBeforeRedraw) {
                _ = rig.Wait(100);
                var beforeRollback = rig.Frame;
                TestAssert.Equal("pending", beforeRollback.MenuOptionCallback!.Phase);
                TestAssert.Equal("OFF", beforeRollback.Rows.Single(row => row.Label == "Use").Value);
                ((ManualTimeProvider)rig.Clock).RollBackTimestamp(TimeSpan.FromMilliseconds(50));
                var restarted = rig.Frame;
                TestAssert.Equal("pending", restarted.MenuOptionCallback!.Phase);
                TestAssert.Equal("OFF", restarted.Rows.Single(row => row.Label == "Use").Value);
                TestAssert.Equal(started.MenuRevision, restarted.MenuRevision);
                TestAssert.Equal(receipt, restarted.MenuUseContinuation);
                WaitToRedraw(rig, 179, receipt);
            } else {
                var refreshed = rig.Wait(180);
                TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
                TestAssert.Equal(started.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
                TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Use").Value);
                _ = rig.Wait(100);
                var beforeRollback = rig.Frame;
                ((ManualTimeProvider)rig.Clock).RollBackTimestamp(TimeSpan.FromMilliseconds(50));
                var restarted = rig.Frame;
                TestAssert.Equal("cooldown", restarted.MenuOptionCallback!.Phase);
                TestAssert.Equal(refreshed.MenuRevision, restarted.MenuRevision);
                TestAssert.Equal("ON", restarted.Rows.Single(row => row.Label == "Use").Value);
                TestAssert.Equal(receipt, restarted.MenuUseContinuation);
            }

            var beforeReady = rig.Wait(249);
            TestAssert.Equal("cooldown", beforeReady.MenuOptionCallback!.Phase);
            TestAssert.Equal(receipt, beforeReady.MenuUseContinuation);
            var ready = rig.Wait(1);
            TestAssert.True(ready.MenuOptionCallback is null, "Clock recovery keeps the remaining source wait finite");
            TestAssert.Equal("ON", ready.Rows.Single(row => row.Label == "Use").Value);
            TestAssert.Equal(receipt, ready.MenuUseContinuation);
            TestAssert.Equal(started.MenuRevision!.Value + 1, ready.MenuRevision!.Value);
        }
    }

    public static void ChangedMenuTargetPreflightRejectsAnExhaustedNavigationEpochBeforeApplyingIt() {
        var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Menu");
        SetNavigationRevision(rig.Navigation, ulong.MaxValue);
        var staged = rig.StageChoice(OverlayInput.NextChoice);
        TestAssert.Equal("Quick Menu", staged.WaitFrame!.Rows[0].Value);
        var beforeFailure = staged.WaitFrame;
        var command = rig.NativeCommand(OverlayInput.Select, beforeFailure);

        // Applying the staged target needs one new action-context epoch
        TestAssert.Throws<OverflowException>(() => rig.Controller.Consume(command, () => rig.Focus));
        var afterFailure = rig.Frame;
        TestAssert.Equal(beforeFailure.Rows[0].Value, afterFailure.Rows[0].Value);
        TestAssert.True(afterFailure.Notice.Contains("Editor applied: Apparition", StringComparison.Ordinal),
            "The applied target stays Main after a failed selector epoch preflight");
        TestAssert.True(beforeFailure.OpeningPolicies!.Main.Actions.SequenceEqual(afterFailure.OpeningPolicies!.Main.Actions),
            "An exhausted navigation epoch cannot partially change opening policy");
        TestAssert.True(afterFailure.MenuOptionCallback is null && afterFailure.MenuUseContinuation is null,
            "A failed selector preflight cannot publish a callback or receipt");
    }

    private static void WaitToRedraw(OpeningControlsUseFixture rig, int milliseconds, MenuUseContinuation receipt) {
        var near = rig.Wait(milliseconds);
        TestAssert.Equal("pending", near.MenuOptionCallback!.Phase);
        TestAssert.Equal("OFF", near.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal(receipt, near.MenuUseContinuation);
        var refreshed = rig.Wait(1);
        TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
        TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal(near.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);
    }

    private static void SetNavigationRevision(OverlayNavigation navigation, ulong revision) =>
        typeof(OverlayNavigation).GetProperty(nameof(OverlayNavigation.Revision))!
            .GetSetMethod(nonPublic: true)!.Invoke(navigation, new object[] { revision });
}
