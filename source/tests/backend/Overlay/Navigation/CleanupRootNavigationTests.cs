using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class CleanupRootNavigationTests {
    public static void CleanupRootReplacesTheMainPathButPreservesTheQuickParentHistory() {
        var nav = new OverlayNavigation();
        nav.Open(); nav.OpenPage(MenuPage.Customization); nav.Close();
        nav.Open(MenuSurface.Quick); nav.OpenPage(MenuPage.Personal);
        ulong previous = nav.Revision;
        nav.OpenCleanupRoot();
        TestAssert.True(nav.IsOpen && nav.Surface == MenuSurface.Main && nav.Page == MenuPage.Main,
            "Administrative cleanup must open the actual Main root");
        TestAssert.True(nav.Revision > previous, "Cleanup replacement must revoke captured actions");
        nav.Handle(OverlayInput.Back);
        TestAssert.True(!nav.IsOpen, "Cleanup Main cannot retain an older parent path");
        nav.Open(MenuSurface.Quick);
        TestAssert.Equal(MenuPage.Personal, nav.Page);
        nav.Handle(OverlayInput.Back);
        TestAssert.Equal(MenuPage.QuickMenu, nav.Page);
    }

    public static void CleanupRootCannotPartiallyChangeAnOwnerWhenItsCounterIsExhausted() {
        var nav = new OverlayNavigation(ulong.MaxValue - 1, 0);
        nav.Open(MenuSurface.Quick);
        ulong presentation = nav.MenuRevision;
        TestAssert.Throws<OverflowException>(() => nav.OpenCleanupRoot());
        TestAssert.True(nav.IsOpen && nav.Surface == MenuSurface.Quick && nav.Page == MenuPage.QuickMenu,
            "A failed administrative transition cannot replace the family");
        TestAssert.Equal(ulong.MaxValue, nav.Revision);
        TestAssert.Equal(presentation, nav.MenuRevision);
    }
}
