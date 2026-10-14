using System.Text.Json;

namespace Backend.Tests;

internal static class MenuFixtureExporterTests {
    public static void OfflineFixturesUseActualRowsAndDisableAllGameFeatures() {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        TestAssert.Equal("Apparition", fixtures["main.json"].Title);
        TestAssert.Equal(8, fixtures["main.json"].Rows.Count);
        TestAssert.True(!fixtures["main.json"].Rows[0].Enabled, "Preview cannot connect");
        TestAssert.Equal("Basic Scripts", fixtures["basic-scripts.json"].Title);
        TestAssert.Equal(31, fixtures["basic-scripts.json"].Rows.Count);
        TestAssert.Equal("God Mode", fixtures["basic-scripts.json"].Rows[0].Label);
        TestAssert.Equal("UNKNOWN", fixtures["basic-scripts.json"].Rows[0].Value);
        // Reference catalog rows remain inert even when their labels resemble working lobby features
        foreach (var row in fixtures["basic-scripts.json"].Rows.Skip(1)) {
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.True(!row.Enabled,
                "Offline catalog declarations cannot introduce game callbacks");
        }
        TestAssert.Equal("Unlimited Ammo", fixtures["basic-scripts.json"].Rows[5].Label);
        // Action intents and selected help belong to the model, not the display-only wire row
        var personalRows = syndishanx_iw_tool.Overlay.BasicScriptsMenuRows.Build(new SessionBoundary().State);
        TestAssert.True(personalRows.Skip(1).All(row => !row.Enabled
            && row.Action.Kind == syndishanx_iw_tool.Overlay.MenuActionKind.None),
            "The actual source declarations have no dispatch intent");
        TestAssert.True(personalRows[5].Detail.Contains("Personal", StringComparison.Ordinal),
            "The source ammo row must retain its distinct personal scope");
        TestAssert.Equal(12, fixtures["all-players.json"].Rows.Count);
        TestAssert.Equal("God Mode", fixtures["all-players.json"].Rows[5].Label);
        TestAssert.Equal("UNAVAILABLE", fixtures["all-players.json"].Rows[5].Value);
        TestAssert.True(!fixtures["all-players.json"].Rows[5].Enabled, "An offline all-player capability cannot enable actions");
        TestAssert.Equal("Infinite Ammo (IW lobby)", fixtures["all-players.json"].Rows[11].Label);
        TestAssert.Equal("UNKNOWN", fixtures["all-players.json"].Rows[11].Value);
        foreach (string name in new[] { "basic-scripts.json", "all-players.json" }) {
            TestAssert.True(fixtures[name].Rows.All(row => !row.Enabled), "Offline features must remain unavailable");
        }
        foreach (var pair in fixtures) {
            var fixture = pair.Value;
            bool roster = pair.Key is "menu-initial-open-single-drawn.json" or "menu-initial-open-dynamic-drawn.json" or "menu-initial-open-dynamic-changed.json";
            TestAssert.Equal(roster ? "DISCONNECTED" : pair.Key == "opening-pending.json" ? "PREVIEW · WORKING" : "PREVIEW", fixture.Context);
            TestAssert.Equal(!ScopedOpeningFixtureProfiles.ClosedProfiles.Contains(pair.Key) && pair.Key is not "menu-initial-open-focus-hidden.json" and not "open-controls-hidden.json" and not "open-controls-closed.json" and not "quick-controls-hidden.json" and not "quick-controls-closed.json", fixture.IsOpen);
            TestAssert.True(roster ? fixture.Rows.All(row => !row.Enabled) && fixture.Notice.Contains("Spawn first", StringComparison.Ordinal)
                : fixture.Notice.Contains("Preview", StringComparison.Ordinal), "Owned offline profiles preserve truthful connection and feature availability");
            // Source catalog labels are allowed only as inert declarations, never as substituted working features
            foreach (var row in fixture.Rows.Where(row => row.Label is "Noclip" or "Unlimited Ammo")) {
                TestAssert.True(!row.Enabled && row.Value == "UNAVAILABLE",
                    "Unimplemented source declarations cannot advertise a working capability or action");
            }
        }
    }

