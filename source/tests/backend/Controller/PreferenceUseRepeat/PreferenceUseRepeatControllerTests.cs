using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class PreferenceUseRepeatControllerTests {
    private static readonly (string Label, MenuPreference Preference)[] TypedFlags = {
        ("Quick Exit", MenuPreference.QuickExit),
        ("Spotlight Cursor", MenuPreference.SpotlightCursor),
        ("Colored Cursor", MenuPreference.ColoredCursor),
        ("Large Cursor", MenuPreference.LargeCursor)
    };

    public static void FourTypedCursorPreferencesKeepTheirRowsUntilTheBooleanRefreshAndReceiptUntil430Milliseconds() {
        foreach (var flag in TypedFlags) {
            var rig = Ready(flag.Label);
            var before = rig.Controller.Snapshot();
            string oldValue = RowValue(before, flag.Label);
            var oldAppearance = before.Appearance!;

            // Commit the policy now, but retain the prior checkbox and cursor style until redraw
            rig.Send(OverlayInput.Select);
            var pending = rig.Controller.Snapshot();
            var receipt = AssertTypedWait(pending);
            TestAssert.Equal(oldValue, RowValue(pending, flag.Label));
            TestAssert.Equal(before.MenuRevision, pending.MenuRevision);
            TestAssert.Equal(oldAppearance with { QuickExit = pending.Appearance!.QuickExit }, pending.Appearance);
            if (flag.Preference == MenuPreference.QuickExit) {
                TestAssert.Equal(true, pending.Appearance.QuickExit);
            } else {
                TestAssert.Equal(oldAppearance, pending.Appearance);
            }

            // The last pending millisecond still belongs to the original invocation
            rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
            var beforeRefresh = rig.Controller.Snapshot();
            TestAssert.Equal("pending", beforeRefresh.MenuOptionCallback!.Phase);
            TestAssert.Equal(oldValue, RowValue(beforeRefresh, flag.Label));
            TestAssert.Equal(receipt, beforeRefresh.MenuUseContinuation);

            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var refreshed = rig.Controller.Snapshot();
            TestAssert.Equal("cooldown", refreshed.MenuOptionCallback!.Phase);
            TestAssert.Equal(before.MenuRevision!.Value + 1, refreshed.MenuRevision!.Value);
            TestAssert.True(RowValue(refreshed, flag.Label) != oldValue, "The preference row changes at its owned180ms redraw");
            TestAssert.Equal(receipt, refreshed.MenuUseContinuation);

            rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
            TestAssert.Equal("cooldown", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var ready = rig.Controller.Snapshot();
            TestAssert.True(ready.MenuOptionCallback is null, "The local boolean wait ends at430ms");
            TestAssert.Equal(receipt, ready.MenuUseContinuation);
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void QuickExitAndStealthReselectionUseTheirOwnReadySelectReceipt() {
        var quickExit = Ready("Quick Exit");
        TestAssert.Equal(false, quickExit.Controller.Snapshot().Appearance!.QuickExit);
        quickExit.Send(OverlayInput.Select);
        var quickOn = quickExit.Controller.Snapshot();
        var firstQuickReceipt = AssertTypedWait(quickOn);
        TestAssert.Equal(true, quickOn.Appearance!.QuickExit);
        TestAssert.Equal("OFF", RowValue(quickOn, "Quick Exit"));
        quickExit.Clock.Advance(TimeSpan.FromMilliseconds(180));
        TestAssert.Equal("ON", RowValue(quickExit.Controller.Snapshot(), "Quick Exit"));
        quickExit.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var quickReady = quickExit.Controller.Snapshot();
        TestAssert.True(quickReady.MenuOptionCallback is null, "Quick Exit receives a ready source callback at430ms");
        TestAssert.Equal(firstQuickReceipt, quickReady.MenuUseContinuation);
        // A second invocation retains ON paint while its committed exit policy is already OFF
        quickExit.Send(OverlayInput.Select);
        var quickOff = quickExit.Controller.Snapshot();
        TestAssert.Equal(false, quickOff.Appearance!.QuickExit);
        TestAssert.Equal(firstQuickReceipt.Sequence + 1, quickOff.MenuUseContinuation!.Sequence);
        TestAssert.Equal(firstQuickReceipt.Revision + 1, quickOff.MenuUseContinuation.Revision);
        TestAssert.Equal("ON", RowValue(quickOff, "Quick Exit"));
        TestAssert.Equal("pending", quickOff.MenuOptionCallback!.Phase);
        quickExit.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var quickOffRefreshed = quickExit.Controller.Snapshot();
        TestAssert.Equal(false, quickOffRefreshed.Appearance!.QuickExit);
        TestAssert.Equal("OFF", RowValue(quickOffRefreshed, "Quick Exit"));
        TestAssert.Equal("cooldown", quickOffRefreshed.MenuOptionCallback!.Phase);
        quickExit.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var quickOffReady = quickExit.Controller.Snapshot();
        TestAssert.True(quickOffReady.MenuOptionCallback is null, "The second Quick Exit callback becomes ready at860ms");
        TestAssert.Equal(quickOff.MenuUseContinuation, quickOffReady.MenuUseContinuation);

        // Stealth redraw owns the checkbox and its dependent Boolean display style together
        var stealth = Ready("Stealth UI");
        TestAssert.Equal(false, stealth.Controller.Snapshot().Appearance!.StealthUI);
        stealth.Send(OverlayInput.Select);
        var stealthOn = stealth.Controller.Snapshot();
        var firstStealthReceipt = AssertTypedWait(stealthOn);
        TestAssert.Equal(false, stealthOn.Appearance!.StealthUI);
        TestAssert.Equal("Boxes", stealthOn.Appearance.BooleanDisplay);
        TestAssert.Equal("OFF", RowValue(stealthOn, "Stealth UI"));
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var stealthRefreshed = stealth.Controller.Snapshot();
        TestAssert.Equal(true, stealthRefreshed.Appearance!.StealthUI);
        TestAssert.Equal("Text", stealthRefreshed.Appearance.BooleanDisplay);
        TestAssert.Equal("ON", RowValue(stealthRefreshed, "Stealth UI"));
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(250));
        TestAssert.Equal(firstStealthReceipt, stealth.Controller.Snapshot().MenuUseContinuation);
        stealth.Send(OverlayInput.Select);
        var stealthOff = stealth.Controller.Snapshot();
        TestAssert.Equal(true, stealthOff.Appearance!.StealthUI);
        TestAssert.Equal(firstStealthReceipt.Sequence + 1, stealthOff.MenuUseContinuation!.Sequence);
        TestAssert.Equal(firstStealthReceipt.Revision + 1, stealthOff.MenuUseContinuation.Revision);
        TestAssert.Equal("ON", RowValue(stealthOff, "Stealth UI"));
        TestAssert.Equal("pending", stealthOff.MenuOptionCallback!.Phase);
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var stealthOffRefreshed = stealth.Controller.Snapshot();
        TestAssert.Equal(false, stealthOffRefreshed.Appearance!.StealthUI);
        TestAssert.Equal("Text", stealthOffRefreshed.Appearance.BooleanDisplay);
        TestAssert.Equal("OFF", RowValue(stealthOffRefreshed, "Stealth UI"));
        TestAssert.Equal("cooldown", stealthOffRefreshed.MenuOptionCallback!.Phase);
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var stealthOffReady = stealth.Controller.Snapshot();
        TestAssert.True(stealthOffReady.MenuOptionCallback is null, "The second Stealth callback becomes ready at860ms");
        TestAssert.Equal(stealthOff.MenuUseContinuation, stealthOffReady.MenuUseContinuation);
    }

    public static void TypedPreferenceWaitsPublishOnlyOptionCallbackMetadataAndLegacyRefreshKeepsItsOwnPhase() {
        var typed = Ready("Spotlight Cursor");
        typed.Send(OverlayInput.Select);
        var typedFrame = typed.Controller.Snapshot();
        TestAssert.True(typedFrame.MenuOptionCallback is not null, "A connected typed preference Select publishes option callback metadata");
        TestAssert.Equal("boolean", typedFrame.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", typedFrame.MenuOptionCallback.Phase);
        TestAssert.True(typedFrame.MenuPreferenceRefresh is null, "A typed source callback cannot publish the legacy refresh phase");
        TestAssert.True(typedFrame.MenuUseContinuation is not null, "The typed Select owns its causal continuation");

        // Actorless input keeps its existing refresh and cannot claim a typed owner receipt
        var legacy = new MenuPreferenceRefreshTests.Rig();
        legacy.Open();
        legacy.Highlight("Spotlight Cursor");
        legacy.Send(OverlayInput.Select);
        var legacyFrame = legacy.Current();
        TestAssert.True(legacyFrame.MenuOptionCallback is null, "An actorless packet cannot borrow typed callback metadata");
        TestAssert.True(legacyFrame.MenuUseContinuation is null, "An actorless packet cannot borrow a typed Use receipt");
        TestAssert.Equal("pending", MenuPreferenceRefreshTests.Phase(legacyFrame));
        TestAssert.Equal("preference:pending", legacy.Controller.ReadMenuPublicationPhase());
        legacy.NoGameWork();
    }

    private static ScopedOpeningFixture Ready(string label, ulong paintRevision = 0, TimeProvider? clock = null) {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, paintRevision, clock);
        int index = rig.Navigation.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(index >= 0, "Required Preferences row is missing: " + label);
        LocalOptionCallbackControllerTests.SelectIndex(rig, index);
        return rig;
    }

    private static string RowValue(StateDocument frame, string label) =>
        frame.Rows.Single(row => row.Label == label).Value;

    // The receipt and timer must refer to the same acknowledged Select
    private static MenuUseContinuation AssertTypedWait(StateDocument frame) {
        TestAssert.True(frame.MenuOptionCallback is not null, "Typed preference Select must own the source boolean wait");
        TestAssert.Equal("boolean", frame.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", frame.MenuOptionCallback.Phase);
        TestAssert.True(frame.MenuPreferenceRefresh is null, "Typed preference Select uses callback metadata");
        TestAssert.True(frame.MenuUseContinuation is not null, "Typed preference Select must retain its causal Use receipt");
        TestAssert.Equal(frame.LastSequence, frame.MenuUseContinuation!.Sequence);
        TestAssert.Equal(frame.MenuOptionCallback.Revision, frame.MenuUseContinuation.Revision);
        TestAssert.Equal("boolean", frame.MenuUseContinuation.Kind);
        return frame.MenuUseContinuation;
    }
}
