using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests.Controller.InstructionCallbacks;

// Instruction paint preserves source row values without changing committed appearance
internal static class MenuInstructionPaintCacheTests {
    public static void OptInCapturesFourInstructionIndicatorsWithoutReinterpretingDisableValues() {
        var appearances = new[] {
            MenuAppearance.Default,
            MenuAppearance.Default with { InstructionsVisible = false, InstructionsBackground = false, AdaptiveInstructions = true },
            MenuAppearance.Default with { AlternateInstructions = true }
        };
        var expected = new[] {
            new[] { "OFF", "OFF", "OFF", "OFF" },
            new[] { "ON", "OFF", "ON", "ON" },
            new[] { "OFF", "ON", "OFF", "OFF" }
        };

        for (int caseIndex = 0; caseIndex < appearances.Length; caseIndex++) {
            var rows = MenuInstructionRows.Build(appearances[caseIndex], positionControlsAvailable: true);
            var cache = Capture(rows);

            TestAssert.Equal(4, cache.Frame.Rows.Count);
            for (int index = 0; index < 4; index++) {
                TestAssert.Equal(new MenuBooleanPaintRow(index, expected[caseIndex][index]), cache.Frame.Rows[index]);
            }
            TestAssert.True(cache.Frame.IsSupported(), "Instruction values must remain valid copied paint");
        }
        // A caller's disabled input still carries its copied source boolean value
        var adaptive = MenuInstructionRows.Build(appearances[2], true)[3] with { Enabled = false };
        TestAssert.Equal("OFF", Capture(new[] { adaptive }).Frame.Rows.Single().Value);
    }

    public static void DefaultCaptureExcludesInstructionsAndRetainsGameScope() {
        var instructions = MenuInstructionRows.Build(MenuAppearance.Default, true);
        TestAssert.True(MenuBooleanPaintCache.Capture(1, instructions) is null,
            "Existing callers must not opt into instruction paint implicitly");
        var mixed = instructions.Append(new MenuRow("God Mode", "UNKNOWN", false,
            new MenuAction(MenuActionKind.TogglePersonalGodmode))).ToArray();

        var cache = MenuBooleanPaintCache.Capture(2, mixed)
            ?? throw new InvalidOperationException("Expected the existing gameplay paint scope");

        TestAssert.Equal(1, cache.Frame.Rows.Count);
        TestAssert.Equal(new MenuBooleanPaintRow(6, "UNKNOWN"), cache.Frame.Rows[0]);
    }

    public static void InstructionOptInExcludesUnrelatedPreferencesEditorsChoicesAndSpacers() {
        var supported = new[] { MenuPreference.InstructionsVisible, MenuPreference.AlternateInstructions,
            MenuPreference.InstructionsBackground, MenuPreference.AdaptiveInstructions };
        var rows = Enum.GetValues<MenuPreference>().Except(supported)
            .Select(preference => Preference(preference, "ON")).ToList();
        rows.AddRange(new[] {
            new MenuRow("Reposition", ">", true, new(MenuActionKind.OpenPage, MenuPage.InstructionPositionEditor)),
            new MenuRow("Reset Position", "", true, new(MenuActionKind.ResetInstructionPosition)),
            new MenuRow("Lobby Ammo", "ON", true, new(MenuActionKind.ToggleLobbyAmmo)),
            new MenuRow("Choice", "ON", true, new(MenuActionKind.ChangeOpeningSelector, Slot: 0)),
            new MenuRow("Opening action", "ON", true, new(MenuActionKind.ChangeMainOpeningAction, Slot: 0)),
            new MenuRow("", "", false, new(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.InstructionsVisible), IsSpacer: true),
            new MenuRow("Unrelated action", "ON", true, new(MenuActionKind.None, Slot: (int)MenuPreference.InstructionsVisible)),
            new MenuRow("Unknown preference", "ON", true, new(MenuActionKind.ChangePreference, Slot: int.MaxValue))
        });

        TestAssert.True(MenuBooleanPaintCache.Capture(3, rows, includeInstructions: true) is null,
            "Only the four exact preference slots may join instruction paint");
    }

