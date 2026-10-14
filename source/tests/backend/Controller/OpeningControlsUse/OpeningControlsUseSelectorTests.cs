using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class OpeningControlsUseControllerTests {
    public static void SameMenuAndBindSlotSelectorsUsePlain250Receipts() {
        var menu = OpeningControlsUseFixture.Ready(selectedLabel: "Menu");
        var menuBefore = menu.Frame;
        var menuCapture = menu.Capture(OverlayInput.Select);
        var menuReceipt = AssertReceipt(menuCapture.ImmediateFrame, menuCapture.WireCommand, "plain", "cooldown");
        // Keeping the applied Menu target leaves its captured navigation scope intact
        TestAssert.Equal(menuBefore.NavigationRevision, menuCapture.ImmediateFrame.NavigationRevision);
        TestAssert.Equal(menuBefore.MenuRevision, menuCapture.ImmediateFrame.MenuRevision);
        var menuWait = menu.Wait(249);
        AssertCallback(menuWait, "plain", "cooldown");
        TestAssert.Equal(menuReceipt, menuWait.MenuUseContinuation);
        var menuReady = menu.Wait(1);
        TestAssert.True(menuReady.MenuOptionCallback is null, "The unchanged Menu selector ends at250ms");
        TestAssert.Equal(menuReceipt, menuReady.MenuUseContinuation);

        var slot = OpeningControlsUseFixture.Ready(selectedLabel: "Bind Slot");
        var slotBefore = slot.Frame;
        var slotCapture = slot.Capture(OverlayInput.Select);
        var slotReceipt = AssertReceipt(slotCapture.ImmediateFrame, slotCapture.WireCommand, "plain", "cooldown");
        // Bind Slot repaints immediately even when its applied value is unchanged
        TestAssert.Equal(slotBefore.NavigationRevision, slotCapture.ImmediateFrame.NavigationRevision);
        TestAssert.Equal(slotBefore.MenuRevision!.Value + 1, slotCapture.ImmediateFrame.MenuRevision!.Value);
        var slotWait = slot.Wait(249);
        TestAssert.Equal(slotReceipt, slotWait.MenuUseContinuation);
        var slotReady = slot.Wait(1);
        TestAssert.True(slotReady.MenuOptionCallback is null, "The unchanged Bind Slot redraw keeps its250ms caller wait");
        TestAssert.Equal(slotReceipt, slotReady.MenuUseContinuation);
        TestAssert.Equal(0, menu.SessionActionCount + slot.SessionActionCount);
    }

    public static void ChangedMenuTargetAndSlotKeepPlainWaitButCancelTheCapturedReceipt() {
        var menu = OpeningControlsUseFixture.Ready(selectedLabel: "Menu");
        var stagedMenu = menu.StageChoice(OverlayInput.NextChoice);
        TestAssert.Equal("Quick Menu", stagedMenu.WaitFrame!.Rows[0].Value);
        var menuBeforeSelect = stagedMenu.WaitFrame;
        var menuCapture = menu.Capture(OverlayInput.Select);
        // Applying the staged target changes navigation and retires this Select's captured scope
        TestAssert.Equal(menuBeforeSelect.NavigationRevision + 1, menuCapture.ImmediateFrame.NavigationRevision);
        TestAssert.True(menuCapture.ImmediateFrame.MenuUseContinuation is null,
            "A changed applied target invalidates the original navigation scope");
        AssertCallback(menuCapture.ImmediateFrame, "plain", "cooldown");
        TestAssert.True(menuCapture.ImmediateFrame.Notice.Contains("Editor applied: Quick Menu", StringComparison.Ordinal),
            "The new target applies even though its old held Use scope expires");
        AssertPlainWaitExpires(menu, menuCapture.ImmediateFrame);

        var slot = OpeningControlsUseFixture.Ready(selectedLabel: "Bind Slot");
        var stagedSlot = slot.StageChoice(OverlayInput.NextChoice);
        TestAssert.Equal("2", stagedSlot.WaitFrame!.Rows[1].Value);
        TestAssert.Equal(16, stagedSlot.WaitFrame.Rows.Count);
        var slotBeforeSelect = stagedSlot.WaitFrame;
        var slotCapture = slot.Capture(OverlayInput.Select);
        TestAssert.Equal(slotBeforeSelect.NavigationRevision + 1, slotCapture.ImmediateFrame.NavigationRevision);
        // Slot two adds None to the source inventory and cannot borrow the old row scope
        TestAssert.Equal(17, slotCapture.ImmediateFrame.Rows.Count);
        TestAssert.Equal("None", slotCapture.ImmediateFrame.Rows[3].Label);
        TestAssert.True(slotCapture.ImmediateFrame.MenuUseContinuation is null,
            "Adding the conditional None row cannot inherit the old Use scope");
        AssertCallback(slotCapture.ImmediateFrame, "plain", "cooldown");
        AssertPlainWaitExpires(slot, slotCapture.ImmediateFrame);
        TestAssert.Equal(0, menu.SessionActionCount + slot.SessionActionCount);
    }

    private static void AssertPlainWaitExpires(OpeningControlsUseFixture rig, StateDocument pending) {
        var receipt = pending.MenuUseContinuation;
        var near = rig.Wait(249);
        AssertCallback(near, "plain", "cooldown");
        TestAssert.Equal(receipt, near.MenuUseContinuation);
        var ready = rig.Wait(1);
        TestAssert.True(ready.MenuOptionCallback is null, "A changed selector still ends its plain wait at250ms");
        TestAssert.True(ready.MenuUseContinuation is null,
            "A changed navigation context cannot become ready through callback completion");
    }
}
