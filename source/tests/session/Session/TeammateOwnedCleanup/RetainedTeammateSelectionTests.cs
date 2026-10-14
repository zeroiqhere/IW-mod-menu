using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;

namespace Session.Tests;

internal static class RetainedTeammateSelectionTests {
    public static async Task ReplacementAfterInitialOwnerCheckCannotFallThroughToLivingCapture() {
        var transport = new PlayerStateReaderRetailTransport();
        RetailBackendSessionTests.Check(transport.Open(4242), "The owned reader fixture did not open");
        TeammateGodmodeSession? session = null;
        session = new(transport.ReadTeammateState, identity => new FlagsPlayerGodmodeFeature(
            session!.GuardOwnedTransport(identity, transport.BindTeammate(identity)), TimeSpan.FromSeconds(30)),
            transport.ReadOwnedTeammateFlags);
        try {
            RetailBackendSessionTests.Check(await session.SelectAsync(1, PlayerStateMemoryFixture.Name(1)),
                "The initial original teammate was not selected");
            RetailBackendSessionTests.Check((await session.ToggleAsync(1, PlayerStateMemoryFixture.Name(1)))?.Succeeded == true,
                "The transition test did not retain an active original owner");
            RetailBackendSessionTests.Check(await session.SelectAsync(2, PlayerStateMemoryFixture.Name(2)),
                "The transition test did not select the other teammate");
            PlayerIdentity otherIdentity = session.ReadView().Identity!;
            int strictReads = transport.StrictReadCalls(1);
            int ownedReads = transport.OwnedReadCalls(1);
            int writes = transport.FlagWrites;
            bool replaced = false;
            var selectionCommand = new AsyncLocal<bool>();
            // Only the selection's logical call may trigger this transition, never the monitor
            transport.AfterOwnedRead = state => {
                if (!selectionCommand.Value || state.Identity.Slot != 1) return;
                transport.AfterOwnedRead = null;
                transport.SetGuid(1, "ABCDEF0123456789");
                replaced = true;
            };

            bool selected;
            selectionCommand.Value = true;
            try {
                selected = await session.SelectAsync(1, PlayerStateMemoryFixture.Name(1));
            } finally {
                selectionCommand.Value = false;
            }
            RetailBackendSessionTests.Check(replaced && !selected && session.ReadView().Identity == otherIdentity
                && transport.TeammateBindCalls == 2 && transport.FlagWrites == writes
                && transport.StrictReadCalls(1) == strictReads && transport.OwnedReadCalls(1) >= ownedReads + 2,
                "A replacement after pending-owner validation fell through to strict living capture or displaced the cleanup owner");
        } finally {
            transport.AfterOwnedRead = null;
            int writes = transport.FlagWrites;
            await session.CloseAsync();
            RetailBackendSessionTests.Check(transport.FlagWrites == writes && transport.HealthWrites == 0,
                "The original owner cleanup wrote through the replacement identity");
            transport.Dispose();
        }
    }