    public static void InstructionFixturesContainActualControllerValuesAndSourceMutualExclusion() {
        var fixtures = MenuFixtureExporter.Build();
        string[] names = { "instructions-default.json", "instructions-alternate-no-background.json",
            "instructions-adaptive.json", "instructions-hidden.json" };
        var initial = syndishanx_iw_tool.Overlay.MenuAppearance.Default;
        var expected = new[] { initial, initial with { AlternateInstructions = true, InstructionsBackground = false },
            initial with { AdaptiveInstructions = true }, initial with { InstructionsVisible = false } };
        string[][] values = { new[] { "OFF", "OFF", "OFF", "OFF" }, new[] { "OFF", "ON", "ON", "OFF" },
            new[] { "OFF", "OFF", "OFF", "ON" }, new[] { "ON", "OFF", "OFF", "OFF" } };
        for (int index = 0; index < names.Length; index++) {
            var state = fixtures[names[index]];
            TestAssert.Equal(expected[index], state.Appearance);
            TestAssert.Equal("Menu Instructions", state.Title);
            TestAssert.Equal("#3998FE", state.Accent);
            TestAssert.True(state.IsOpen && !state.IsRootMenu && state.Context == "PREVIEW",
                "Every instruction profile must be an actual open offline child page");
            TestAssert.True(state.Rows.Take(4).Select(row => row.Value).SequenceEqual(values[index]),
                "Visible Disable boxes must reflect the inverse committed fields");
            TestAssert.True(!state.Rows[4].Enabled && state.Rows[4].Value == "UNAVAILABLE",
                "The original profiles do not advertise unprovided movement controls");
            TestAssert.True(state.Rows[5].Enabled && state.Rows[5].Value == "", "Local Reset must remain available in the old profiles");
            TestAssert.True(state.Rows[3].Enabled, "The source declares Adaptive as selectable even when its handler rejects the conflicting setting");
            using var json = JsonDocument.Parse(state.Serialize());
            var appearance = json.RootElement.GetProperty("appearance");
            TestAssert.Equal(expected[index].AlternateInstructions, appearance.GetProperty("alternateInstructions").GetBoolean());
            TestAssert.Equal(expected[index].InstructionsBackground, appearance.GetProperty("instructionsBackground").GetBoolean());
            TestAssert.Equal(expected[index].AdaptiveInstructions, appearance.GetProperty("adaptiveInstructions").GetBoolean());
        }
        foreach (var original in fixtures.Where(item => !names.Contains(item.Key)
            && item.Key is not "preferences-design-adaptive-aio-rebuild.json" and not "preferences-design-adaptive-aio-pre-rebuild.json")) {
            TestAssert.True(original.Value.Appearance is { AlternateInstructions: false, InstructionsBackground: true, AdaptiveInstructions: false },
                "All retained profiles must preserve their instruction preferences");
        }
    }

    public static void PositionFixturesExposeActualPhasesCommittedMovesResetAndHiddenResetCapability() {
        var fixtures = MenuFixtureExporter.Build();
        string[] names = { "instructions-position-entering.json", "instructions-position-editing.json",
            "instructions-position-moved.json", "instructions-position-exiting.json" };
        string[] phases = { "entering", "editing", "editing", "exiting" };
        ulong epoch = fixtures[names[0]].NavigationRevision;
        for (int index = 0; index < names.Length; index++) {
            var editor = fixtures[names[index]];
            TestAssert.Equal(phases[index], editor.InstructionPositionEditor?.Phase);
            TestAssert.True(editor.IsOpen && !editor.IsRootMenu && editor.Rows.Count == 0 && editor.SelectedIndex == 0
                && editor.WidthEditor is null && editor.MenuUnlock is null && editor.Appearance?.InstructionsVisible == true,
                "Editor fixtures must expose valid dedicated owner state");
            TestAssert.Equal(epoch, editor.NavigationRevision);
            TestAssert.Equal("Menu Instructions", editor.Title);
            TestAssert.Equal(index < 2 ? null : new syndishanx_iw_tool.Overlay.InstructionPosition(263, 480), editor.Appearance?.InstructionPosition);
        }
        foreach (string name in new[] { "instructions-position-reset.json", "instructions-hidden-reset.json" }) {
            var parent = fixtures[name];
            TestAssert.True(parent.IsOpen && parent.InstructionPositionEditor is null && parent.MenuUnlock is null
                && parent.Appearance?.InstructionPosition is null, "Finished reset fixtures must be ordinary unlocked parent pages");
            TestAssert.Equal("Reset Position", parent.Rows[parent.SelectedIndex].Label);
            TestAssert.True(parent.Rows[5].Enabled && parent.Rows[5].Value == "", "Reset stays available with hidden instructions");
        }
        TestAssert.True(fixtures["instructions-hidden-reset.json"].Appearance?.InstructionsVisible == false
            && !fixtures["instructions-hidden-reset.json"].Rows[4].Enabled, "Hidden instructions cannot reposition");
    }

