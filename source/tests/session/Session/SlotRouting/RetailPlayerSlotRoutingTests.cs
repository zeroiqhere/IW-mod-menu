using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Real session and bound flag adapters operate only on this fixture's stored players
internal static class RetailPlayerSlotRoutingTests {
    public static async Task NonzeroPersonalActorsAndPeerZeroUseIndependentActualMenuFlagOwners() {
        foreach (int localSlot in new[] { 2, 3 }) {
            var transport = new RecordingRetailTransport { LocalSlot = localSlot };
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                var personal = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
                Check(personal.Enabled && personal.Value == "OFF", "The verified personal row must be available");
                await session.ExecuteAsync(personal.Action);
                await session.ExecuteAsync(SelectCurrent(transport, 0));

                foreach (string expected in new[] { "ON", "OFF", "ON" }) {
                    var before = PeerRow(session);
                    Check(before.Enabled && before.Action.Kind == MenuActionKind.ToggleTeammateGodmode
                        && before.Action.Slot == 0, "Foreign slot zero must expose the actual selected-player action");
                    await session.ExecuteAsync(before.Action);
                    var after = PeerRow(session);
                    Check(after.Value == expected, "The actual peer row must confirm ON, OFF, and ON");
                    Check(transport.Flags(localSlot) == 0x201 && session.ReadMenuState().PersonalGodmodeValue == "ON",
                        "The peer action must not change the independent personal owner");
                }
                Check(transport.TeammateBindCalls == 1 && transport.LocalBindCalls == 1 && transport.HealthWrites == 0,
                    "Independent routes must retain one flags adapter each without health writes");
                foreach (int other in Enumerable.Range(0, 4).Except(new[] { 0, localSlot })) {
                    Check(transport.Flags(other) == 0x200, "A slot routing action must not change an unrelated player");
                }
                var closed = await session.ShutdownAsync();
                Check(closed.Stopped && closed.Restored && transport.Flags(0) == 0x200
                    && transport.Flags(localSlot) == 0x200 && transport.DisposeCalls == 1,
                    "Shutdown must restore both original flag owners before disposal");
            } finally { await session.ShutdownAsync(); }
        }
    }

    public static async Task VerifiedLocalZeroSelectionUsesOnlyThePersonalOwner() {
        var transport = new RecordingRetailTransport { LocalSlot = 0 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(SelectCurrent(transport, 0));
            var frame = session.ReadMenuState();
            var players = OverlayMenuBuilder.Build(MenuPage.Players, frame);
            var selfLink = players.Rows.Single(row => row.Action.Slot == 0);

            Check(frame.SelectedSlot == 0 && frame.PersonalGodmodeIdentity == transport.ReadTeammateState(0).Identity
                && !frame.CanTeammateGodmode && selfLink.Action.Page == MenuPage.SelectedPlayer,
                "Verified self zero must retain the personal route rather than a teammate capability");
            var personal = OverlayMenuBuilder.Build(MenuPage.Personal, frame).Rows[0];
            Check(personal.Enabled && personal.Value == "OFF", "The self menu must reflect its confirmed personal state");
            await session.ExecuteAsync(personal.Action);
            // A stale generic selected-player intent must still reach the same original personal owner
            await session.ExecuteAsync(ToggleCurrent(transport, 0));
            Check(transport.Flags(0) == 0x200 && transport.LocalBindCalls == 1
                && transport.TeammateBindCalls == 0 && transport.HealthWrites == 0,
                "Self zero must not construct a second writer when generic selection intent is received");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task PersonalFirstZeroReservationBlocksTeammateBindingEvenAfterOff() {
        var transport = new RecordingRetailTransport { LocalSlot = 0 };
        transport.Open(4242);
        var owner = new RetailConnection(transport, processId: 4242);
        try {
            Check(await owner.LocalGodmode!.CaptureAsync(), "The personal zero reservation must be captured");
            Check(!await owner.Teammates.SelectAsync(0, "Host") && transport.TeammateBindCalls == 0
                && owner.Teammates.ReadView().Status.Contains("already has a Godmode owner", StringComparison.Ordinal),
                "A valid zero selection must reach and respect the existing personal reservation");
            Check(await owner.LocalGodmode.ToggleAsync() && await owner.LocalGodmode.RestoreAsync(),
                "The original personal owner must confirm ON and OFF");
            Check(!await owner.Teammates.SelectAsync(0, "Host") && transport.TeammateBindCalls == 0,
                "Confirmed OFF must not release a retained personal adapter for another writer");
        } finally {
            await owner.LocalGodmode!.CloseAsync();
            await owner.Teammates.CloseAsync();
            transport.Dispose();
        }
    }

    public static async Task TeammateFirstZeroReservationCannotTransferToLaterPersonalVerification() {
        var transport = new RecordingRetailTransport { LocalSlot = 0, FailLocalRead = true };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(SelectCurrent(transport, 0));
            var peer = PeerRow(session);
            Check(peer.Enabled && peer.Action.Kind == MenuActionKind.ToggleTeammateGodmode,
                "Unproved personal identity must not borrow the verified selected-player flags capability");
            await session.ExecuteAsync(peer.Action);
            await session.ExecuteAsync(PeerRow(session).Action);
            Check(transport.Flags(0) == 0x200 && transport.TeammateBindCalls == 1,
                "The original teammate writer must retain its pin after OFF");

            transport.FailLocalRead = false;
            int writes = transport.FlagWrites;
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            var frame = session.ReadMenuState();
            Check(frame.PersonalGodmodeValue == "UNKNOWN" && !frame.CanPersonalGodmode
                && frame.PersonalGodmodeIdentity is null && transport.LocalBindCalls == 0
                && transport.TeammateBindCalls == 1 && transport.FlagWrites == writes,
                "Later personal proof must not transfer a retained teammate zero writer");
            peer = PeerRow(session);
            Check(peer.Enabled, "Rejected personal capture must not remove the original teammate action route");
            await session.ExecuteAsync(peer.Action);
            Check(transport.Flags(0) == 0x201 && transport.LocalBindCalls == 0,
                "The original teammate zero owner must remain the only flags writer");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task WrongSlotAndNamePayloadsCannotUseTheSelectedPeerZeroWriter() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(SelectCurrent(transport, 0));
            Check(PeerRow(session).Enabled, "Peer zero selection must be established before testing rejected payloads");
            var rejected = new[] {
                new MenuAction(MenuActionKind.ToggleTeammateGodmode, Slot: 1, ExpectedName: "Host"),
                new MenuAction(MenuActionKind.ToggleTeammateGodmode, Slot: 0, ExpectedName: "Wrong name"),
                new MenuAction(MenuActionKind.TogglePersonalGodmode, Slot: 0, ExpectedName: "Host"),
                new MenuAction(MenuActionKind.SelectPlayer, Slot: 0, ExpectedName: "Wrong name"),
                new MenuAction(MenuActionKind.SelectPlayer, Slot: -1, ExpectedName: "Host"),
                new MenuAction(MenuActionKind.SelectPlayer, Slot: 4, ExpectedName: "Host")
            };

            foreach (var action in rejected) await session.ExecuteAsync(action);

            var frame = session.ReadMenuState();
            Check(frame.SelectedSlot == 0 && frame.SelectedName == "Host" && PeerRow(session).Value == "OFF"
                && transport.FlagWrites == 0 && transport.HealthWrites == 0 && transport.TeammateBindCalls == 1,
                "Wrong payloads must retain the original selection without any flags write or rebinding");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task PeerZeroIdentityReplacementCannotTransferItsRetainedWriter() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(SelectCurrent(transport, 0));
            var action = PeerRow(session).Action;
            await session.ExecuteAsync(action);
            Check(transport.Flags(0) == 0x201, "The original peer zero flag must be owned before replacement");
            var original = transport.ReadTeammateState(0);
            var replacement = original with { Identity = original.Identity with { Guid = "1111111111111111" } };
            transport.SetPlayerState(0, replacement);
            int writes = transport.FlagWrites;

            await session.RefreshLocalObservationAsync();
            var changed = session.ReadMenuState();
            await session.ExecuteAsync(action);
            await session.ExecuteAsync(SelectCurrent(transport, 0));

            Check(changed.SelectedSlot == 0 && !changed.CanTeammateGodmode && changed.TeammateGodmodeValue == "UNKNOWN"
                && transport.TeammateBindCalls == 1 && transport.FlagWrites == writes,
                "Same slot and name with a changed full identity must not transfer the original writer");
            var failed = await session.ShutdownAsync();
            Check(!failed.Stopped && !failed.Restored && transport.DisposeCalls == 0,
                "An unconfirmed original peer flag must retain its resource for cleanup");
        } finally {
            // Ending the owned fixture process releases stale restoration without writing replacement bytes
            transport.Disposed = true;
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }

    public static async Task FailedPeerZeroRestoreRetainsTransportWhilePersonalCleanupAndRetryComplete() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0].Action);
            await session.ExecuteAsync(SelectCurrent(transport, 0));
            var peer = PeerRow(session);
            Check(peer.Enabled, "The peer recovery test must use an actual available selected-player row");
            await session.ExecuteAsync(peer.Action);
            transport.RejectFlagRestore = true;

            var failed = await session.ShutdownAsync();

            Check(!failed.Stopped && !failed.Restored && transport.DisposeCalls == 0
                && transport.Flags(3) == 0x200 && transport.Flags(0) == 0x201,
                "Peer restoration failure must retain its transport while independent personal cleanup succeeds");
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(transport.OpenCalls == 1 && !session.ReadMenuState().CanConnect,
                "An unconfirmed peer owner must prevent reconnect");
            transport.RejectFlagRestore = false;
            session.AllowShutdownRetry();
            var retried = await session.ShutdownAsync();
            Check(retried.Stopped && retried.Restored && transport.Flags(0) == 0x200
                && transport.Flags(3) == 0x200 && transport.DisposeCalls == 1
                && transport.TeammateBindCalls == 1 && transport.LocalBindCalls == 1 && transport.HealthWrites == 0,
                "Retry must restore the original peer writer before releasing the one retained transport");
        } finally {
            transport.RejectFlagRestore = false;
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }

    private static MenuRow PeerRow(RetailBackendSession session) =>
        OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, session.ReadMenuState()).Rows[0];

    // Names are fixture display hints; production routing still validates the captured full identity
    private static MenuAction SelectCurrent(RecordingRetailTransport transport, int slot) =>
        new(MenuActionKind.SelectPlayer, Slot: slot, ExpectedName: transport.ReadTeammateState(slot).Identity.Name);

    private static MenuAction ToggleCurrent(RecordingRetailTransport transport, int slot) =>
        new(MenuActionKind.ToggleTeammateGodmode, Slot: slot, ExpectedName: transport.ReadTeammateState(slot).Identity.Name);
}
