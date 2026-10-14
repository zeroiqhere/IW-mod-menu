using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// Exercise real queued actions and navigation without a live game process
internal static class PersonalGodmodeRecoveryControllerTests {
    private static readonly PlayerIdentity Local = new(2, 0x10000, 0x20000, 0x30000,
        4, "Local player", "1234567890ABCDEF") { ServerClient = 0x40000 };
    private static SessionBoundary Session() => new() { State = new(false, true, false, false,
        false, "UNKNOWN", "Restoration unconfirmed", false, "UNKNOWN", "", true, true,
        new[] { new OverlayPlayer(2, Local.Name) }, -1, null, false, "UNKNOWN", "", false, "") {
            PersonalGodmodeIdentity = Local, PersonalGodmodeRequiresVerifiedTarget = true,
            CanRestorePersonalGodmode = true
        }
    };
    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);

    public static void RecoverySelectionDispatchesOnlyOffAndPublishesBusyBeforeExecution() {
        var session = Session(); var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down); Send(controller, 3, OverlayInput.Select);
        session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(controller, 4, OverlayInput.Select);
        TestAssert.True(controller.Busy && !controller.Snapshot().Rows[0].Enabled,
            "OFF recovery must publish its busy acknowledgment before execution");
        TestAssert.Equal(0, session.Executed.Count);
        controller.StartQueuedAction();
        TestAssert.Equal(MenuActionKind.RestorePersonalGodmode, session.Executed.Single().Kind);
        Send(controller, 5, OverlayInput.Select); controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        session.Pending.SetResult(); controller.ObserveCompletion();
    }

    public static void PendingPersonalSelectionUsesOnlyUnchangedLiveTargetProof() {
        foreach (var (replacement, connected, busy, requiresProof, opensPersonal) in new (PlayerIdentity?, bool, bool, bool, bool)[] {
            (null, true, false, true, false), (Local with { Guid = "ABCDEF0123456789" }, true, false, true, false),
            (Local with { Client = 0x80000 }, true, false, true, false), (Local with { ServerClient = 0x90000 }, true, false, true, false),
            (Local, false, false, true, false), (Local, true, true, true, false),
            (Local, true, false, false, false), (Local, true, false, true, true) }) {
            var session = Session(); var clock = new ManualTimeProvider();
            var controller = new BackendController(42, session, clock: clock);
            Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
            Send(controller, 2, OverlayInput.Down); Send(controller, 3, OverlayInput.Down);
            Send(controller, 4, OverlayInput.Down); Send(controller, 5, OverlayInput.Select);
            Send(controller, 6, OverlayInput.Down);
            session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Send(controller, 7, OverlayInput.Select); controller.StartQueuedAction();
            TestAssert.Equal(MenuPage.SelectedPlayer, session.Executed.Single().Page);
            session.State = session.State with { SelectedSlot = 2, SelectedName = Local.Name,
                PersonalGodmodeIdentity = replacement, Connected = connected, Busy = busy,
                PersonalGodmodeRequiresVerifiedTarget = requiresProof,
                // The fake receipt mirrors a session rejecting a replaced or withdrawn full binding
                SelectedPlayerTarget = requiresProof && replacement == Local
                    ? session.State.SelectedPlayerTarget : null };
            session.Pending.SetResult(); controller.ObserveCompletion();
            TestAssert.Equal(opensPersonal ? Local.Name : "Players Menu", controller.Snapshot().Title);
            if (opensPersonal) {
                clock.Advance(TimeSpan.FromMilliseconds(250));
                Send(controller, 8, OverlayInput.Down); Send(controller, 9, OverlayInput.Select);
                var recovery = controller.Snapshot().Rows[0];
                TestAssert.True(recovery.Enabled, "Confirmed selection must allow owned OFF recovery");
                Send(controller, 10, OverlayInput.Select); controller.StartQueuedAction();
                TestAssert.Equal(MenuActionKind.RestorePersonalGodmode, session.Executed.Last().Kind);
            }
        }
    }

    public static void PendingTeammateSelectionCannotCompleteOnAChangedPersonalRoute() {
        var session = Session(); var clock = new ManualTimeProvider();
        session.State = session.State with { Players = new[] { new OverlayPlayer(1, "Friend") } };
        var controller = new BackendController(42, session, clock: clock);
        Send(controller, 1, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, 2, OverlayInput.Down); Send(controller, 3, OverlayInput.Down);
        Send(controller, 4, OverlayInput.Down); Send(controller, 5, OverlayInput.Select);
        Send(controller, 6, OverlayInput.Down);
        session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(controller, 7, OverlayInput.Select); controller.StartQueuedAction();
        TestAssert.Equal(MenuPage.SelectedPlayer, session.Executed.Single().Page);
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true,
            PersonalGodmodeIdentity = Local with { Slot = 1, Name = "Friend" },
            SelectedPlayerTarget = new(Local with { Slot = 1, Name = "Friend" }, PlayerMenuRoute.Personal,
                session.State.SelectedPlayerTarget!.OwnerToken, session.State.SelectedPlayerTarget.SelectionRevision + 1) };
        session.Pending.SetResult(); controller.ObserveCompletion();
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
    }
}