    public static void ExportedPaletteShowsActualDefaultAndSelectedMintExclusively() {
        var fixtures = MenuFixtureExporter.Build();
        var initial = fixtures["theme-colors-default.json"];
        TestAssert.Equal(37, initial.Rows.Count);
        TestAssert.Equal("#3998FE", initial.Accent);
        TestAssert.Equal("Dodger Blue", initial.Rows.Single(row => row.Value == "ON").Label);
        var mint = fixtures["theme-colors-mint.json"];
        TestAssert.Equal("#98FF98", mint.Accent);
        TestAssert.Equal("Mint", mint.Rows.Single(row => row.Value == "ON").Label);
        TestAssert.Equal("Mint", mint.Rows[mint.SelectedIndex].Label);
    }

    public static void ExportPublishesCompleteJsonIntoANewDirectoryWithoutOverwritingExistingFiles() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-export-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try {
            string destination = Path.Combine(parent, "preview");
            MenuFixtureExporter.Export(destination);
            TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, Directory.GetFiles(destination).Length);
            foreach (string file in Directory.GetFiles(destination)) {
                using var json = JsonDocument.Parse(File.ReadAllBytes(file));
                bool roster = Path.GetFileName(file) is "menu-initial-open-single-drawn.json" or "menu-initial-open-dynamic-drawn.json" or "menu-initial-open-dynamic-changed.json";
                TestAssert.Equal(roster ? "DISCONNECTED" : Path.GetFileName(file) == "opening-pending.json" ? "PREVIEW · WORKING" : "PREVIEW", json.RootElement.GetProperty("context").GetString());
            }
            string sentinel = Path.Combine(destination, "main.json");
            byte[] before = File.ReadAllBytes(sentinel);
            TestAssert.Throws<IOException>(() => MenuFixtureExporter.Export(destination));
            TestAssert.True(before.SequenceEqual(File.ReadAllBytes(sentinel)), "Existing output cannot be overwritten");
            TestAssert.Equal(1, Directory.GetDirectories(parent).Length);
            TestAssert.Throws<ArgumentException>(() => MenuFixtureExporter.Export("relative-directory"));
        } finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    public static void ExportCliRejectsUnknownAndIncompleteArgumentsWithoutWritingOutput() {
        TestAssert.Equal(1, MenuFixtureExporter.Run(new[] { "--export-menu-fixtures" }));
        TestAssert.Equal(1, MenuFixtureExporter.Run(new[] { "--unknown", "relative-directory" }));
    }

    public static void ExportedPreferencesContainActualDefaultAndChangedControllerState() {
        var fixtures = MenuFixtureExporter.Build();
        var initial = fixtures["preferences-default.json"];
        TestAssert.Equal("Menu Preferences", initial.Title);
        TestAssert.Equal(12, initial.Rows.Count);
        TestAssert.Equal(syndishanx_iw_tool.Overlay.MenuAppearance.Default, initial.Appearance);
        var draft = fixtures["preferences-draft.json"];
        TestAssert.Equal("Text", draft.Rows[1].Value);
        TestAssert.Equal("Boxes", draft.Appearance?.BooleanDisplay);
        TestAssert.True(draft.Notice.Contains("Candidate: Text; Applied: Boxes", StringComparison.Ordinal),
            "The preview must distinguish a pending candidate from its applied appearance");
        var changed = fixtures["preferences-text-left.json"];
        TestAssert.Equal("Text", changed.Appearance?.BooleanDisplay);
        TestAssert.Equal("Left", changed.Appearance?.BoolBoxLocation);
        TestAssert.True(changed.Appearance is { InstructionsVisible: false, SpotlightCursor: false,
            ColoredCursor: false, LargeCursor: true, OptionCounter: true, QuickExit: false }, "Exported flags must reflect real selections");
        TestAssert.Equal("#98FF98", changed.Accent);
        TestAssert.Equal(9, changed.SelectedIndex);
        TestAssert.Equal("ON", changed.Rows[changed.SelectedIndex].Value);
    }
    public static void QuickExitFixturesExposeCommittedLocalOptionAndExplicitRootIdentity() {
        var fixtures = MenuFixtureExporter.Build();
        var nested = fixtures["preferences-quick-exit.json"];
        var root = fixtures["root-quick-exit.json"];
        TestAssert.True(nested.Appearance?.QuickExit == true && !nested.IsRootMenu,
            "The nested preview must reflect the committed local option");
        TestAssert.Equal("Menu Preferences", nested.Title);
        TestAssert.Equal(4, nested.SelectedIndex);
        TestAssert.Equal("Quick Exit", nested.Rows[4].Label);
        TestAssert.Equal("ON", nested.Rows[4].Value);
        TestAssert.True(nested.Notice.Contains("hold melee closes", StringComparison.Ordinal),
            "The selected option must explain tap and hold");
        TestAssert.True(root.Appearance?.QuickExit == true && root.IsRootMenu,
            "The root preview must retain the same option and explicit root identity");
        TestAssert.Equal("Apparition", root.Title);
        TestAssert.Equal(8, root.Rows.Count);
        foreach (var state in new[] { nested, root }) {
            using var json = JsonDocument.Parse(state.Serialize());
            TestAssert.True(json.RootElement.GetProperty("appearance").GetProperty("quickExit").GetBoolean(),
                "Committed Quick Exit must serialize as a true boolean");
            TestAssert.Equal(state.IsRootMenu, json.RootElement.GetProperty("isRootMenu").GetBoolean());
        }
        foreach (string name in fixtures.Keys.Where(name => name is not "preferences-quick-exit.json" and not "root-quick-exit.json"
            && !name.StartsWith("preferences-refresh-", StringComparison.Ordinal))) {
            TestAssert.True(fixtures[name].Appearance?.QuickExit == false,
                "The original nine fixtures must retain the default Quick Exit value");
        }
    }

    public static void WidthFixturesComeFromCommittedEditorStateAndFinishIntoItsActualParent() {
        var fixtures = MenuFixtureExporter.Build();
        var initial = fixtures["width-editor-default.json"];
        var wide = fixtures["width-editor-wide.json"];
        var parent = fixtures["customization-wide.json"];
        TestAssert.Equal(260, initial.Appearance?.MenuWidth);
        TestAssert.Equal(1, initial.WidthEditor?.Step);
        TestAssert.Equal(500, wide.Appearance?.MenuWidth);
        TestAssert.Equal(10, wide.WidthEditor?.Step);
        foreach (var editor in new[] { initial, wide }) {
            TestAssert.True(editor.IsOpen && !editor.IsRootMenu && editor.Rows.Count == 0 && editor.SelectedIndex == 0,
                "The preview must use actual dedicated editor metadata");
            TestAssert.Equal("Width Editor", editor.Title);
        }
        TestAssert.Equal(initial.NavigationRevision, wide.NavigationRevision);
        TestAssert.Equal("Menu Customization", parent.Title);
        TestAssert.Equal(500, parent.Appearance?.MenuWidth);
        TestAssert.True(parent.WidthEditor is null && parent.IsOpen, "Melee must finish rather than close the parent");
        TestAssert.Equal("Width Editor", parent.Rows[parent.SelectedIndex].Label);
        foreach (var fixture in fixtures.Where(item => item.Key is not "width-editor-wide.json" and not "customization-wide.json"
            and not "customization-unlock-rebuild.json" and not "customization-unlock-pause.json")) {
            TestAssert.Equal(260, fixture.Value.Appearance?.MenuWidth);
        }
    }

    public static void UnlockFixturesExposeActualControlledClockPhasesAndPreserveTheUnlockedParent() {
        var fixtures = MenuFixtureExporter.Build();
        var rebuild = fixtures["customization-unlock-rebuild.json"];
        var pause = fixtures["customization-unlock-pause.json"];
        var unlocked = fixtures["customization-wide.json"];
        TestAssert.Equal("rebuild", rebuild.MenuUnlock);
        TestAssert.Equal("pause", pause.MenuUnlock);
        TestAssert.True(unlocked.MenuUnlock is null, "The original parent fixture must remain fully unlocked");
        foreach (var locked in new[] { rebuild, pause }) {
            TestAssert.True(locked.IsOpen && !locked.IsRootMenu && locked.WidthEditor is null,
                "The phase must retain actual parent state without fabricating an editor");
            TestAssert.Equal("Menu Customization", locked.Title);
            TestAssert.Equal(unlocked.Appearance, locked.Appearance);
            TestAssert.Equal(unlocked.NavigationRevision, locked.NavigationRevision);
            TestAssert.Equal(unlocked.SelectedIndex, locked.SelectedIndex);
            TestAssert.True(locked.Rows.SequenceEqual(unlocked.Rows), "A transient pause cannot remove real row capabilities");
            TestAssert.True(locked.Notice.Contains("Returning to menu; controls paused", StringComparison.Ordinal)
                && !locked.Notice.Contains("Select to open Width Editor", StringComparison.Ordinal), "Only active pause help belongs in these fixtures");
        }
        foreach (var fixture in fixtures.Where(item => item.Key is not "customization-unlock-rebuild.json" and not "customization-unlock-pause.json"
            and not "menu-position-unlock-rebuild.json" and not "menu-position-unlock-pause.json"
            and not "menu-credits-unlock-rebuild.json" and not "menu-credits-unlock-pause.json")) {
            TestAssert.True(fixture.Value.MenuUnlock is null, "Ordinary profiles must remain unlocked");
        }
    }

}
