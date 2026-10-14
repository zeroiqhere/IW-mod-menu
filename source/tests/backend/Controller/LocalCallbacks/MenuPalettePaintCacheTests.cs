using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests.Controller.LocalCallbacks;

// These cases use pure menu rows and never start a callback or feature operation
internal static class MenuPalettePaintCacheTests {
    public static void CapturesAllThirtySevenPaletteIndicatorsInSourceOrder() {
        var rows = Palette();
        var cache = Capture(rows);

        TestAssert.Equal(37, rows.Length);
        TestAssert.Equal(37, cache.Frame.Rows.Count);
        TestAssert.Equal(11UL, cache.Frame.Revision);
        TestAssert.True(cache.Frame.IsSupported(), "The full palette must remain a supported wire frame");
        for (int index = 0; index < rows.Length; index++) {
            TestAssert.Equal(new MenuBooleanPaintRow(index, index == 6 ? "ON" : "OFF"), cache.Frame.Rows[index]);
        }
    }

    public static void DefaultCaptureStillExcludesPaletteAndRetainsGameBooleans() {
        var rows = Palette();
        TestAssert.True(MenuBooleanPaintCache.Capture(1, rows) is null,
            "Palette eligibility must require the explicit caller opt-in");
        TestAssert.True(MenuBooleanPaintCache.Capture(1, new[] {
            rows[0] with { Action = rows[0].Action with { Slot = -1 } }
        }) is null, "Opt-out must not adopt or validate a palette action as game paint");
        var mixed = rows.Append(new MenuRow("God Mode", "UNKNOWN", false,
            new(MenuActionKind.TogglePersonalGodmode))).ToArray();
        var legacy = MenuBooleanPaintCache.Capture(1, mixed)
            ?? throw new InvalidOperationException("The existing game bool contract must remain available");

        TestAssert.Equal(1, legacy.Frame.Rows.Count);
        TestAssert.Equal(new MenuBooleanPaintRow(37, "UNKNOWN"), legacy.Frame.Rows[0]);
        TestAssert.Equal(38, Capture(mixed).Frame.Rows.Count);
    }

    public static void PaletteOptInDoesNotCaptureSlidersPreferencesAmmoOrSpacers() {
        var rows = new[] {
            new MenuRow("Red", "ON", true, new(MenuActionKind.ChangeAccent, Slot: 0)),
            new MenuRow("Smooth Rainbow", "OFF", true, new(MenuActionKind.EnableSmoothRainbowTheme)),
            new MenuRow("Design", "ON", true, new(MenuActionKind.ChangePreference, Slot: 12)),
            new MenuRow("Menu", "OFF", true, new(MenuActionKind.ChangeOpeningSelector, Slot: 0)),
            new MenuRow("Infinite Ammo", "ON", true, new(MenuActionKind.ToggleLobbyAmmo)),
            new MenuRow("", "", false, new(MenuActionKind.None), IsSpacer: true)
        };
        var cache = Capture(rows);

        TestAssert.Equal(2, cache.Frame.Rows.Count);
        TestAssert.Equal(new MenuBooleanPaintRow(0, "ON"), cache.Frame.Rows[0]);
        TestAssert.Equal(new MenuBooleanPaintRow(1, "OFF"), cache.Frame.Rows[1]);
    }

    public static void LivePaletteChangesPreservePaintUntilAnExplicitFreshCapture() {
        var rows = Palette();
        var initial = Capture(rows);
        var changed = rows.Select((row, index) => row with {
            Value = index == 36 ? "ON" : "OFF", Enabled = false, Detail = "Controls paused"
        }).ToArray();

        // Applying the mode now does not mutate the previous redraw's checked indicators
        TestAssert.True(initial.MatchesTopology(changed), "Live mode and eligibility are not structural changes");
        TestAssert.Equal("ON", initial.Frame.Rows[6].Value);
        TestAssert.Equal("OFF", initial.Frame.Rows[36].Value);
        var refreshed = MenuBooleanPaintCache.Capture(12, changed, includePalette: true)
            ?? throw new InvalidOperationException("Expected explicit palette redraw capture");
        TestAssert.Equal("OFF", refreshed.Frame.Rows[6].Value);
        TestAssert.Equal("ON", refreshed.Frame.Rows[36].Value);
        changed[3] = changed[3] with { Value = "UNKNOWN" };
        TestAssert.Equal("UNKNOWN", Capture(changed).Frame.Rows[3].Value);
    }

    public static void OwnedPalettePaintRejectsStructuralChangesWithoutInputAliasing() {
        var rows = Palette();
        var original = (MenuRow[])rows.Clone();
        var cache = Capture(rows);
        rows[6] = rows[6] with { Label = "Changed", Value = "OFF" };

        TestAssert.True(cache.MatchesTopology(original), "Replacing the input array cannot replace captured topology");
        TestAssert.True(!cache.MatchesTopology(rows), "A label change requires new paint ownership");
        TestAssert.Equal("ON", cache.Frame.Rows[6].Value);
        var changedAction = (MenuRow[])original.Clone();
        changedAction[6] = changedAction[6] with { Action = new(MenuActionKind.ChangeAccent, Slot: 7) };
        TestAssert.True(!cache.MatchesTopology(changedAction), "A different color action retires old paint");
        changedAction[6] = original[6] with { IsSpacer = true };
        TestAssert.True(!cache.MatchesTopology(changedAction), "A source blank cannot inherit color paint");
        TestAssert.True(!cache.MatchesTopology(original.Reverse().ToArray()), "Logical order owns the indicator indices");
        TestAssert.Throws<NotSupportedException>(() =>
            ((IList<MenuBooleanPaintRow>)cache.Frame.Rows)[6] = new(6, "OFF"));
    }

    public static void InvalidPaletteIndicesAndValuesRejectCaptureWithoutPartialPaint() {
        var first = Palette()[0];
        var invalid = new[] {
            first with { Action = first.Action with { Slot = -1 } },
            first with { Action = first.Action with { Slot = MenuThemes.All.Count } },
            first with { Action = new(MenuActionKind.EnableSmoothRainbowTheme, Slot: 0) },
            first with { Action = new((MenuActionKind)int.MaxValue) },
            first with { Action = first.Action with { Page = (MenuPage)int.MaxValue } },
            first with { Value = "UNAVAILABLE" }, first with { Value = "on" }
        };
        foreach (var row in invalid) {
            // A valid earlier color cannot make a malformed later entry partially acceptable
            TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(1,
                new[] { first, row }, includePalette: true));
        }
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuBooleanPaintCache.Capture(0,
            new[] { first }, includePalette: true));
        TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(1,
            Enumerable.Repeat(first, 129).ToArray(), includePalette: true));
        TestAssert.Equal(37, Capture(Palette()).Frame.Rows.Count);
    }

    private static MenuBooleanPaintCache Capture(IReadOnlyList<MenuRow> rows) =>
        MenuBooleanPaintCache.Capture(11, rows, includePalette: true)
            ?? throw new InvalidOperationException("Expected explicitly opted-in palette paint");

    private static MenuRow[] Palette() => MenuThemes.All.Select((theme, index) =>
        new MenuRow(theme.Name, index == 6 ? "ON" : "OFF", true,
            new(MenuActionKind.ChangeAccent, Slot: index)))
        .Append(new("Smooth Rainbow", "OFF", true, new(MenuActionKind.EnableSmoothRainbowTheme))).ToArray();
}
