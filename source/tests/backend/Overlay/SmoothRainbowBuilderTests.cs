using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class SmoothRainbowBuilderTests {
    public static void SeparateRainbowModeAddsOneExclusiveSourceRowWithoutChangingTheFixedCatalogOrCapabilities() {
        var boundary = new SessionBoundary();
        var fixedMenu = OverlayMenuBuilder.Build(MenuPage.ThemeColors, boundary.State);
        var rainbow = OverlayMenuBuilder.Build(MenuPage.ThemeColors, boundary.State, smoothRainbowTheme: true);
        TestAssert.Equal(36, MenuThemes.All.Count);
        TestAssert.Equal(37, fixedMenu.Rows.Count);
        TestAssert.Equal(37, rainbow.Rows.Count);
        for (int index = 0; index < 36; index++) {
            TestAssert.Equal(MenuThemes.All[index].Name, rainbow.Rows[index].Label);
            TestAssert.Equal(new MenuAction(MenuActionKind.ChangeAccent, Slot: index), rainbow.Rows[index].Action);
            TestAssert.Equal("OFF", rainbow.Rows[index].Value);
            TestAssert.True(rainbow.Rows[index].Enabled, "Fixed colors remain available to stop rainbow");
        }
        TestAssert.Equal("Dodger Blue", fixedMenu.Rows.Single(row => row.Value == "ON").Label);
        TestAssert.Equal("Smooth Rainbow", rainbow.Rows.Single(row => row.Value == "ON").Label);
        TestAssert.Equal(new MenuAction(MenuActionKind.EnableSmoothRainbowTheme), rainbow.Rows[36].Action);
        TestAssert.Equal("Select to start the color cycle. Selecting it again keeps it running. Choose a fixed color to stop it",
            rainbow.Rows[36].Detail);
        TestAssert.Equal(fixedMenu.Rows[36].Detail, rainbow.Rows[36].Detail);
        var unknownFallback = OverlayMenuBuilder.Build(MenuPage.ThemeColors, boundary.State, "#123456", smoothRainbowTheme: true);
        TestAssert.Equal("Smooth Rainbow", unknownFallback.Rows.Single(row => row.Value == "ON").Label);
        // Presentation modes never change the capabilities supplied by a session
        foreach (var page in new[] { MenuPage.Personal, MenuPage.AllPlayers }) {
            var features = OverlayMenuBuilder.Build(page, boundary.State, smoothRainbowTheme: true);
            TestAssert.True(features.Rows.All(row => !row.Enabled), "Rainbow cannot enable disconnected features");
        }
    }
}
