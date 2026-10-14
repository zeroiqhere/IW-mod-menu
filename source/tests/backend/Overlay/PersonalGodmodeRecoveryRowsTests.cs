using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// View tests distinguish an owned OFF retry from a fresh toggle permission
internal static class PersonalGodmodeRecoveryRowsTests {
    private static readonly PlayerIdentity Actor = new(2, 0x10000, 0x20000, 0x30000,
        4, "Local player", "1234567890ABCDEF");
    private static OverlayMenuState State() => new(false, true, false, false, false,
        "UNKNOWN", "The owned change could not be restored", false, "UNKNOWN", "", false,
        true, new[] { new OverlayPlayer(0, "Friend"), new OverlayPlayer(2, Actor.Name) },
        -1, null, false, "UNKNOWN", "", false, "") {
        PersonalGodmodeIdentity = Actor, PersonalGodmodeRequiresVerifiedTarget = true,
        CanRestorePersonalGodmode = true
    };

    public static void UnconfirmedOwnedChangeOffersOnlyAnExplicitOffRetry() {
        var row = BasicScriptsMenuRows.PersonalGodmode(State());
        TestAssert.True(row.Enabled, "An original owned change must offer its OFF recovery");
        TestAssert.Equal("UNKNOWN", row.Value);
        TestAssert.Equal(MenuActionKind.RestorePersonalGodmode, row.Action.Kind);
        TestAssert.True(row.Detail.Contains("OFF", StringComparison.Ordinal),
            "Recovery help must explain that selection cannot enable Godmode");
        // Ordinary confirmed ON remains the normal toggle when its capability is valid
        row = BasicScriptsMenuRows.PersonalGodmode(State() with { CanPersonalGodmode = true, PersonalGodmodeValue = "ON" });
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, row.Action.Kind);
    }

    public static void OffRecoveryStillHonorsPreviewConnectionAndBusyGuards() {
        foreach (var state in new[] { State() with { Preview = true }, State() with { Connected = false }, State() with { Busy = true } }) {
            var row = BasicScriptsMenuRows.PersonalGodmode(state);
            TestAssert.True(!row.Enabled, "Recovery cannot bypass the ordinary session guards");
            TestAssert.Equal("UNKNOWN", row.Value);
        }
    }

    public static void ProductionPlayerLinksNeverChooseAnOwnerFromSlotOrFlagCapabilities() {
        var rows = OverlayMenuBuilder.Build(MenuPage.Players, State()).Rows;
        var self = rows.Single(row => row.Action.Slot == 2);
        var other = rows.Single(row => row.Action.Slot == 0);
        TestAssert.Equal(MenuPage.SelectedPlayer, self.Action.Page);
        TestAssert.Equal(MenuPage.SelectedPlayer, other.Action.Page);
        TestAssert.True(other.Enabled, "A verified foreign slot zero must be selectable without using personal controls");
        var unavailable = OverlayMenuBuilder.Build(MenuPage.Players, State() with { PersonalGodmodeIdentity = null });
        TestAssert.True(unavailable.Rows.Where(row => row.Action.Kind == MenuActionKind.SelectPlayer)
            .All(row => row.Action.Page != MenuPage.Personal), "An unknown local target cannot use the legacy slot-zero assumption");
    }

    public static void QuickProductionCaptureFailureCannotBorrowLegacySlotZero() {
        var zero = Actor with { Slot = 0 };
        var observation = new LocalPlayerObservation(4242, zero, LocalPlayerLife.Alive, true, false, "Alive", false);
        var rows = QuickMenuRows.Build(State() with { PersonalGodmodeIdentity = null },
            new(QuickPlayerLife.Living, false, false), observation, zero, 4242);
        var row = rows.Single(row => row.Label == "God Mode");
        TestAssert.Equal("UNAVAILABLE", row.Value);
        TestAssert.True(!row.Enabled && row.Action.Kind == MenuActionKind.None,
            "A failed production capture cannot advertise an OFF recovery for an unverified player");
    }
}
