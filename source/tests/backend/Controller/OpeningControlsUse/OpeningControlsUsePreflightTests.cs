using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class OpeningControlsUseControllerTests {
    public static void FailedClockAndExhaustedCallbackEpochLeaveBindingAndUseReceiptUnchanged() {
        var clock = new LocalOptionCallbackControllerTests.SwitchableClock();
        var clockRig = OpeningControlsUseFixture.Ready(selectedLabel: "Use", clock: clock);
        var beforeClockFailure = clockRig.Frame;
        var clockCommand = clockRig.NativeCommand(OverlayInput.Select, beforeClockFailure);

        // The callback clock must be sampled before a policy tuple is committed
        clock.Fail = true;
        TestAssert.Throws<IOException>(() => clockRig.Controller.Consume(clockCommand, () => clockRig.Focus));
        clock.Fail = false;
        AssertBindingUnchanged(beforeClockFailure, clockRig.Frame);

        var callbackRig = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        var beforeCallbackFailure = callbackRig.Frame;
        var callbackRevision = typeof(Apparition.Backend.Controller.BackendController).GetField("optionCallbackRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        callbackRevision.SetValue(callbackRig.Controller, ulong.MaxValue);
        var callbackCommand = callbackRig.NativeCommand(OverlayInput.Select, beforeCallbackFailure);

        // A callback without a fresh revision cannot authorize this policy edit
        TestAssert.Throws<OverflowException>(() => callbackRig.Controller.Consume(callbackCommand, () => callbackRig.Focus));
        AssertBindingUnchanged(beforeCallbackFailure, callbackRig.Frame);
    }

    public static void ExhaustedMenuRevisionRejectsBindingBeforeMutationAndOneEpochAllowsItsRedraw() {
        var exhausted = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        SetMenuRevision(exhausted.Navigation, ulong.MaxValue);
        var beforeFailure = exhausted.Frame;
        var rejectedCommand = exhausted.NativeCommand(OverlayInput.Select, beforeFailure);

        // The only scheduled boolean redraw cannot be reserved at the final menu epoch
        TestAssert.Throws<OverflowException>(() => exhausted.Controller.Consume(rejectedCommand, () => exhausted.Focus));
        AssertBindingUnchanged(beforeFailure, exhausted.Frame);

        var available = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        SetMenuRevision(available.Navigation, ulong.MaxValue - 1);
        var before = available.Frame;
        var capture = available.Capture(OverlayInput.Select);
        var receipt = AssertReceipt(capture.ImmediateFrame, capture.WireCommand);
        TestAssert.Equal(ulong.MaxValue - 1, capture.ImmediateFrame.MenuRevision!.Value);

        // One reserved epoch is spent by the source redraw at180ms
        var held = available.Wait(179);
        TestAssert.Equal(ulong.MaxValue - 1, held.MenuRevision!.Value);
        TestAssert.Equal(receipt, held.MenuUseContinuation);
        var refreshed = available.Wait(1);
        TestAssert.Equal(ulong.MaxValue, refreshed.MenuRevision!.Value);
        TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);
        var ready = available.Wait(250);
        TestAssert.True(ready.MenuOptionCallback is null, "The callback reaches its430ms deadline without a second menu epoch");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
        TestAssert.Equal(before.OpeningPolicies!.Revision + 1, ready.OpeningPolicies!.Revision);
    }

    public static void ExhaustedPaintEpochDoesNotBlockOpeningControlsAndUnchangedMenuNeedsNoMenuEpoch() {
        var paint = OpeningControlsUseFixture.Ready(selectedLabel: "Use", booleanPaintRevision: ulong.MaxValue);
        var capture = paint.Capture(OverlayInput.Select);
        var receipt = AssertReceipt(capture.ImmediateFrame, capture.WireCommand);
        TestAssert.Equal(OpeningAction.Use, capture.ImmediateFrame.OpeningPolicies!.Main.Actions[0]);
        TestAssert.Equal(receipt, paint.Wait(430).MenuUseContinuation);

        var sameMenu = OpeningControlsUseFixture.Ready(selectedLabel: "Menu");
        SetMenuRevision(sameMenu.Navigation, ulong.MaxValue);
        var before = sameMenu.Frame;
        var sameTarget = sameMenu.Capture(OverlayInput.Select);
        var plainReceipt = AssertReceipt(sameTarget.ImmediateFrame, sameTarget.WireCommand, "plain", "cooldown");
        TestAssert.Equal(ulong.MaxValue, sameTarget.ImmediateFrame.MenuRevision!.Value);
        TestAssert.Equal(before.NavigationRevision, sameTarget.ImmediateFrame.NavigationRevision);
        TestAssert.Equal(plainReceipt, sameMenu.Wait(250).MenuUseContinuation);
    }

    public static void OneRemainingMenuEpochSupportsTheBindSlotImmediateRedraw() {
        var rig = OpeningControlsUseFixture.Ready(selectedLabel: "Bind Slot");
        SetMenuRevision(rig.Navigation, ulong.MaxValue - 1);
        var before = rig.Frame;
        var capture = rig.Capture(OverlayInput.Select);
        var receipt = AssertReceipt(capture.ImmediateFrame, capture.WireCommand, "plain", "cooldown");

        // Bind Slot redraws now, but does not spend a second context epoch for its wait
        TestAssert.Equal(ulong.MaxValue, capture.ImmediateFrame.MenuRevision!.Value);
        TestAssert.Equal(before.NavigationRevision, capture.ImmediateFrame.NavigationRevision);
        TestAssert.Equal(receipt, rig.Wait(249).MenuUseContinuation);
        var ready = rig.Wait(1);
        TestAssert.True(ready.MenuOptionCallback is null, "The no-op slot redraw keeps its250ms caller wait");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
    }

    public static void StaleNavigationOrOpeningPolicyCapturesCannotStartAnOpeningControlsWait() {
        var staleNavigation = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        var beforeNavigation = staleNavigation.Frame;
        var ignoredNavigation = staleNavigation.Capture(OverlayInput.Select,
            navigationRevision: beforeNavigation.NavigationRevision + 1).ImmediateFrame;
        AssertNoWaitOrMutation(beforeNavigation, ignoredNavigation);

        var stalePolicy = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        var beforePolicy = stalePolicy.Frame;
        var ignoredPolicy = stalePolicy.Capture(OverlayInput.Select,
            openingPolicyRevision: beforePolicy.OpeningPolicies!.Revision + 1).ImmediateFrame;
        AssertNoWaitOrMutation(beforePolicy, ignoredPolicy);
    }

    private static void AssertBindingUnchanged(StateDocument before, StateDocument after) {
        TestAssert.True(before.OpeningPolicies!.Main.Actions.SequenceEqual(after.OpeningPolicies!.Main.Actions),
            "A failed callback preflight cannot partially change the opening tuple");
        TestAssert.Equal(before.OpeningPolicies!.Revision, after.OpeningPolicies!.Revision);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
        TestAssert.True(after.MenuOptionCallback is null && after.MenuUseContinuation is null,
            "A failed callback preflight publishes neither wait nor Use receipt");
    }

    private static void AssertNoWaitOrMutation(StateDocument before, StateDocument after) {
        TestAssert.True(before.OpeningPolicies!.Main.Actions.SequenceEqual(after.OpeningPolicies!.Main.Actions),
            "A stale native capture cannot change the opening tuple");
        TestAssert.Equal(before.OpeningPolicies!.Revision, after.OpeningPolicies!.Revision);
        TestAssert.True(after.MenuOptionCallback is null && after.MenuUseContinuation is null,
            "A stale native capture cannot start a callback or Use receipt");
    }

    private static void SetMenuRevision(OverlayNavigation navigation, ulong revision) =>
        typeof(OverlayNavigation).GetProperty(nameof(OverlayNavigation.MenuRevision))!
            .GetSetMethod(nonPublic: true)!.Invoke(navigation, new object[] { revision });
}
