using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class PreferenceUseRepeatControllerTests {
    public static void FailedTypedPreferenceClockAdmissionLeavesFlagUnchanged() {
        var clock = new LocalOptionCallbackControllerTests.SwitchableClock();
        var clockRig = Ready("Large Cursor", clock: clock);
        var beforeClockFailure = clockRig.Controller.Snapshot();
        // Sampling failure must occur before the flag or callback is committed
        clock.Fail = true;
        TestAssert.Throws<IOException>(() => clockRig.Send(OverlayInput.Select));
        clock.Fail = false;
        var afterClockFailure = clockRig.Controller.Snapshot();
        TestAssert.Equal(beforeClockFailure.Appearance, afterClockFailure.Appearance);
        TestAssert.Equal(RowValue(beforeClockFailure, "Large Cursor"), RowValue(afterClockFailure, "Large Cursor"));
        TestAssert.True(afterClockFailure.MenuOptionCallback is null && afterClockFailure.MenuUseContinuation is null,
            "A failed clock sample commits neither callback identity nor Use receipt");
    }

    public static void ExhaustedUnrelatedPaintEpochStillAllowsTypedPreferenceCallback() {
        // Preferences owns one navigation redraw, with no BooleanPaintCache epoch
        var unrelatedPaintEpoch = Ready("Large Cursor", paintRevision: ulong.MaxValue);
        var beforePaintEpoch = unrelatedPaintEpoch.Controller.Snapshot();
        unrelatedPaintEpoch.Send(OverlayInput.Select);
        var afterPaintEpoch = unrelatedPaintEpoch.Controller.Snapshot();
        AssertTypedWait(afterPaintEpoch);
        TestAssert.Equal(RowValue(beforePaintEpoch, "Large Cursor"), RowValue(afterPaintEpoch, "Large Cursor"));
    }

    public static void ExhaustedNavigationRedrawEpochRejectsPreferenceBeforeMutation() {
        var exhaustedNavigation = Ready("Large Cursor");
        // Reserve the required redraw revision before changing any preference
        SetMenuRevision(exhaustedNavigation, ulong.MaxValue);
        var beforeNavigationFailure = exhaustedNavigation.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, beforeNavigationFailure.MenuRevision!.Value);
        TestAssert.Throws<OverflowException>(() => exhaustedNavigation.Send(OverlayInput.Select));
        var afterNavigationFailure = exhaustedNavigation.Controller.Snapshot();
        TestAssert.Equal(beforeNavigationFailure.Appearance, afterNavigationFailure.Appearance);
        TestAssert.Equal(RowValue(beforeNavigationFailure, "Large Cursor"), RowValue(afterNavigationFailure, "Large Cursor"));
        TestAssert.True(afterNavigationFailure.MenuOptionCallback is null && afterNavigationFailure.MenuUseContinuation is null,
            "A missing navigation redraw epoch rejects the preference before its state or receipt changes");
    }

    public static void ExhaustedOptionCallbackEpochRejectsPreferenceBeforeMutation() {
        var exhaustedCallback = Ready("Large Cursor");
        var beforeCallbackFailure = exhaustedCallback.Controller.Snapshot();
        var revision = typeof(Apparition.Backend.Controller.BackendController).GetField("optionCallbackRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        // A new invocation needs a distinct identity even when its policy edit is local
        revision.SetValue(exhaustedCallback.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => exhaustedCallback.Send(OverlayInput.Select));
        var afterCallbackFailure = exhaustedCallback.Controller.Snapshot();
        TestAssert.Equal(beforeCallbackFailure.Appearance, afterCallbackFailure.Appearance);
        TestAssert.Equal(RowValue(beforeCallbackFailure, "Large Cursor"), RowValue(afterCallbackFailure, "Large Cursor"));
        TestAssert.True(afterCallbackFailure.MenuOptionCallback is null && afterCallbackFailure.MenuUseContinuation is null,
            "An exhausted callback identity rejects the preference before its state or receipt changes");
    }

    public static void OneRemainingMenuRevisionSupportsTypedPreferenceRedrawAt180Milliseconds() {
        var rig = Ready("Large Cursor");
        SetMenuRevision(rig, ulong.MaxValue - 1);
        var before = rig.Controller.Snapshot();
        string oldValue = RowValue(before, "Large Cursor");
        TestAssert.Equal(ulong.MaxValue - 1, before.MenuRevision!.Value);

        rig.Send(OverlayInput.Select);
        var pending = rig.Controller.Snapshot();
        var receipt = AssertTypedWait(pending);
        TestAssert.Equal(ulong.MaxValue - 1, pending.MenuRevision!.Value);
        TestAssert.Equal(oldValue, RowValue(pending, "Large Cursor"));

        // Exactly one remaining revision must support the actual redraw
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var refreshed = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, refreshed.MenuRevision!.Value);
        TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
        TestAssert.True(RowValue(refreshed, "Large Cursor") != oldValue,
            "The one reserved navigation epoch is spent exactly at the source redraw");
        TestAssert.Equal(receipt, refreshed.MenuUseContinuation);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null, "The typed callback reaches ready without a second context epoch");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
    }

    public static void StealthWithOptionCounterUsesItsSeparateRebuildWithoutBorrowingAUseReceipt() {
        // Counter reconstruction keeps its separate timeline instead of borrowing a BOOL receipt
        var rig = Ready("Option Counter");
        var beforeCounter = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select);
        var counterRebuild = rig.Controller.Snapshot();
        TestAssert.Equal(beforeCounter.Appearance, counterRebuild.Appearance);
        TestAssert.True(counterRebuild.MenuPreferenceRebuild is not null, "Option Counter retains its creation rebuild owner");
        TestAssert.True(counterRebuild.MenuOptionCallback is null && counterRebuild.MenuUseContinuation is null,
            "A topology rebuild does not invent a source callback or held-Use receipt");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        var counterDrawn = rig.Controller.Snapshot();
        TestAssert.Equal(true, counterDrawn.Appearance!.OptionCounter);
        TestAssert.Equal("cooldown", counterDrawn.MenuPreferenceRebuild!.Phase);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        TestAssert.True(rig.Controller.Snapshot().MenuPreferenceRebuild is null,
            "The counter rebuild ends on its existing250ms owner wait");

        // Stealth with an active counter uses the same distinct reconstruction branch
        int stealthIndex = rig.Navigation.Rows.ToList().FindIndex(row => row.Label == "Stealth UI");
        TestAssert.True(stealthIndex >= 0, "Stealth UI row remains available after the counter rebuild");
        LocalOptionCallbackControllerTests.SelectIndex(rig, stealthIndex);
        var beforeStealth = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select);
        var stealthRebuild = rig.Controller.Snapshot();
        TestAssert.Equal(beforeStealth.Appearance, stealthRebuild.Appearance);
        TestAssert.True(stealthRebuild.MenuPreferenceRebuild is not null,
            "Stealth plus Option Counter retains its separate menu reconstruction");
        TestAssert.True(stealthRebuild.MenuOptionCallback is null && stealthRebuild.MenuUseContinuation is null,
            "The Stealth counter rebuild cannot borrow generic callback or held-Use authority");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        var rebuilt = rig.Controller.Snapshot();
        TestAssert.Equal("cooldown", rebuilt.MenuPreferenceRebuild!.Phase);
        TestAssert.Equal(true, rebuilt.Appearance!.StealthUI);
        TestAssert.Equal(false, rebuilt.Appearance.OptionCounter);
        TestAssert.Equal(true, RowValue(rebuilt, "Stealth UI") == "ON");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        TestAssert.True(rig.Controller.Snapshot().MenuPreferenceRebuild is null,
            "The independent rebuild wait completes at250ms without a callback receipt");
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    // Set only the counter boundary; the normal controller still performs the transition
    private static void SetMenuRevision(ScopedOpeningFixture rig, ulong revision) =>
        typeof(OverlayNavigation).GetProperty(nameof(OverlayNavigation.MenuRevision))!
            .GetSetMethod(nonPublic: true)!.Invoke(rig.Navigation, new object[] { revision });
}