    public static void CaptureOwnsImmutableIndicatorsAndEveryNonpaintRowTopology() {
        var rows = MenuInstructionRows.Build(MenuAppearance.Default, true).ToList();
        var original = rows.ToArray();
        var cache = Capture(rows);

        rows[0] = rows[0] with { Value = "ON", Label = "Replacement" };
        rows.Clear();

        TestAssert.Equal(new MenuBooleanPaintRow(0, "OFF"), cache.Frame.Rows[0]);
        TestAssert.True(cache.MatchesTopology(original), "Mutable input cannot alter retained topology");
        var publicRows = (IList<MenuBooleanPaintRow>)cache.Frame.Rows;
        TestAssert.True(publicRows.IsReadOnly, "The copied paint must remain read-only");
        TestAssert.Throws<NotSupportedException>(() => publicRows.Clear());
        var changedEditor = original.ToArray();
        changedEditor[4] = changedEditor[4] with { Action = new(MenuActionKind.OpenPage, MenuPage.WidthEditor) };
        TestAssert.True(!cache.MatchesTopology(changedEditor), "Nonpaint editor action identity remains structural");
        TestAssert.True(!cache.MatchesTopology(original.Take(4).ToArray()), "Nonpaint rows remain part of full topology");
    }

    public static void EligibleInstructionValuesRejectAnythingOutsideOnOffUnknown() {
        foreach (var preference in InstructionPreferences()) {
            foreach (var value in new[] { "ON", "OFF", "UNKNOWN" }) {
                var cache = Capture(new[] { Preference(preference, value) });
                TestAssert.Equal(new MenuBooleanPaintRow(0, value), cache.Frame.Rows.Single());
            }
            foreach (var value in new[] { "", "on", "off", "UNAVAILABLE", "true", "ON " }) {
                TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(4,
                    new[] { Preference(preference, value) }, includeInstructions: true));
            }
        }
    }

    public static void InstructionIdentityChangesRetirePaintButCurrentValuesAndEligibilityDoNot() {
        var row = Preference(MenuPreference.AdaptiveInstructions, "OFF");
        var cache = Capture(new[] { row });
        var current = row with { Value = "ON", Enabled = false, Detail = "Handler unavailable" };

        TestAssert.True(cache.MatchesTopology(new[] { current }), "Live values and disabled handlers do not redraw old paint");
        TestAssert.Equal("OFF", cache.Frame.Rows.Single().Value);
        var changed = new[] {
            row with { Label = "Other instruction" },
            row with { Action = row.Action with { Slot = (int)MenuPreference.InstructionsVisible } },
            row with { Action = row.Action with { Kind = MenuActionKind.None } },
            row with { Action = row.Action with { Page = MenuPage.Instructions } },
            row with { Action = row.Action with { ExpectedName = "changed" } },
            row with { IsSpacer = true },
            row with { OpeningAction = OpeningAction.Aim }
        };
        foreach (var candidate in changed) {
            TestAssert.True(!cache.MatchesTopology(new[] { candidate }), "Every copied row identity change must retire paint");
        }
    }

    public static void InstructionAndPaletteOptInsPreserveTheirIndependentExistingScopes() {
        var rows = new[] {
            Preference(MenuPreference.InstructionsBackground, "ON"),
            new MenuRow("Fixed color", "OFF", true, new(MenuActionKind.ChangeAccent, Slot: 19)),
            new MenuRow("Smooth Rainbow", "ON", true, new(MenuActionKind.EnableSmoothRainbowTheme)),
            new MenuRow("God Mode", "UNKNOWN", false, new(MenuActionKind.ToggleTeammateGodmode, Slot: 2))
        };
        var instructionOnly = Capture(rows);
        var paletteOnly = MenuBooleanPaintCache.Capture(5, rows, includePalette: true)
            ?? throw new InvalidOperationException("Expected existing palette paint");
        var both = MenuBooleanPaintCache.Capture(5, rows, includePalette: true, includeInstructions: true)
            ?? throw new InvalidOperationException("Expected combined paint scopes");

        TestAssert.Equal("0,3", string.Join(",", instructionOnly.Frame.Rows.Select(row => row.Index)));
        TestAssert.Equal("1,2,3", string.Join(",", paletteOnly.Frame.Rows.Select(row => row.Index)));
        TestAssert.Equal("0,1,2,3", string.Join(",", both.Frame.Rows.Select(row => row.Index)));
        TestAssert.Equal("ON", both.Frame.Rows[0].Value);
        TestAssert.True(both.Frame.IsSupported(), "Combined scopes still satisfy the existing wire contract");
    }

    private static MenuPreference[] InstructionPreferences() => new[] {
        MenuPreference.InstructionsVisible, MenuPreference.AlternateInstructions,
        MenuPreference.InstructionsBackground, MenuPreference.AdaptiveInstructions
    };

    private static MenuRow Preference(MenuPreference preference, string value) =>
        new(preference.ToString(), value, true, new(MenuActionKind.ChangePreference, Slot: (int)preference));

    // Only this explicit caller flag admits the local instruction indicators
    private static MenuBooleanPaintCache Capture(IReadOnlyList<MenuRow> rows) =>
        MenuBooleanPaintCache.Capture(9, rows, includeInstructions: true)
        ?? throw new InvalidOperationException("Expected instruction paint");
}
