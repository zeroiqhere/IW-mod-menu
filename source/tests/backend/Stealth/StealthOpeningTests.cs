using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StealthOpeningTests {
    public static void RetainedOpeningContinuesDuringPendingRefreshAndStopsOnlyAtThe180MillisecondRedraw() {
        foreach (string label in new[] { "Quick Exit", "Spotlight Cursor", "Colored Cursor", "Large Cursor", "Stealth UI" }) {
            var rig = Retained(label);
            var opening = rig.Current().MenuOpening;
            TestAssert.True(opening is not null, "The input-ready retained page must still have an active row fade");
            rig.Send(OverlayInput.Select);
            var pending = rig.Current();
            TestAssert.Equal("pending", pending.MenuPreferenceRefresh!.Phase);
            TestAssert.Equal(opening, pending.MenuOpening);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
            var last = rig.Current();
            TestAssert.Equal(opening!.Revision, last.MenuOpening!.Revision);
            TestAssert.Equal(opening.ElapsedMilliseconds + 179, last.MenuOpening.ElapsedMilliseconds);
            TestAssert.Equal(pending.MenuRevision, last.MenuRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var applied = rig.Current();
            TestAssert.True(applied.MenuOpening is null, "Only the actual option redraw ends its old fade");
            TestAssert.Equal("cooldown", applied.MenuPreferenceRefresh!.Phase);
            TestAssert.Equal(pending.MenuRevision + 1, applied.MenuRevision);
            rig.NoGameWork();
        }
    }

    internal static MenuDesignTests.Rig Retained(string label) {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Highlight(label);
        rig.Send(OverlayInput.Toggle);
        rig.Send(OverlayInput.Toggle);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        rig.Current();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550));
        TestAssert.True(rig.Current().MenuOpenTransition is null, "The normal opening input cooldown must finish before selection");
        return rig;
    }
}
