using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// Roster position never identifies the authenticated personal writer
internal static class PlayerSlotRoutingRowsTests {
    private static readonly PlayerIdentity Local = new(2, 0x10000, 0x20000, 0x30000,
        4, "Player2", "1234567890ABCDEF");
    private static OverlayMenuState State() => new(false, true, false, false,
        true, "OFF", "", false, "OFF", "", true, true,
        Enumerable.Range(0, 4).Select(slot => new OverlayPlayer(slot, "Player" + slot)).ToArray(),
        0, "Player0", true, "OFF", "Captured teammate", false, "") {
        PersonalGodmodeIdentity = Local, PersonalGodmodeRequiresVerifiedTarget = true,
        SelectedPlayerTarget = new(Local with { Slot = 0, Name = "Player0" }, PlayerMenuRoute.Teammate, new object(), 1)
    };

    public static void EveryVerifiedRosterSlotUsesCapturedSourceOptionsWithoutChoosingAnOwner() {
        foreach (int localSlot in Enumerable.Range(0, 4)) {
            var local = Local with { Slot = localSlot, Name = "Player" + localSlot };
            var rows = OverlayMenuBuilder.Build(MenuPage.Players, State() with { PersonalGodmodeIdentity = local }).Rows;
            foreach (var row in rows.Where(row => row.Action.Kind == MenuActionKind.SelectPlayer)) {
                TestAssert.True(row.Enabled, "Every validated roster slot must be selectable");
                TestAssert.Equal(MenuPage.SelectedPlayer, row.Action.Page);
                TestAssert.Equal("Player" + row.Action.Slot, row.Action.ExpectedName);
                TestAssert.Equal("Player" + row.Action.Slot, row.Label);
            }
        }
    }

    public static void UnverifiedPersonalIdentityCannotRedirectSlotZeroToPersonal() {
        foreach (var local in new PlayerIdentity?[] { null, Local with { Slot = 0, Name = "Different actor" } }) {
            var row = OverlayMenuBuilder.Build(MenuPage.Players, State() with { PersonalGodmodeIdentity = local })
                .Rows.Single(row => row.Action.Slot == 0);
            TestAssert.Equal(MenuPage.SelectedPlayer, row.Action.Page);
            TestAssert.True(row.Enabled, "The roster link must retain selection for strict backend validation");
            TestAssert.Equal("Player0", row.Action.ExpectedName);
        }
    }

    public static void SelectedSlotZeroToggleCarriesTheCapturedNameAndConfirmedValue() {
        foreach (string value in new[] { "OFF", "ON", "UNKNOWN" }) {
            var page = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, State() with { TeammateGodmodeValue = value });
            var row = page.Rows[0];
            TestAssert.True(row.Enabled, "The slot-zero row must honor its verified backend capability");
            TestAssert.Equal(value, row.Value);
            TestAssert.Equal(MenuActionKind.ToggleTeammateGodmode, row.Action.Kind);
            TestAssert.Equal(0, row.Action.Slot);
            TestAssert.Equal("Player0", row.Action.ExpectedName);
            TestAssert.True(page.Title.Contains("Basic Scripts", StringComparison.Ordinal), "Selected actor must appear in context");
        }
    }

    public static void SlotZeroSelectionAndToggleRetainConnectionPreviewBusyAndCapabilityGuards() {
        var states = new[] { State() with { Preview = true }, State() with { Connected = false },
            State() with { Busy = true }, State() with { CanSelectPlayers = false, CanTeammateGodmode = false } };
        foreach (var state in states) {
            var selected = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state).Rows[0];
            TestAssert.True(!selected.Enabled, "Roster position cannot bypass a feature guard");
            var players = OverlayMenuBuilder.Build(MenuPage.Players, state).Rows;
            TestAssert.True(players.Where(row => row.Action.Kind == MenuActionKind.SelectPlayer).All(row => !row.Enabled),
                "An unavailable session cannot send any roster selection");
        }
    }

    public static void MalformedSelectedPayloadCannotAuthorizeAnyTeammateToggle() {
        foreach (var state in new[] { State() with { SelectedSlot = -1 }, State() with { SelectedSlot = 4 },
            State() with { SelectedName = null }, State() with { SelectedName = " " } }) {
            var page = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state);
            TestAssert.True(!page.Rows[0].Enabled, "An invalid slot or name cannot authorize a toggle");
            TestAssert.True(page.Rows[0].Detail.Contains("captured player", StringComparison.Ordinal), "Invalid selection needs actionable guidance");
        }
    }

    public static void SlotZeroCleanupRowPreservesUnknownAndAnExplicitBackendOffCapability() {
        var state = State() with { CanSelectPlayers = false, CanTeammateGodmode = true,
            TeammateGodmodeValue = "UNKNOWN", TeammateGodmodeStatus = "Turn off the original selection" };
        var row = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, state).Rows[0];
        TestAssert.True(row.Enabled && row.Detail.Contains("original selection", StringComparison.Ordinal),
            "The owned recovery target must remain inspectable and actionable");
        TestAssert.Equal("UNKNOWN", row.Value);
        TestAssert.True(OverlayMenuBuilder.Build(MenuPage.Players, state).Rows
            .Where(item => item.Action.Kind == MenuActionKind.SelectPlayer).All(item => !item.Enabled),
            "Pending cleanup cannot open a replacement player selection");
    }
}
