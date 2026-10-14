using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class OpeningControlsUseControllerTests {
    public static void MainBindingBoolSelectKeepsOldRowsAndItsReceiptUntil430Milliseconds() {
        var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        var before = rig.Frame;
        var capture = rig.Capture(OverlayInput.Select);
        var pending = capture.ImmediateFrame;
        var receipt = AssertReceipt(pending, capture.WireCommand);

        TestAssert.True(capture.WireCommand.LocalAdmissionRevision is null && capture.WireCommand.MenuRevision is null,
            "Native Select carries only navigation and opening-policy captures");
        TestAssert.Equal(before.NavigationRevision, capture.WireCommand.NavigationRevision!.Value);
        TestAssert.Equal(before.OpeningPolicies!.Revision, capture.WireCommand.OpeningControlsRevision!.Value);
        TestAssert.Equal(before.OpeningPolicies!.Revision + 1, pending.OpeningPolicies!.Revision);
        TestAssert.Equal(OpeningAction.Use, pending.OpeningPolicies!.Main.Actions[0]);
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("ON", pending.Rows.Single(row => row.Label == "Aim").Value);
        // The applied policy changes at Select while the visible row waits for its source redraw
        var beforeRefresh = rig.Wait(179);
        TestAssert.Equal("OFF", beforeRefresh.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal(receipt, beforeRefresh.MenuUseContinuation);
        var refreshed = rig.Wait(1);
        TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("OFF", refreshed.Rows.Single(row => row.Label == "Aim").Value);
        TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        AssertCallback(refreshed, "boolean", "cooldown");
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);

        var beforeReady = rig.Wait(249);
        AssertCallback(beforeReady, "boolean", "cooldown");
        TestAssert.Equal(receipt, beforeReady.MenuUseContinuation);
        var ready = rig.Wait(1);
        TestAssert.True(ready.MenuOptionCallback is null, "The exact callback expires at430ms");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
        TestAssert.Equal(0, rig.SessionActionCount);

        // A fresh physical Select starts a new callback identity after readiness
        var secondCapture = rig.Capture(OverlayInput.Select);
        TestAssert.Equal(receipt.Sequence + 1, secondCapture.ImmediateFrame.MenuUseContinuation!.Sequence);
        TestAssert.Equal(receipt.Revision + 1, secondCapture.ImmediateFrame.MenuUseContinuation.Revision);
    }

    public static void RejectedDuplicateBindingKeepsItsBooleanWaitWithoutChangingThePolicy() {
        var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Aim");
        var before = rig.Frame;
        var capture = rig.Capture(OverlayInput.Select);
        var rejected = capture.ImmediateFrame;
        var receipt = AssertReceipt(rejected, capture.WireCommand);

        TestAssert.Equal(before.OpeningPolicies!.Revision, rejected.OpeningPolicies!.Revision);
        TestAssert.True(before.OpeningPolicies!.Main.Actions.SequenceEqual(rejected.OpeningPolicies!.Main.Actions),
            "A duplicate binding cannot partially change the applied tuple");
        TestAssert.True(rejected.Notice.Contains("already", StringComparison.OrdinalIgnoreCase),
            "The rejected duplicate keeps its source feedback");
        TestAssert.Equal("ON", rejected.Rows.Single(row => row.Label == "Aim").Value);
        var refreshed = rig.Wait(180);
        TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Aim").Value);
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);
        var ready = rig.Wait(250);
        TestAssert.True(ready.MenuOptionCallback is null, "A rejected source BOOL still completes its430ms wait");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
        TestAssert.Equal(0, rig.SessionActionCount);
    }

    public static void DisableQuickBoolSelectUsesTheSameDelayedRefreshAnd430MillisecondReceipt() {
        var rig = OpeningControlsUseFixture.Ready(MenuPage.Preferences, "Disable Quick Menu");
        var before = rig.Frame;
        var capture = rig.Capture(OverlayInput.Select);
        var pending = capture.ImmediateFrame;
        var receipt = AssertReceipt(pending, capture.WireCommand);

        TestAssert.True(!before.OpeningPolicies!.QuickDisabled && pending.OpeningPolicies!.QuickDisabled,
            "The actual Quick-disabled policy commits at Select");
        TestAssert.Equal(before.OpeningPolicies!.Revision + 1, pending.OpeningPolicies!.Revision);
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "Disable Quick Menu").Value);

        var held = rig.Wait(179);
        TestAssert.Equal("OFF", held.Rows.Single(row => row.Label == "Disable Quick Menu").Value);
        TestAssert.Equal(receipt, held.MenuUseContinuation);
        var refreshed = rig.Wait(1);
        TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Disable Quick Menu").Value);
        TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);
        var ready = rig.Wait(250);
        TestAssert.True(ready.MenuOptionCallback is null, "Disable Quick reaches its source deadline at430ms");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
        TestAssert.Equal(0, rig.SessionActionCount);
    }

    private static MenuUseContinuation AssertReceipt(StateDocument frame, CommandEnvelope command,
        string kind = "boolean", string phase = "pending") {
        TestAssert.True(frame.MenuUseContinuation is not null, "An admitted typed Select owns one causal Use receipt");
        var receipt = frame.MenuUseContinuation!;
        TestAssert.True(frame.MenuOptionCallback is not null, "The callback and receipt share one exact revision");
        TestAssert.Equal(command.Sequence, receipt.Sequence);
        TestAssert.Equal(frame.MenuOptionCallback!.Revision, receipt.Revision);
        TestAssert.Equal(kind, receipt.Kind);
        TestAssert.Equal(frame.LocalMenuEligibility!.Revision, receipt.AdmissionRevision);
        AssertCallback(frame, kind, phase);
        return receipt;
    }

    private static void AssertCallback(StateDocument frame, string kind, string phase) {
        TestAssert.True(frame.MenuOptionCallback is not null, "The typed Select owns its source wait");
        TestAssert.Equal(kind, frame.MenuOptionCallback!.Kind);
        TestAssert.Equal(phase, frame.MenuOptionCallback.Phase);
        TestAssert.True(frame.MenuPreferenceRefresh is null,
            "A typed Select cannot publish a competing preference-refresh timeline");
    }
}
