using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// A supplied feature target is view data; the action owner still revalidates it before writing
internal static class QuickPersonalTargetTests {
    public static void AVerifiedFeatureTargetCanDisplayTheCurrentLocalPlayerInEverySlot() {
        foreach (int slot in new[] { 0, 1, 2, 3 }) {
            var identity = MenuObservationFixture.Local with {
                Slot = slot, Entity = 0x10000 + slot * 0x3F8,
                Client = 0x20000 + slot * 0x10000, ServerClient = 0x30000 + slot * 715416L
            };
            var observation = MenuObservationFixture.Observed() with { Identity = identity };
            var state = MenuObservationFixture.State() with { PersonalGodmodeIdentity = identity };
            var row = QuickMenuRows.Build(state, new(Apparition.Backend.QuickMenu.QuickPlayerLife.Living, true, null),
                observation, identity, MenuObservationFixture.ProcessId)[0];
            TestAssert.Equal("ON", row.Value);
            TestAssert.True(row.Enabled, "A matched local feature target cannot be restricted to Player 1");
            TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, row.Action.Kind);
        }
    }

    public static void ADifferentFeatureTargetCannotBorrowItsValueOrActionEvenForSlotZero() {
        var observation = MenuObservationFixture.Observed();
        foreach (var other in new[] {
            MenuObservationFixture.Local with { Slot = 1 },
            MenuObservationFixture.Local with { Entity = 0x60000 },
            MenuObservationFixture.Local with { Guid = "FFFFFFFFFFFFFFFF" }
        }) {
            var state = MenuObservationFixture.State() with { PersonalGodmodeIdentity = other };
            var row = QuickMenuRows.Build(state, new(Apparition.Backend.QuickMenu.QuickPlayerLife.Living, true, null),
                observation, observation.Identity, MenuObservationFixture.ProcessId)[0];
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.True(!row.Enabled, "A feature owned by another identity cannot enable this local row");
            TestAssert.Equal(MenuActionKind.None, row.Action.Kind);
        }
    }

    public static void FreshControllerSelectionRechecksThePersonalFeatureTargetBeforeDispatch() {
        var fixture = new MenuObservationFixture();
        var local = MenuObservationFixture.Local with { Slot = 2 };
        fixture.Session.Observation = MenuObservationFixture.Observed() with { Identity = local };
        fixture.Session.MenuState = MenuObservationFixture.State() with { PersonalGodmodeIdentity = local };
        TestAssert.True(fixture.Controller.Snapshot().Rows[0].Enabled, "The initial view must use the matched feature owner");
        // The same visible player can have a different current feature target before selection executes
        fixture.Session.MenuState = fixture.Session.MenuState with { PersonalGodmodeIdentity = MenuObservationFixture.Local };
        fixture.Send(OverlayInput.Select);
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(0, fixture.Session.Executed.Count);
    }

    public static void AMatchedNonzeroLocalControllerDispatchesOnlyItsPersonalGodmodeIntent() {
        var fixture = new MenuObservationFixture();
        var local = MenuObservationFixture.Local with { Slot = 3 };
        fixture.Session.Observation = MenuObservationFixture.Observed() with { Identity = local };
        fixture.Session.MenuState = MenuObservationFixture.State() with { PersonalGodmodeIdentity = local };
        fixture.Controller.Snapshot();
        fixture.Send(OverlayInput.Select);
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(1, fixture.Session.Executed.Count);
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, fixture.Session.Executed[0].Kind);
        TestAssert.True(fixture.Session.Executed.All(action => action.Kind != MenuActionKind.ToggleTeammateGodmode),
            "A selected local feature must never dispatch a teammate operation");
    }
}
