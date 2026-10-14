using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuBuilderTests {
    public static void RootTitleMatchesApparitionAndRetainsClearFeatureDestinations() {
        var states = new[] {
            new SessionBoundary().State,
            new SessionBoundary().State with { Connected = true },
            new SessionBoundary().State with { Preview = true },
            new SessionBoundary().State with { Preview = true, Connected = true }
        };
        (string Label, MenuAction Action)[] expectedRows = {
            ("Connect after spawning", new(MenuActionKind.Connect)),
            ("Basic Scripts", new(MenuActionKind.OpenPage, MenuPage.Personal)),
            ("Menu Customization", new(MenuActionKind.OpenPage, MenuPage.Customization)),
            ("Players Menu", new(MenuActionKind.OpenPage, MenuPage.Players)),
            ("All Players Menu", new(MenuActionKind.OpenPage, MenuPage.AllPlayers)),
            ("Messages", new(MenuActionKind.OpenPage, MenuPage.History)),
            ("Feature Support", new(MenuActionKind.OpenPage, MenuPage.Support)),
            ("Exit", new(MenuActionKind.Exit))
        };
        MenuRow[]? extensionRows = null;
        foreach (var state in states) {
            var menu = OverlayMenuBuilder.Build(MenuPage.Main, state);
            TestAssert.Equal("Apparition", menu.Title);
            TestAssert.True(menu.Rows.Select(row => row.Label).SequenceEqual(expectedRows.Select(row => row.Label)),
                "The four Main source destinations must retain their pinned order before the IW extension tail");
            TestAssert.True(menu.Rows.Select(row => row.Action).SequenceEqual(expectedRows.Select(row => row.Action)),
                "Each Main label must retain its existing destination and action");
            // Entering a page does not grant its gameplay actions
            TestAssert.True(menu.Rows.Skip(1).Take(4).All(row => row.Enabled && row.Value == ">"),
                "The represented navigation rows remain available in disconnected, connected, and preview visits");

            var connect = menu.Rows[0];
            TestAssert.Equal(state.Preview ? "UNAVAILABLE" : state.Connected ? "CONNECTED" : "", connect.Value);
            TestAssert.Equal(!state.Preview && !state.Connected && state.CanConnect, connect.Enabled);

            var currentExtensions = menu.Rows.Skip(5).ToArray();
            if (extensionRows is null) extensionRows = currentExtensions;
            else TestAssert.True(currentExtensions.SequenceEqual(extensionRows),
                "Messages, Feature Support, and Exit keep their row routing and availability in every session state");
        }
    }

    public static void DisconnectedGuidanceRemainsActionableAcrossFeaturePages() {
        var state = new SessionBoundary().State;
        foreach (MenuPage page in new[] { MenuPage.Main, MenuPage.Personal, MenuPage.Players,
            MenuPage.SelectedPlayer, MenuPage.AllPlayers }) {
            var menu = OverlayMenuBuilder.Build(page, state);
            TestAssert.True(menu.Notice.Contains("Connect after spawning", StringComparison.Ordinal),
                "Every disconnected feature page must name the available connection option");
            TestAssert.True(menu.Notice.Contains("Apparition", StringComparison.Ordinal),
                "A child page must explain where connection is available");
        }
        var lobby = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state);
        TestAssert.True(lobby.Notice.Contains("everyone in the lobby", StringComparison.Ordinal),
            "Connection guidance must retain the shared-ammo warning");
    }

    public static void BlockedConnectionUsesMessagesGuidanceWithoutPromisingAnAvailableConnectAction() {
        var menu = OverlayMenuBuilder.Build(MenuPage.Personal, new SessionBoundary().State with { CanConnect = false });
        TestAssert.True(menu.Notice.Contains("Messages", StringComparison.Ordinal),
            "A blocked connection must point to the current session status");
        TestAssert.True(!menu.Notice.Contains("select Connect", StringComparison.OrdinalIgnoreCase),
            "Unavailable connection cannot be presented as an executable next step");
    }

    public static void ConnectedConnectRowShowsConfirmationAndRejectsStaleReconnectCapability() {
        var state = new SessionBoundary().State with { Connected = true, CanConnect = true };
        var menu = OverlayMenuBuilder.Build(MenuPage.Main, state);
        var connect = menu.Rows[0];
        TestAssert.Equal("CONNECTED", connect.Value);
        TestAssert.True(!connect.Enabled, "A confirmed connection cannot request another Connect");
        TestAssert.True(connect.Detail.Contains("Connected", StringComparison.Ordinal), "The selected connection row must confirm the current state");
        TestAssert.True(!connect.Detail.Contains("Spawn", StringComparison.OrdinalIgnoreCase), "A connected row must not keep a stale spawn instruction");
        TestAssert.True(!menu.Notice.Contains("select Connect", StringComparison.OrdinalIgnoreCase), "Connected pages must clear disconnected guidance");
    }

    public static void PreviewConnectRowCannotClaimARealConnection() {
        var menu = OverlayMenuBuilder.Build(MenuPage.Main,
            new SessionBoundary().State with { Preview = true, Connected = true });
        TestAssert.True(!menu.Rows[0].Enabled, "Preview cannot connect");
        TestAssert.True(menu.Rows[0].Value != "CONNECTED", "Stale preview metadata cannot confirm a live connection");
        TestAssert.True(menu.Rows[0].Detail.Contains("Preview", StringComparison.Ordinal), "The selected row must explain its preview state");
    }

    public static void DisconnectedPreviewAndBusyFeatureValuesRemainUnknown() {
        var stale = new SessionBoundary().State with {
            CanPersonalGodmode = true, CanLobbyAmmo = true, CanTeammateGodmode = true,
            PersonalGodmodeValue = "ON", LobbyAmmoValue = "ON", TeammateGodmodeValue = "ON",
            SelectedSlot = 1, SelectedName = "Friend"
        };
        foreach (var state in new[] { stale, stale with { Connected = true, Preview = true },
            stale with { Connected = true, Busy = true } }) {
            foreach (MenuPage page in new[] { MenuPage.Personal, MenuPage.AllPlayers, MenuPage.SelectedBasicScripts }) {
                var row = OverlayMenuBuilder.Build(page, state).Rows[page == MenuPage.AllPlayers ? 11 : 0];
                TestAssert.Equal("UNKNOWN", row.Value);
                TestAssert.True(!row.Enabled, "Unknown unavailable feature state cannot permit an action");
            }
        }
    }

    public static void CapabilityDisabledConnectedFeaturePreservesItsConfirmedValue() {
        var state = new SessionBoundary().State with { Connected = true, CanPersonalGodmode = false,
            PersonalGodmodeValue = "ON" };
        var row = OverlayMenuBuilder.Build(MenuPage.Personal, state).Rows[0];
        TestAssert.True(!row.Enabled, "Capability remains the action guard");
        TestAssert.Equal("ON", row.Value);
    }
    public static void AllPlayerGodmodeRemainsUnavailableWithoutAnAllPlayerBindingAndCannotMirrorPersonalOn() {
        var initial = new SessionBoundary().State;
        foreach (var state in new[] { initial, initial with { Connected = true, CanPersonalGodmode = true,
            PersonalGodmodeValue = "ON", CanTeammateGodmode = true, TeammateGodmodeValue = "ON",
            SelectedSlot = 1, SelectedName = "Friend" }, initial with { Preview = true, Connected = true },
            initial with { Busy = true, Connected = true } }) {
            var menu = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state);
            var row = menu.Rows.Single(row => row.Label == "God Mode");
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.Equal(MenuActionKind.None, row.Action.Kind);
            TestAssert.True(!row.Enabled, "Unproved all-player scope cannot dispatch a gameplay action");
            TestAssert.True(row.Detail.Contains("Players Menu", StringComparison.Ordinal),
                "The unavailable row must point to the existing individual teammate controls");
            TestAssert.Equal(MenuActionKind.ToggleLobbyAmmo, menu.Rows[11].Action.Kind);
        }
    }

}
