using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class RejectedPreferencesControllerTests {
    private static long nextAccount = 900_000;

    public static void StealthOptionCounterRemainsSelectableAndRejectedUseOwnsBothBooleanWaits() {
        var scope = new LocalMenuPreferenceScope((ulong)Interlocked.Increment(ref nextAccount), new object());
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, preferenceScope: scope);
        LocalOptionCallbackControllerTests.SelectIndex(rig, 10);
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        var stealthReady = rig.Controller.Snapshot();
        TestAssert.True(stealthReady.Appearance!.StealthUI, "The connected controller must commit Stealth before testing its rejected counter row");

        LocalOptionCallbackControllerTests.SelectIndex(rig, 9);
        var before = rig.Controller.Snapshot();
        var counterRow = before.Rows.Single(row => row.Label == "Option Counter");
        TestAssert.True(counterRow.Enabled, "The source BOOL remains selectable while Stealth rejects its callback");
        var persistedBefore = ReloadAppearance(rig);

        rig.Send(OverlayInput.Select);
        var pending = rig.Controller.Snapshot();
        var receipt = AssertRejectedBoolean(pending, before.LastSequence + 1);
        TestAssert.Equal(before.Appearance, pending.Appearance);
        TestAssert.Equal(counterRow.Value, pending.Rows.Single(row => row.Label == "Option Counter").Value);
        TestAssert.True(pending.Notice.Contains("You Can't Use The Option Counter While Stealth UI Is Enabled", StringComparison.Ordinal),
            "Rejected counter feedback names the active Stealth rule");
        TestAssert.Equal(persistedBefore, ReloadAppearance(rig));
        TestAssert.Equal(0, rig.Session.Executed.Count);

        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        var wait179 = rig.Controller.Snapshot();
        TestAssert.Equal("pending", wait179.MenuOptionCallback!.Phase);
        TestAssert.Equal(before.MenuRevision, wait179.MenuRevision);
        TestAssert.Equal(counterRow.Value, wait179.Rows.Single(row => row.Label == "Option Counter").Value);
        TestAssert.Equal(receipt, wait179.MenuUseContinuation);

        // The source refresh redraws the unchanged rejected row at180ms
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var redraw180 = rig.Controller.Snapshot();
        TestAssert.Equal("cooldown", redraw180.MenuOptionCallback!.Phase);
        TestAssert.Equal(before.MenuRevision!.Value + 1, redraw180.MenuRevision!.Value);
        TestAssert.Equal(counterRow.Value, redraw180.Rows.Single(row => row.Label == "Option Counter").Value);
        TestAssert.Equal(before.Appearance, redraw180.Appearance);
        TestAssert.Equal(receipt, redraw180.MenuUseContinuation);

        rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
        TestAssert.Equal("cooldown", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var ready430 = rig.Controller.Snapshot();
        TestAssert.True(ready430.MenuOptionCallback is null, "The rejected source BOOL wait ends at430ms");
        TestAssert.Equal(receipt, ready430.MenuUseContinuation);

        // A ready held Select begins a new source callback and owns a new receipt
        rig.Send(OverlayInput.Select);
        var repeated = rig.Controller.Snapshot();
        var repeatedReceipt = AssertRejectedBoolean(repeated, ready430.LastSequence + 1);
        TestAssert.Equal(receipt.Revision + 1, repeatedReceipt.Revision);
        TestAssert.Equal(before.Appearance, repeated.Appearance);
        TestAssert.True(repeated.Notice.Contains("You Can't Use The Option Counter While Stealth UI Is Enabled", StringComparison.Ordinal),
            "The repeated rejection retains its exact source reason");

        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw610 = rig.Controller.Snapshot();
        TestAssert.Equal("cooldown", redraw610.MenuOptionCallback!.Phase);
        TestAssert.Equal(before.MenuRevision!.Value + 2, redraw610.MenuRevision!.Value);
        TestAssert.Equal(repeatedReceipt, redraw610.MenuUseContinuation);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var ready860 = rig.Controller.Snapshot();
        TestAssert.True(ready860.MenuOptionCallback is null, "The repeated rejected BOOL becomes ready at860ms");
        TestAssert.Equal(repeatedReceipt, ready860.MenuUseContinuation);
        TestAssert.Equal(persistedBefore, ReloadAppearance(rig));
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    private static MenuAppearance? ReloadAppearance(ScopedOpeningFixture rig) =>
        new BackendController(42, rig.Session, choiceControlsAvailable: true, clock: rig.Clock).Snapshot().Appearance;

    private static MenuUseContinuation AssertRejectedBoolean(StateDocument frame, ulong sequence) {
        TestAssert.True(frame.MenuOptionCallback is not null, "A selectable rejected source BOOL owns callback metadata");
        TestAssert.Equal("boolean", frame.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", frame.MenuOptionCallback.Phase);
        TestAssert.True(frame.MenuUseContinuation is not null, "A rejected source BOOL owns its causal held-Use receipt");
        TestAssert.Equal(sequence, frame.MenuUseContinuation!.Sequence);
        TestAssert.Equal(frame.MenuOptionCallback.Revision, frame.MenuUseContinuation.Revision);
        TestAssert.Equal("boolean", frame.MenuUseContinuation.Kind);
        TestAssert.True(frame.MenuPreferenceRefresh is null, "Connected source callback uses its typed callback timeline");
        return frame.MenuUseContinuation;
    }
}
