using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// Independent literal inventory follows pinned Functions/player.gsc and allplayers.gsc
internal static class SourcePlayerInventoryTests {
    internal static readonly string[] Options = { "Verification", "Basic Scripts", "Teleport Menu",
        "Profile Management", "Weaponry", "Bullet Menu", "Fun Scripts", "Model Manipulation",
        "Aimbot Menu", "Model Attachment", "Malicious Options", "Send Message", "Freeze", "Kick" };
    internal static readonly string[] AllPlayers = { "Verification", "Teleport", "Profile Management",
        "Model Manipulation", "Malicious Options", "God Mode", "Send Message", "Kick", "Down", "Revive", "Respawn" };
    internal static OverlayMenuState State(PlayerMenuRoute route = PlayerMenuRoute.Teammate) {
        var identity = PlayerMenuTargetContractTests.Identity;
        return new SessionBoundary().State with { Connected = true, CanTeammateGodmode = true,
            CanPersonalGodmode = true, PersonalGodmodeValue = "OFF", TeammateGodmodeValue = "ON",
            SelectedSlot = identity.Slot, SelectedName = identity.Name,
            PersonalGodmodeIdentity = route == PlayerMenuRoute.Personal ? identity : null,
            PersonalGodmodeRequiresVerifiedTarget = true,
            SelectedPlayerTarget = new(identity, route, new object(), 1) };
    }

    public static void EveryCapturedActorHasTheFourteenSourceOptionsInOrder() {
        foreach (var route in new[] { PlayerMenuRoute.Personal, PlayerMenuRoute.Teammate }) {
            var state = State(route); var page = OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, state);
            TestAssert.Equal(14, page.Rows.Count);
            for (int i = 0; i < Options.Length; ++i) TestAssert.Equal(Options[i], page.Rows[i].Label);
            var basic = page.Rows[1]; TestAssert.True(basic.Enabled, "A captured actor can browse its Basic Scripts");
            TestAssert.Equal(MenuPage.SelectedBasicScripts, basic.Action.Page);
            TestAssert.True(basic.Action.ExpectedTarget!.Equals(state.SelectedPlayerTarget), "The link retains the full captured target");
            TestAssert.True(page.Rows.Where((_, index) => index != 1).All(row => !row.Enabled
                && row.Value == "UNAVAILABLE" && row.Action.Kind == MenuActionKind.None), "Unsupported callbacks stay inert");
        }
    }

    public static void AllPlayersKeepsElevenSourceRowsBeforeTheClearlyNamedIwExtension() {
        var page = OverlayMenuBuilder.Build(MenuPage.AllPlayers, State());
        TestAssert.Equal(12, page.Rows.Count);
        for (int i = 0; i < AllPlayers.Length; ++i) {
            TestAssert.Equal(AllPlayers[i], page.Rows[i].Label);
            TestAssert.True(!page.Rows[i].Enabled && page.Rows[i].Action.Kind == MenuActionKind.None,
                "Unverified group callbacks cannot gain support from inventory visibility");
        }
        TestAssert.Equal("Infinite Ammo (IW lobby)", page.Rows[11].Label);
        TestAssert.Equal(MenuActionKind.ToggleLobbyAmmo, page.Rows[11].Action.Kind);
    }
}