    public static async Task ActiveOwnerReselectionRejectsSameNameReplacementWithoutStrictCaptureOrWrites() {
        foreach (int health in new[] { 100, 0 }) {
            var transport = new PlayerStateReaderRetailTransport();
            var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                await session.ExecuteAsync(SelectPlayer(1));
                await session.ExecuteAsync(GodmodeRow(session).Action);
                await session.ExecuteAsync(SelectPlayer(2));
                PlayerIdentity otherIdentity = session.ReadMenuState().SelectedPlayerTarget!.Identity;
                RetailBackendSessionTests.Check(transport.Flags(1) == 0x201,
                    "The replacement test did not retain an active original owner");

                // A living replacement catches any fallback into ordinary new capture
                transport.SetGuid(1, "ABCDEF0123456789");
                transport.SetHealth(1, health);
                int strictReads = transport.StrictReadCalls(1);
                int ownedReads = transport.OwnedReadCalls(1);
                int writes = transport.FlagWrites;
                await session.ExecuteAsync(SelectPlayer(1));

                RetailBackendSessionTests.Check(session.ReadMenuState().SelectedPlayerTarget?.Identity == otherIdentity
                    && transport.TeammateBindCalls == 2 && transport.FlagWrites == writes
                    && transport.StrictReadCalls(1) == strictReads && transport.OwnedReadCalls(1) > ownedReads,
                    "An active same-name replacement displaced the selection, reached strict capture, or changed flags");
            } finally {
                // The changed original identity must never be restored through its replacement
                int writes = transport.FlagWrites;
                await session.ShutdownAsync();
                RetailBackendSessionTests.Check(transport.FlagWrites == writes && transport.HealthWrites == 0,
                    "Shutdown wrote the replacement after its original identity proof failed");
            }
        }
    }

    public static async Task ProductionReaderReselectsOnlyAnOwnedDeadTargetAndWaitsForLifeBeforeEnabling() {
        var transport = new PlayerStateReaderRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(SelectPlayer(1));
            var captured = session.ReadMenuState().SelectedPlayerTarget;
            RetailBackendSessionTests.Check(captured is { Route: PlayerMenuRoute.Teammate }
                && captured.Identity.Slot == 1 && captured.Identity.Name == PlayerStateMemoryFixture.Name(1),
                "The production reader did not capture the original teammate identity");
            PlayerIdentity originalIdentity = captured!.Identity;
            await session.ExecuteAsync(GodmodeRow(session).Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201 && transport.TeammateBindCalls == 1,
                "The original teammate did not receive one retained ON owner");

            await session.ExecuteAsync(SelectPlayer(2));
            var otherTarget = session.ReadMenuState().SelectedPlayerTarget;
            RetailBackendSessionTests.Check(otherTarget?.Identity.Slot == 2,
                "The UI could not select a second living teammate");

            transport.SetHealth(3, 0);
            transport.SetFlags(3, 0x201);
            await session.ExecuteAsync(SelectPlayer(3));
            var afterForeignDeadSelection = session.ReadMenuState();
            RetailBackendSessionTests.Check(afterForeignDeadSelection.SelectedPlayerTarget?.Identity == otherTarget!.Identity
                && transport.TeammateBindCalls == 2 && transport.Flags(3) == 0x201 && transport.FlagWrites == 1,
                "A dead unowned actor with a foreign ON flag displaced the selection or gained an owner");

            transport.SetHealth(1, 0);
            bool strictReadRejectedDeath = false;
            try {
                transport.ReadTeammateState(1);
            } catch (InvalidOperationException) {
                strictReadRejectedDeath = true;
            }
            RetailBackendSessionTests.Check(strictReadRejectedDeath,
                "The compiled production reader unexpectedly accepted the dead actor as a living capture");
            int strictReadsBeforeReselect = transport.StrictReadCalls(1);
            int ownedReadsBeforeReselect = transport.OwnedReadCalls(1);

            await session.ExecuteAsync(SelectPlayer(1));
            var reselected = session.ReadMenuState();
            var onRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(reselected.SelectedPlayerTarget?.Identity == originalIdentity
                && reselected.SelectedSlot == 1 && onRow.Enabled && onRow.Value == "ON"
                && transport.TeammateBindCalls == 2
                && transport.StrictReadCalls(1) == strictReadsBeforeReselect
                && transport.OwnedReadCalls(1) > ownedReadsBeforeReselect,
                "The dead active owner was not reselected from fresh retained proof without a living read or new adapter");

            int writesBeforeOff = transport.FlagWrites;
            await session.ExecuteAsync(onRow.Action);
            var afterOff = session.ReadMenuState();
            var unavailableRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200
                && transport.FlagWrites == writesBeforeOff + 1
                && afterOff.SelectedPlayerTarget?.Identity == originalIdentity
                && unavailableRow.Value == "UNKNOWN" && !unavailableRow.Enabled
                && afterOff.TeammateGodmodeStatus.Contains("living state could not be confirmed", StringComparison.OrdinalIgnoreCase)
                && !afterOff.TeammateGodmodeStatus.Contains("identity changed", StringComparison.OrdinalIgnoreCase),
                "Confirmed OFF lost the retained identity or reported a false identity change after the strict dead read");

            await session.ExecuteAsync(onRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200 && transport.FlagWrites == writesBeforeOff + 1,
                "A stale row action re-enabled the dead actor after confirmed OFF");

            transport.SetHealth(1, 100);
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var recoveredRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(recoveredRow.Enabled && recoveredRow.Value == "OFF",
                "The same living identity did not regain explicit ON eligibility");
            await session.ExecuteAsync(recoveredRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201
                && transport.FlagWrites == writesBeforeOff + 2 && transport.TeammateBindCalls == 2
                && transport.HealthWrites == 0,
                "Explicit ON did not reuse the original adapter or wrote health");
        } finally {
            await session.ShutdownAsync();
        }
    }

    public static async Task ActualUiCanReselectADeadOwnedTeammateAndWaitForLifeBeforeEnabling() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            var initial = session.ReadMenuState().SelectedPlayerTarget;
            RetailBackendSessionTests.Check(initial?.Identity == transport.ReadTeammateState(1).Identity,
                "The first UI selection did not capture the complete teammate identity");
            await session.ExecuteAsync(GodmodeRow(session).Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201,
                "The first selected teammate did not receive its owned ON change");

            await session.ExecuteAsync(RetailBackendSessionTests.Select(2));
            var original = transport.ReadTeammateState(1);
            transport.SetPlayerState(1, original with { Health = 0 });
            await session.RefreshLocalObservationAsync();
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));

            var selected = session.ReadMenuState();
            var onRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(selected.SelectedPlayerTarget is { Route: PlayerMenuRoute.Teammate }
                && selected.SelectedPlayerTarget.Identity == initial!.Identity && selected.SelectedSlot == 1
                && onRow.Enabled && onRow.Value == "ON",
                "A dead active owner could not be reselected with its original cleanup receipt");
            RetailBackendSessionTests.Check(transport.TeammateBindCalls == 2,
                "Reselection created a second adapter instead of reusing the original owner");

            await session.ExecuteAsync(onRow.Action);
            var offRow = GodmodeRow(session);
            var offView = session.ReadMenuState();
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200 && transport.Flags(2) == 0x200
                && !offRow.Enabled && offRow.Value == "UNKNOWN"
                && offView.TeammateGodmodeStatus.Contains("not living", StringComparison.OrdinalIgnoreCase)
                && !offView.TeammateGodmodeStatus.Contains("identity changed", StringComparison.OrdinalIgnoreCase),
                "The dead owner's OFF result was hidden behind a false identity-change status");

            int writesWhileDead = transport.FlagWrites;
            await session.ExecuteAsync(onRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200 && transport.FlagWrites == writesWhileDead,
                "A stale ON receipt re-enabled a dead teammate after cleanup");

            transport.SetPlayerState(1, transport.ReadTeammateState(1) with { Health = 100 });
            await session.RefreshLocalObservationAsync();
            var recoveredRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(recoveredRow.Enabled && recoveredRow.Value == "OFF",
                "A living unchanged teammate did not recover explicit enable eligibility");
            await session.ExecuteAsync(recoveredRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201
                && transport.FlagWrites == writesWhileDead + 1 && transport.TeammateBindCalls == 2,
                "A living retry replaced the original adapter or failed to enter one explicit ON write");
        } finally {
            await session.ShutdownAsync();
        }
    }

    public static async Task UiRejectsWrongNamesDeadUnownedFlagsAndSameNameReplacement() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.SelectPlayer, Slot: 1, ExpectedName: "Wrong name"));
            var unowned = transport.ReadTeammateState(3);
            transport.SetPlayerState(3, unowned with { Health = 0, Flags = 0x201 });
            await session.ExecuteAsync(RetailBackendSessionTests.Select(3));
            RetailBackendSessionTests.Check(session.ReadMenuState().SelectedPlayerTarget is null
                && transport.TeammateBindCalls == 0 && transport.FlagWrites == 0 && transport.Flags(3) == 0x201,
                "A wrong-name action or dead foreign-ON actor created a teammate owner");

            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(2));
            var captured = transport.ReadTeammateState(1);
            transport.SetPlayerState(1, captured with {
                Identity = captured.Identity with { Guid = "same-name-replacement" }, Health = 0
            });
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            RetailBackendSessionTests.Check(session.ReadMenuState().SelectedPlayerTarget?.Identity.Slot == 2
                && transport.TeammateBindCalls == 2 && transport.FlagWrites == 0
                && transport.Flags(1) == 0x200 && transport.Flags(2) == 0x200,
                "A same-name dead replacement displaced the captured target or created another adapter");
        } finally {
            await session.ShutdownAsync();
        }
    }

    public static async Task PendingCleanupOnAnotherTeammateCannotRedirectTheSelectedOwner() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            await session.ExecuteAsync(GodmodeRow(session).Action);
            await session.ExecuteAsync(RetailBackendSessionTests.Select(2));
            await session.ExecuteAsync(GodmodeRow(session).Action);
            transport.RejectFlagRestore = true;
            await session.ExecuteAsync(GodmodeRow(session).Action);
            var pending = session.ReadMenuState();
            int writesBeforeRedirect = transport.FlagWrites;

            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));

            RetailBackendSessionTests.Check(pending.SelectedPlayerTarget?.Identity.Slot == 2
                && session.ReadMenuState().SelectedPlayerTarget?.Identity.Slot == 2
                && transport.TeammateBindCalls == 2 && transport.FlagWrites == writesBeforeRedirect
                && transport.Flags(1) == 0x201 && transport.Flags(2) == 0x201,
                "A different pending owner allowed selection to redirect or mutate the original cleanup target");

            transport.RejectFlagRestore = false;
            await session.ExecuteAsync(GodmodeRow(session).Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201 && transport.Flags(2) == 0x200,
                "The original pending selection could not retry its own confirmed OFF");
        } finally {
            transport.RejectFlagRestore = false;
            await session.ShutdownAsync();
        }
    }

    public static async Task UnknownOwnedReadCannotSelectOrReplaceAnExistingAdapter() {
        var fixture = new TeammateOwnedCleanupFixture();
        try {
            RetailBackendSessionTests.Check(await fixture.Session.SelectAsync(1, "Friend1"),
                "The initial read-only owner capture failed");
            fixture.ReadFails = true;
            bool selected = await fixture.Session.SelectAsync(1, "Friend1");
            fixture.ReadFails = false;

            RetailBackendSessionTests.Check(!selected && fixture.Binds == 1 && fixture.Writes == 0
                && !fixture.Session.ReadView().CanToggle,
                "An unknown owned read selected or replaced a teammate adapter");
        } finally {
            fixture.ReadFails = false;
            await fixture.Session.CloseAsync();
        }
    }

    private static MenuRow GodmodeRow(RetailBackendSession session) =>
        OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, session.ReadMenuState())
            .Rows.Single(row => row.Label == "God Mode");

    private static MenuAction SelectPlayer(int slot) => new(MenuActionKind.SelectPlayer,
        Slot: slot, ExpectedName: PlayerStateMemoryFixture.Name(slot));
}
