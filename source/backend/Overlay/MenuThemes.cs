using System;
using System.Collections.Generic;

namespace syndishanx_iw_tool.Overlay;

// Names and RGB values follow the pinned Apparition palette in utilities.gsc
public sealed record MenuTheme(string Name, string Hex);

public static class MenuThemes {
    public const string DefaultAccent = "#3998FE";
    // Preserve source order so the label and color stay paired across menu refreshes
    public static IReadOnlyList<MenuTheme> All { get; } = Array.AsReadOnly(new[] {
        new MenuTheme("Red", "#FF0000"),
        new MenuTheme("Green", "#00FF00"),
        new MenuTheme("Blue", "#0000FF"),
        new MenuTheme("Black", "#000000"),
        new MenuTheme("White", "#FFFFFF"),
        new MenuTheme("Gray", "#808080"),
        new MenuTheme("Dodger Blue", "#3998FE"),
        new MenuTheme("Ocean Blue", "#0064C8"),
        new MenuTheme("Deep Blue", "#00008B"),
        new MenuTheme("Midnight Blue", "#191970"),
        new MenuTheme("Sky Blue", "#87CEFA"),
        new MenuTheme("Cyan", "#00FFFF"),
        new MenuTheme("Aqua", "#00FFC8"),
        new MenuTheme("Teal", "#008080"),
        new MenuTheme("Pink", "#FF6EFF"),
        new MenuTheme("AIO Pink", "#FF96FF"),
        new MenuTheme("Hot Pink", "#FF1493"),
        new MenuTheme("Rose", "#FF66CC"),
        new MenuTheme("Fuchsia", "#FF00FF"),
        new MenuTheme("Purple", "#8000FF"),
        new MenuTheme("Lavender", "#C8A2FF"),
        new MenuTheme("Violet", "#EE82EE"),
        new MenuTheme("Indigo", "#4B0082"),
        new MenuTheme("Plasma Purple", "#C800FF"),
        new MenuTheme("Neon Purple", "#AA00FF"),
        new MenuTheme("Crimson", "#DC143C"),
        new MenuTheme("Fire Red", "#FF1E1E"),
        new MenuTheme("Ruby", "#E0115F"),
        new MenuTheme("Orange", "#FF8000"),
        new MenuTheme("Deep Orange", "#FF5000"),
        new MenuTheme("Yellow", "#FFFF00"),
        new MenuTheme("Gold", "#FFD700"),
        new MenuTheme("Mint", "#98FF98"),
        new MenuTheme("Lime", "#96FF00"),
        new MenuTheme("Toxic Green", "#00FF64"),
        new MenuTheme("Emerald", "#00C957")
    });

    // Out-of-range action payloads never become a different color
    public static MenuTheme? Find(int index) => index >= 0 && index < All.Count ? All[index] : null;
}
