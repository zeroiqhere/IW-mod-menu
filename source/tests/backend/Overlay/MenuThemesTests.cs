using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuThemesTests {
    public static void PaletteMatchesAllThirtySixOrderedSourceNamesAndRgbValues() {
        // This fixture contains the source export, not names inferred from common colors
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "reference-palette.json")));
        TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", source.RootElement.GetProperty("commit").GetString());
        var colors = source.RootElement.GetProperty("colors");
        TestAssert.Equal(36, colors.GetArrayLength());
        TestAssert.Equal(36, MenuThemes.All.Count);
        for (int index = 0; index < colors.GetArrayLength(); index++) {
            var rgb = colors[index].GetProperty("rgb");
            string hex = $"#{rgb[0].GetInt32():X2}{rgb[1].GetInt32():X2}{rgb[2].GetInt32():X2}";
            TestAssert.Equal(colors[index].GetProperty("name").GetString(), MenuThemes.All[index].Name);
            TestAssert.Equal(hex, MenuThemes.All[index].Hex);
        }
        TestAssert.Equal(36, MenuThemes.All.Select(theme => theme.Hex).Distinct().Count());
    }

    public static void DefaultAndFormerApproximateColorsUseExactReferenceValues() {
        TestAssert.Equal("#3998FE", MenuThemes.DefaultAccent);
        TestAssert.Equal("#3998FE", MenuThemes.All.Single(theme => theme.Name == "Dodger Blue").Hex);
        TestAssert.Equal("#98FF98", MenuThemes.All.Single(theme => theme.Name == "Mint").Hex);
        TestAssert.Equal("#FFD700", MenuThemes.All.Single(theme => theme.Name == "Gold").Hex);
    }

    public static void CatalogRejectsInvalidIndicesInsteadOfClampingOrWrappingThem() {
        foreach (int index in new[] { int.MinValue, -1, 36, int.MaxValue }) {
            TestAssert.True(MenuThemes.Find(index) is null, "Invalid color indices cannot select a replacement color");
        }
        TestAssert.Equal("Red", MenuThemes.Find(0)?.Name);
        TestAssert.Equal("Emerald", MenuThemes.Find(35)?.Name);
    }

    public static void BuilderChecksOnlyActualAccentAndNeverInventsAnUnknownSelection() {
        var state = new SessionBoundary().State;
        var current = OverlayMenuBuilder.Build(MenuPage.ThemeColors, state, "#98FF98");
        TestAssert.Equal("Main Design Color", current.Title);
        TestAssert.Equal(37, current.Rows.Count);
        TestAssert.Equal(1, current.Rows.Count(row => row.Value == "ON"));
        TestAssert.Equal("Mint", current.Rows.Single(row => row.Value == "ON").Label);
        TestAssert.True(current.Rows.Take(36).All(row => row.Enabled && row.Action.Kind == MenuActionKind.ChangeAccent),
            "Appearance remains available without a game connection");
        TestAssert.Equal("Smooth Rainbow", current.Rows[36].Label);
        TestAssert.Equal("OFF", current.Rows[36].Value);
        TestAssert.Equal(MenuActionKind.EnableSmoothRainbowTheme, current.Rows[36].Action.Kind);
        var unknown = OverlayMenuBuilder.Build(MenuPage.ThemeColors, state, "#123456");
        TestAssert.True(unknown.Rows.All(row => row.Value == "OFF"), "An unknown accent cannot show a checked catalog color");
    }
}
