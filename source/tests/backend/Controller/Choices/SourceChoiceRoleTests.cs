using System.Text.Json;
using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Independent literal declarations distinguish unavailable choices from non-slider options
internal static class SourceChoiceRoleTests {
    public static void BasicAndQuickDeclarationsKeepExactPrivateSliderRolesWithoutGrantingActions() {
        var state = SourcePlayerInventoryTests.State(PlayerMenuRoute.Personal);
        var rows = BasicScriptsMenuRows.Build(state); TestAssert.Equal(31, rows.Count);
        for (int index = 0; index < rows.Count; index++) {
            var expected = index is 11 or 24 ? MenuSourceChoiceRole.IncrementSlider
                : index is 5 or 8 or 14 or 20 or 21 or 22 or 30 ? MenuSourceChoiceRole.Slider : MenuSourceChoiceRole.NonSlider;
            TestAssert.Equal(expected, rows[index].SourceChoiceRole);
            if (index != 0) { TestAssert.Equal("UNAVAILABLE", rows[index].Value); TestAssert.True(!rows[index].Enabled
                && rows[index].Action.Kind == MenuActionKind.None, "Source shape cannot enable a missing callback"); }
        }
        foreach (var route in new[] { PlayerMenuRoute.Personal, PlayerMenuRoute.Teammate }) {
            var selected = SourcePlayerInventoryTests.State(route);
            var copied = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, selected).Rows;
            TestAssert.True(rows.Select(row => row.SourceChoiceRole).SequenceEqual(copied.Select(row => row.SourceChoiceRole)),
                "Selected Basic Scripts preserve every original declaration role");
            TestAssert.Equal(selected.SelectedPlayerTarget, copied[0].Action.ExpectedTarget);
        }
        var identity = state.SelectedPlayerTarget!.Identity;
        foreach (var life in new[] { LocalPlayerLife.Alive, LocalPlayerLife.Dead }) {
            var facts = new QuickMenuEligibility(life == LocalPlayerLife.Alive ? QuickPlayerLife.Living : QuickPlayerLife.Dead, true, null);
            var observation = new LocalPlayerObservation(42, identity, life, true, false, "Supplied test actor facts");
            var quick = QuickMenuRows.Build(state, facts, observation, identity, 42);
            var expected = life == LocalPlayerLife.Alive
                ? new[] { 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0 }
                : new[] { 0, 1, 0 };
            TestAssert.Equal(expected.Length, quick.Count);
            for (int index = 0; index < quick.Count; index++) TestAssert.Equal(expected[index] == 1
                ? MenuSourceChoiceRole.Slider : MenuSourceChoiceRole.NonSlider, quick[index].SourceChoiceRole);
        }
    }

    public static void UnsupportedSourceSlidersWait180WhileKnownNonslidersWait50AndUnknownRowsDoNeither() {
        foreach (var entry in new[] { (Index: 5, Kind: "choice"), (Index: 11, Kind: "choice"), (Index: 1, Kind: "choiceNoop") }) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Personal);
            LocalOptionCallbackControllerTests.SelectIndex(rig, entry.Index); var before = rig.Controller.Snapshot();
            rig.Send(OverlayInput.NextChoice); var start = rig.Controller.Snapshot();
            TestAssert.Equal(entry.Kind, start.MenuMonitorWait!.Kind);
            TestAssert.True(before.Rows.SequenceEqual(start.Rows) && start.MenuOptionCallback is null,
                "An unsupported source branch cannot fabricate a value or dispatch a callback");
            TestAssert.Equal(0, rig.Session.Executed.Count); rig.Clock.Advance(TimeSpan.FromMilliseconds(entry.Kind == "choice" ? 180 : 50));
            TestAssert.True(rig.Controller.Snapshot().MenuMonitorWait is null, "Each declaration owns only its finite mechanical wait");
        }
        var unknown = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Main);
        TestAssert.Equal(MenuSourceChoiceRole.Unknown, unknown.Navigation.Rows[0].SourceChoiceRole);
        unknown.Send(OverlayInput.NextChoice); TestAssert.True(unknown.Controller.Snapshot().MenuMonitorWait is null,
            "The local Connect extension cannot pretend to be a source non-slider declaration");
    }

    public static void SourceChoiceRoleIsPrivateAndChangesRetireTheCapturedRowTopology() {
        var row = new MenuRow("Observed option", "OFF", true, new(MenuActionKind.ChangeAccent, Slot: 0)) { SourceChoiceRole = MenuSourceChoiceRole.NonSlider };
        var navigation = new OverlayNavigation(); navigation.SetRows(new[] { row }); navigation.Open();
        var changed = row with { SourceChoiceRole = MenuSourceChoiceRole.Slider };
        TestAssert.True(!navigation.RowsMatchTopology(new[] { changed }), "A source choice-role replacement cannot inherit causal Use history");
        var paint = Apparition.Backend.Controller.MenuBooleanPaintCache.Capture(1, new[] { row }, includePalette: true)!;
        TestAssert.True(!paint.MatchesTopology(new[] { changed }), "Cached source indicators cannot cross a declaration-role change");
        var prior = navigation.MenuRevision; var priorNavigation = navigation.Revision;
        navigation.SetRows(new[] { changed }); TestAssert.Equal(prior + 1, navigation.MenuRevision);
        TestAssert.Equal(priorNavigation, navigation.Revision);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuSourceChoiceRoles.ForBasicScripts(-1));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuSourceChoiceRoles.ForBasicScripts(31));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuSourceChoiceRoles.ForQuickOption((QuickMenuOptionId)999));
        TestAssert.Equal(MenuSourceChoiceRole.Unknown, MenuSourceChoiceRoles.ForPageRow(MenuPage.Preferences, 12, new(MenuActionKind.None)));
        TestAssert.Equal(MenuSourceChoiceRole.Unknown, MenuSourceChoiceRoles.ForPageRow(MenuPage.Preferences, 0,
            new(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.QuickExit)));
        TestAssert.Equal(MenuSourceChoiceRole.Unknown, MenuSourceChoiceRoles.ForPageRow(MenuPage.ThemeColors, 0,
            new(MenuActionKind.ChangeAccent, Slot: 999)));
        TestAssert.Equal(MenuSourceChoiceRole.Unknown, MenuSourceChoiceRoles.ForPageRow(MenuPage.Main, 0, new(MenuActionKind.Connect)));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(row));
        TestAssert.True(!document.RootElement.TryGetProperty("SourceChoiceRole", out _) && !document.RootElement.TryGetProperty("sourceChoiceRole", out _),
            "Source role metadata stays private when a model is serialized");
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors);
        rig.Send(OverlayInput.Select); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is not null,
            "The original observed row has a normally retired callback");
        rig.Navigation.SetRows(rig.Navigation.Rows.Select((item, index) => index == rig.Navigation.SelectedIndex
            ? item with { SourceChoiceRole = MenuSourceChoiceRole.Slider } : item).ToArray());
        TestAssert.True(rig.Controller.ReadMenuPublicationPhase()?.Contains("use:", StringComparison.Ordinal) != true,
            "A changed private source branch cannot remain a ready continuation");
    }
}
