using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// Real controller admission and completion are exercised through a controlled session boundary
internal static class PlayerMenuControllerTests {
    private sealed class Fixture {
        public readonly SessionBoundary Session = new();
        public readonly ManualTimeProvider Clock = new();
        public readonly BackendController Controller;
        public bool Focus = true;
        private ulong sequence;
        public Fixture() {
            Session.State = SourcePlayerInventoryTests.State() with { CanSelectPlayers = true, CanRefreshPlayers = true,
                Players = new[] { new OverlayPlayer(2, PlayerMenuTargetContractTests.Identity.Name) }, SelectedPlayerTarget = null };
            Controller = new(42, Session, clock: Clock);
            Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, Clock);
            Send(OverlayInput.Down); Send(OverlayInput.Down); Send(OverlayInput.Down);
            Send(OverlayInput.Select); Send(OverlayInput.Down);
        }
        public void Send(OverlayInput input) => Controller.Consume(new(++sequence, 1, 42, input, true), () => Focus);
        public void Select(bool held = false) {
            if (held) Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Send(OverlayInput.Select); Controller.StartQueuedAction();
        }
        public void Basic() { Clock.Advance(TimeSpan.FromMilliseconds(250)); Controller.Snapshot(); Send(OverlayInput.Down); Send(OverlayInput.Select); }
    }

    public static void RejectedSelectionAndExhaustedEpochNeverNavigateToAnOlderSameNamedTarget() {
        var fixture = new Fixture();
        fixture.Session.SelectionEpoch = 1;
        var scope = fixture.Session.ReadPlayerSelectionScope()!;
        var prior = new PlayerMenuTarget(PlayerMenuTargetContractTests.Identity, PlayerMenuRoute.Teammate,
            scope.OwnerToken, scope.Revision);
        fixture.Session.State = fixture.Session.State with { SelectedSlot = prior.Identity.Slot,
            SelectedName = prior.Identity.Name, SelectedPlayerTarget = prior };
        fixture.Session.CaptureSelection = false; fixture.Select();
        TestAssert.Equal("Players Menu", fixture.Controller.Snapshot().Title);
        fixture = new(); fixture.Session.SelectionEpoch = ulong.MaxValue; fixture.Select();
        TestAssert.Equal(0, fixture.Session.Executed.Count);
        TestAssert.Equal("Players Menu", fixture.Controller.Snapshot().Title);
    }

    public static void DelayedSelectionRequiresUnchangedFullReceiptOwnerEpochNavigationAndFocus() {
        foreach (string change in new[] { "guid", "owner", "epoch", "focus", "navigation", "disconnected", "busy" }) {
            var fixture = new Fixture(); fixture.Select(held: true);
            var target = fixture.Session.State.SelectedPlayerTarget!;
            if (change is "guid" or "owner" or "epoch") fixture.Session.State = fixture.Session.State with {
                SelectedPlayerTarget = new(change == "guid" ? target.Identity with { Guid = "FFFFFFFFFFFFFFFF" } : target.Identity,
                    target.Route, change == "owner" ? new object() : target.OwnerToken,
                    change == "epoch" ? target.SelectionRevision + 1 : target.SelectionRevision) };
            if (change == "focus") fixture.Focus = false;
            if (change == "navigation") fixture.Send(OverlayInput.Back);
            if (change == "disconnected") fixture.Session.State = fixture.Session.State with { Connected = false };
            if (change == "busy") fixture.Session.State = fixture.Session.State with { Busy = true };
            fixture.Session.Pending!.SetResult(); fixture.Controller.ObserveCompletion();
            TestAssert.True(fixture.Controller.Snapshot().Title != target.Identity.Name,
                "A changed completion scope cannot install the old target: " + change);
            fixture.Focus = true; fixture.Controller.ObserveCompletion();
            TestAssert.True(fixture.Controller.Snapshot().Title != target.Identity.Name, "Rejected completion cannot replay later");
        }
    }

    public static void QueuedTargetMutationCannotDispatchAfterFullIdentityReplacement() {
        var fixture = new Fixture(); fixture.Select(); fixture.Basic();
        var target = fixture.Session.State.SelectedPlayerTarget!;
        fixture.Send(OverlayInput.Select); TestAssert.True(fixture.Controller.Busy, "The leaf remains pending until publication");
        fixture.Session.State = fixture.Session.State with { SelectedPlayerTarget = new(target.Identity with { Client = 0x80000 },
            target.Route, target.OwnerToken, target.SelectionRevision + 1) };
        fixture.Controller.StartQueuedAction(); TestAssert.Equal(1, fixture.Session.Executed.Count);
        TestAssert.True(!fixture.Controller.Busy, "A stale private target is consumed without a game task");
        var row = fixture.Controller.Snapshot().Rows[0]; TestAssert.True(!row.Enabled && row.Value == "UNKNOWN",
            "A stale page cannot display another actor's confirmed Godmode value");
    }

    public static void SameTargetBooleanCompletionKeepsReceiptTopologyAndSourcePaintWait() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        PlayerMenuTestNavigation.Select(rig, "Players Menu"); PlayerMenuTestNavigation.Select(rig, "Friend");
        rig.Controller.StartQueuedAction(); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Controller.Snapshot();
        PlayerMenuTestNavigation.Select(rig, "Basic Scripts");
        var target = rig.Session.State.SelectedPlayerTarget!;
        var before = rig.Controller.Snapshot(); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        var action = rig.Session.Executed.Last(); TestAssert.Equal(MenuActionKind.ToggleTeammateGodmode, action.Kind);
        TestAssert.True(action.ExpectedTarget!.Equals(target), "The dispatch keeps the exact admitted target");
        rig.Session.State = rig.Session.State with { TeammateGodmodeValue = "OFF" };
        var pending = rig.Controller.Snapshot(); TestAssert.Equal("boolean", pending.MenuOptionCallback!.Kind);
        TestAssert.Equal("OFF", pending.Rows[0].Value); TestAssert.Equal("ON", pending.MenuBooleanPaint!.Rows[0].Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179)); TestAssert.Equal(before.MenuRevision, rig.Controller.Snapshot().MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(before.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
        TestAssert.Equal("OFF", redraw.MenuBooleanPaint!.Rows[0].Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); rig.Send(OverlayInput.Back);
        TestAssert.Equal("Basic Scripts", rig.Controller.Snapshot().Title);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.Send(OverlayInput.Back);
        TestAssert.Equal(target.Identity.Name, rig.Controller.Snapshot().Title);
        TestAssert.True(rig.Session.State.SelectedPlayerTarget!.Equals(target), "Godmode changes cannot invent a new selection epoch");
        rig.Session.State = rig.Session.State with { TeammateGodmodeValue = "ON", PersonalGodmodeValue = "OFF" };
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        rig.Send(OverlayInput.Toggle); rig.Open(OverlayInput.OpenQuick);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, rig.Session.Executed.Last().Kind);
        TestAssert.True(rig.Session.Executed.Last().ExpectedTarget is null,
            "Quick self cannot borrow the retained Main target receipt");
        TestAssert.True(rig.Session.State.SelectedPlayerTarget!.Equals(target), "Quick action cannot replace Main selection");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.Snapshot();
        rig.Send(OverlayInput.Back); rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        PlayerMenuTestNavigation.Select(rig, "Basic Scripts");
        TestAssert.Equal("ON", rig.Controller.Snapshot().Rows[0].Value);
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        TestAssert.True(rig.Session.Executed.Last().ExpectedTarget!.Equals(target),
            "Returning Main keeps its original peer action scope");
    }
}
