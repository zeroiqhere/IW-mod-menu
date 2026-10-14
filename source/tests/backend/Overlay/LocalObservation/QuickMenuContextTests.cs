using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class QuickMenuContextTests {
    public static void OptionalQuickContextUsesVerifiedDeclarationsAndLegacyMissingContextStaysUnknown() {
        var observation = MenuObservationFixture.Observed();
        var context = Context(observation);
        var supplied = OverlayMenuBuilder.Build(MenuPage.QuickMenu, MenuObservationFixture.State(), quickContext: context);
        var legacy = OverlayMenuBuilder.Build(MenuPage.QuickMenu, MenuObservationFixture.State());

        TestAssert.Equal(14, supplied.Rows.Count);
        TestAssert.Equal("God Mode", supplied.Rows[0].Label);
        TestAssert.True(!supplied.Notice.Contains("opening is unavailable", StringComparison.OrdinalIgnoreCase),
            "A verified view must not retain the old unconditional unavailable status");
        TestAssert.Equal(1, legacy.Rows.Count);
        TestAssert.Equal("Respawn", legacy.Rows[0].Label);
    }

    public static void LockedOrUnknownControlsRetainTheKnownCatalogButCannotEnableItsAction() {
        foreach (bool? disabled in new bool?[] { true, null }) {
            var observation = MenuObservationFixture.Observed() with { ControlsDisabled = disabled };
            var rows = QuickMenuRows.Build(MenuObservationFixture.State(), new(QuickPlayerLife.Living, true, null),
                observation, MenuObservationFixture.Local, 51);

            TestAssert.Equal(14, rows.Count);
            TestAssert.Equal("God Mode", rows[0].Label);
            TestAssert.True(!rows[0].Enabled, "A declared living row does not bypass an unavailable monitor");
        }
    }

    public static void InvalidContextCannotAuthenticateAPlayerThroughItsSelectedIdentity() {
        var observation = MenuObservationFixture.Observed();
        var contexts = new[] {
            Context(observation) with { ExpectedProcessId = 52 },
            Context(observation) with { SelectedLocalIdentity = MenuObservationFixture.Local with { Client = 0x60000 } },
            Context(observation with { HasMenu = false }),
            Context(observation with { Life = LocalPlayerLife.Unknown })
        };
        foreach (var context in contexts) {
            var frame = OverlayMenuBuilder.Build(MenuPage.QuickMenu, MenuObservationFixture.State(), quickContext: context);
            TestAssert.Equal(1, frame.Rows.Count);
            TestAssert.True(frame.Rows.All(row => !row.Enabled), "Unknown context never enables a source callback");
        }
    }

    public static void LockedViewCannotBorrowLobbyAmmoOrUnsupportedAuthorityCallbacks() {
        var observation = MenuObservationFixture.Observed() with { ControlsDisabled = true };
        var state = MenuObservationFixture.State() with { CanLobbyAmmo = true, LobbyAmmoValue = "ON" };
        var frame = OverlayMenuBuilder.Build(MenuPage.QuickMenu, state, quickContext: Context(observation));

        TestAssert.Equal(14, frame.Rows.Count);
        TestAssert.True(frame.Rows.All(row => !row.Enabled), "Every gameplay action stays disabled while monitor controls are locked");
        var ammo = frame.Rows.Single(row => row.Label == "Unlimited Ammo");
        TestAssert.Equal("UNAVAILABLE", ammo.Value);
        TestAssert.Equal(MenuActionKind.None, ammo.Action.Kind);
        TestAssert.True(ammo.Detail.Contains("personal", StringComparison.OrdinalIgnoreCase)
            && ammo.Detail.Contains("lobby", StringComparison.OrdinalIgnoreCase),
            "Personal ammo remains distinct from the supported whole-lobby operation");
    }

    public static void VerifiedNonzeroLocalSlotsKeepCatalogButCannotBorrowPlayerOneGodmode() {
        foreach (int slot in new[] { 1, 2, 3 }) {
            var identity = MenuObservationFixture.Local with {
                Slot = slot,
                Entity = MenuObservationFixture.Local.Entity + slot * 0x3F8,
                Client = MenuObservationFixture.Local.Client + slot * 0x10000,
                ServerClient = MenuObservationFixture.Local.ServerClient + slot * 0x10000
            };
            var observation = MenuObservationFixture.Observed() with { Identity = identity };
            var frame = OverlayMenuBuilder.Build(MenuPage.QuickMenu, MenuObservationFixture.State(),
                quickContext: Context(observation));

            TestAssert.Equal(14, frame.Rows.Count);
            TestAssert.Equal("God Mode", frame.Rows[0].Label);
            TestAssert.Equal("UNAVAILABLE", frame.Rows[0].Value);
            TestAssert.True(!frame.Rows[0].Enabled, "The current personal adapter cannot target a different verified local slot");
            TestAssert.Equal(MenuActionKind.None, frame.Rows[0].Action.Kind);
            TestAssert.True(frame.Rows[0].Detail.Contains("this player", StringComparison.OrdinalIgnoreCase),
                "The unsupported player target needs honest personal help");
        }
        // The controller's fresh action path must preserve the same target limitation
        var fixture = new MenuObservationFixture();
        fixture.Session.Observation = MenuObservationFixture.Observed() with {
            Identity = MenuObservationFixture.Local with { Slot = 2 }
        };
        fixture.Controller.Snapshot();
        fixture.Send(OverlayInput.Select);
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(0, fixture.Session.Executed.Count);
    }

    private static QuickMenuViewContext Context(LocalPlayerObservation observation) =>
        new(observation, new(QuickPlayerLife.Living, true, null), observation.Identity, 51);
}
