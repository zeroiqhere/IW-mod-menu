using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// The shared source inventory changes only its one supported actor-bound leaf
internal static class SelectedBasicScriptsTests {
    public static void CapturedPersonalAndPeerLeavesKeepFullProofWithoutBorrowingOtherCapabilities() {
        foreach (var route in new[] { PlayerMenuRoute.Personal, PlayerMenuRoute.Teammate }) {
            var state = SourcePlayerInventoryTests.State(route);
            var rows = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state).Rows;
            TestAssert.Equal(31, rows.Count);
            TestAssert.True(rows.Select(row => row.Label).SequenceEqual(BasicScriptsMenuRows.Build(state).Select(row => row.Label)),
                "Both actor views use the same thirty-one source declarations");
            TestAssert.Equal(route == PlayerMenuRoute.Personal ? MenuActionKind.TogglePersonalGodmode
                : MenuActionKind.ToggleTeammateGodmode, rows[0].Action.Kind);
            TestAssert.Equal(MenuPage.SelectedBasicScripts, rows[0].Action.Page);
            TestAssert.True(rows[0].Action.ExpectedTarget!.Equals(state.SelectedPlayerTarget), "The leaf owns the captured target");
            TestAssert.True(rows.Skip(1).All(row => row.Action.Kind == MenuActionKind.None && !row.Enabled), "Unsupported callbacks stay inert");
        }
        var personal = SourcePlayerInventoryTests.State(PlayerMenuRoute.Personal);
        foreach (var identity in new[] { null, personal.PersonalGodmodeIdentity! with { Guid = "FFFFFFFFFFFFFFFF" } }) {
            var metadataOnly = personal with { PersonalGodmodeIdentity = identity, CanRestorePersonalGodmode = true };
            TestAssert.True(OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, metadataOnly).Rows[1].Enabled,
                "A proved browsing receipt does not require a flags writer");
            var row = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, metadataOnly).Rows[0];
            TestAssert.True(!row.Enabled && row.Value == "UNKNOWN" && row.Action.Kind == MenuActionKind.None,
                "An old personal restore capability cannot serve a different full target");
        }
        var recovery = personal with { CanPersonalGodmode = false, CanRestorePersonalGodmode = true, PersonalGodmodeValue = "UNKNOWN" };
        TestAssert.Equal(MenuActionKind.RestorePersonalGodmode,
            OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, recovery).Rows[0].Action.Kind);
        var peer = SourcePlayerInventoryTests.State() with { CanSelectPlayers = false, CanTeammateGodmode = true };
        TestAssert.True(OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, peer).Rows[1].Enabled,
            "Retained cleanup browsing stays independent of strict new-selection admission");
    }

    public static void MissingOrMalformedTargetKeepsOptionsInventoryAndCannotExposeAMutation() {
        var state = SourcePlayerInventoryTests.State();
        foreach (var invalid in new[] { state with { SelectedPlayerTarget = null }, state with { SelectedSlot = 0 },
            state with { SelectedName = "Replacement" }, state with { SelectedPlayerTarget = new(
                state.SelectedPlayerTarget!.Identity with { Slot = 4 }, PlayerMenuRoute.Teammate, new object(), 1) } }) {
            var options = OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, invalid);
            TestAssert.Equal(14, options.Rows.Count);
            TestAssert.True(options.Rows.All(row => !row.Enabled && row.Action.Kind == MenuActionKind.None),
                "Missing captured authority cannot invent the old one-row shortcut");
            var row = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, invalid).Rows[0];
            TestAssert.True(!row.Enabled && row.Action.Kind == MenuActionKind.None, "Malformed view context cannot authorize mutation");
        }
    }

    public static void SourceBasicCursorIsSharedWhilePeerPersonalAndQuickActionsRemainDistinct() {
        var state = SourcePlayerInventoryTests.State(); var navigation = new OverlayNavigation();
        navigation.Open(); navigation.OpenPage(MenuPage.SelectedPlayer);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, state).Rows);
        navigation.SelectIndex(1); navigation.OpenPage(MenuPage.SelectedBasicScripts);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state).Rows); navigation.SelectIndex(30);
        navigation.Handle(OverlayInput.Back); navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, state).Rows);
        TestAssert.Equal(1, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Back); navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(BasicScriptsMenuRows.Build(state)); TestAssert.Equal(30, navigation.SelectedIndex);
        navigation.SelectIndex(0); TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, navigation.Handle(OverlayInput.Select)!.Kind);
        navigation.Handle(OverlayInput.Back); navigation.OpenPage(MenuPage.SelectedBasicScripts);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state).Rows);
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.Equal(MenuActionKind.ToggleTeammateGodmode, navigation.Handle(OverlayInput.Select)!.Kind);
        // A shortened snapshot clamps the shared cursor before any later action can use it
        navigation.SelectIndex(30); navigation.SetRows(new[] { new MenuRow("Only", "", false, new(MenuActionKind.None)) });
        navigation.Handle(OverlayInput.Back); navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(BasicScriptsMenuRows.Build(state)); TestAssert.Equal(0, navigation.SelectedIndex);
        var exhausted = new OverlayNavigation(ulong.MaxValue, 1);
        TestAssert.Throws<OverflowException>(() => exhausted.OpenPage(MenuPage.SelectedBasicScripts));
        TestAssert.Equal(MenuPage.Main, exhausted.Page);
    }
}
