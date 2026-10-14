namespace Session.Tests;

internal static class TeammateOwnedCleanupTests {
    public static async Task SelectedOriginalDeadOwnerCanRestoreWithoutAuthorizingAnotherEnable() {
        var fixture = new TeammateOwnedCleanupFixture();
        try {
            await fixture.Enable();fixture.Replace(state => state with { Health = 0 });
            await fixture.Session.RefreshAsync();
            var view = fixture.Session.ReadView();
            RetailBackendSessionTests.Check(view.CanToggle && view.Value == syndishanx_iw_tool.Features.Godmode.GodmodeValue.On,
                "The original dead ON owner did not retain its cleanup toggle");
            var off = await fixture.Session.ToggleAsync(1, "Friend1");
            RetailBackendSessionTests.Check(off?.Succeeded == true && fixture.ReadCurrent().Flags == 0x200,
                "Death blocked restoration of the original retained flags");
            int writes = fixture.Writes;
            RetailBackendSessionTests.Check(!fixture.Session.ReadView().CanToggle
                && await fixture.Session.ToggleAsync(1, "Friend1") is null && fixture.Writes == writes,
                "A dead OFF owner was allowed to enable again");
            fixture.Replace(state => state with { Health = 100 });
            await fixture.Session.RefreshAsync();
            RetailBackendSessionTests.Check(fixture.Session.ReadView().CanToggle
                && (await fixture.Session.ToggleAsync(1, "Friend1"))?.Succeeded == true
                && fixture.ReadCurrent().Flags == 0x201 && fixture.Binds == 1,
                "A life-only refusal permanently poisoned the unchanged original owner");
        } finally { await fixture.Session.CloseAsync(); }
    }

    public static async Task CloseRestoresOriginalDeadOwnerAndRemainsIdempotent() {
        var fixture = new TeammateOwnedCleanupFixture();
        await fixture.Enable();fixture.Replace(state => state with { Health = 1 });
        var close = await fixture.Session.CloseAsync();
        RetailBackendSessionTests.Check(close.Stopped && close.Restored && fixture.ReadCurrent().Flags == 0x200,
            "Shutdown failed to restore the same downed owner");
        int writes = fixture.Writes;
        RetailBackendSessionTests.Check((await fixture.Session.CloseAsync()).Restored && fixture.Writes == writes,
            "Repeated close wrote or lost confirmed cleanup");
    }

    public static async Task ReplacedRollbackAndUnreadableOwnersCannotBorrowRecoveredProof() {
        foreach (string failure in new[] { "identity", "rollback", "read", "disconnect" }) {
            var fixture = new TeammateOwnedCleanupFixture();
            try {
                await fixture.Enable();var original = fixture.ReadCurrent();
                if (failure == "identity") fixture.Replace(state => state with {
                    Identity = state.Identity with { Guid = "replacement" }
                });
                if (failure == "rollback") fixture.Replace(state => state with { CommandTime = 99 });
                if (failure == "read") fixture.ReadFails = true;
                if (failure == "disconnect") fixture.Connected = false;
                await fixture.Session.RefreshAsync();
                RetailBackendSessionTests.Check(!fixture.Session.ReadView().CanToggle,
                    "An invalid owned proof published cleanup permission");
                fixture.ReadFails = false;fixture.Connected = true;fixture.Replace(_ => original);
                int writes = fixture.Writes;
                RetailBackendSessionTests.Check(await fixture.Session.ToggleAsync(1, "Friend1") is null
                    && fixture.Writes == writes && !fixture.Session.ReadView().CanToggle,
                    "Recovered display facts revived a retired session proof");
                var close = await fixture.Session.CloseAsync();
                RetailBackendSessionTests.Check(close.Stopped && !close.Restored && fixture.Writes == writes
                    && fixture.ReadCurrent().Flags == 0x201,
                    "Recovered underlying facts let shutdown write through a retired session proof");
            } finally { await fixture.Session.CloseAsync(); }
        }
    }

    public static async Task WrongPayloadAndNonlivingSelectionNeverBindOrWriteAnotherOwner() {
        var fixture = new TeammateOwnedCleanupFixture();
        try {
            fixture.Replace(state => state with { Health = 0 });
            RetailBackendSessionTests.Check(!await fixture.Session.SelectAsync(1, "Friend1") && fixture.Binds == 0,
                "A nonliving selection created an adapter");
            fixture.Replace(state => state with { Health = 100 });await fixture.Enable();
            foreach (var payload in new[] { (0, "Friend1"), (1, "Replacement"), (4, "Friend1") })
                RetailBackendSessionTests.Check(await fixture.Session.ToggleAsync(payload.Item1, payload.Item2) is null,
                    "A mismatched name or slot reached the selected writer");
            RetailBackendSessionTests.Check(fixture.Writes == 1 && fixture.Binds == 1,
                "Rejected payloads wrote or rebound an owner");
        } finally { await fixture.Session.CloseAsync(); }
    }

    public static async Task LegacyStrictOwnerKeepsItsExistingDeathAdmission() {
        var fixture = new TeammateOwnedCleanupFixture(owned: false);
        await fixture.Enable();fixture.Replace(state => state with { Health = 0 });
        await fixture.Session.RefreshAsync();
        RetailBackendSessionTests.Check(!fixture.Session.ReadView().CanToggle
            && await fixture.Session.ToggleAsync(1, "Friend1") is null,
            "The optional capability changed a legacy strict owner");
        await fixture.Session.CloseAsync();
    }

    public static async Task ActualRetailRowsRestoreDeadOwnerThenDisableNewOnAndDisposeAfterShutdown() {
        var transport = new RecordingRetailTransport();var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(syndishanx_iw_tool.Overlay.MenuActionKind.Connect));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            await session.ExecuteAsync(RetailBackendSessionTests.Toggle(1));
            transport.SetPlayerState(1, transport.ReadTeammateState(1) with { Health = 0 });
            var row = PeerRow(session);
            RetailBackendSessionTests.Check(row.Enabled && row.Value == "ON",
                "Actual selected-player row hid the dead original cleanup owner");
            await session.ExecuteAsync(row.Action);
            row = PeerRow(session);int writes = transport.FlagWrites;
            RetailBackendSessionTests.Check(!row.Enabled && row.Value == "UNKNOWN" && transport.Flags(1) == 0x200,
                "The dead restored row claimed new enable permission");
            await session.ExecuteAsync(RetailBackendSessionTests.Toggle(1));
            RetailBackendSessionTests.Check(transport.FlagWrites == writes && transport.TeammateBindCalls == 1,
                "A stale row action enabled or replaced the restored owner");
            var close = await session.ShutdownAsync();
            RetailBackendSessionTests.Check(close.Stopped && close.Restored && transport.DisposeCalls == 1,
                "Confirmed cleanup did not release exactly the original transport");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task IndependentOriginalOwnersRestoreAfterDeathWithoutChangingUnrelatedFlags() {
        var transport = new RecordingRetailTransport();var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(syndishanx_iw_tool.Overlay.MenuActionKind.Connect));
            foreach (int slot in new[] { 1, 2 }) {
                await session.ExecuteAsync(RetailBackendSessionTests.Select(slot));
                await session.ExecuteAsync(RetailBackendSessionTests.Toggle(slot));
                transport.SetPlayerState(slot, transport.ReadTeammateState(slot) with { Health = slot - 1 });
            }
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201 && transport.Flags(2) == 0x201,
                "Death transferred or released an independent owned flag");
            var close = await session.ShutdownAsync();
            RetailBackendSessionTests.Check(close.Stopped && close.Restored && transport.TeammateBindCalls == 2
                && Enumerable.Range(0, 4).All(slot => transport.Flags(slot) == 0x200)
                && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.DisposeCalls == 1,
                "Shutdown failed to restore only the retained teammate flag owners");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task FailedDeadRestoreRetainsTransportUntilOriginalCleanupRetry() {
        var transport = new RecordingRetailTransport();var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(syndishanx_iw_tool.Overlay.MenuActionKind.Connect));
            await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
            await session.ExecuteAsync(RetailBackendSessionTests.Toggle(1));
            transport.SetPlayerState(1, transport.ReadTeammateState(1) with { Health = 0 });
            transport.RejectFlagRestore = true;
            var failed = await session.ShutdownAsync();
            RetailBackendSessionTests.Check(!failed.Restored && !failed.Stopped && transport.DisposeCalls == 0
                && !session.ReadMenuState().CanConnect && transport.Flags(1) == 0x201,
                "Failed restoration disposed or replaced the original dead owner");
            transport.RejectFlagRestore = false;session.AllowShutdownRetry();
            var retry = await session.ShutdownAsync();
            RetailBackendSessionTests.Check(retry.Restored && retry.Stopped && transport.Flags(1) == 0x200
                && transport.DisposeCalls == 1 && transport.TeammateBindCalls == 1,
                "Cleanup retry did not restore the retained adapter before disposal");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task StrictReplacementOrNegativeTimeRetiresExpectedOwnerDespiteOwnedRecovery() {
        foreach (bool replacement in new[] { true, false }) {
            var fixture = new TeammateOwnedCleanupFixture();
            try {
                RetailBackendSessionTests.Check(await fixture.Session.SelectAsync(1, "Friend1"), "Initial OFF capture failed");
                fixture.StrictTransform = state => replacement
                    ? state with { Identity = state.Identity with { Client = state.Identity.Client + 8 } }
                    : state with { CommandTime = -1 };
                await fixture.Session.RefreshAsync();
                RetailBackendSessionTests.Check(!fixture.Session.ReadView().CanToggle,
                    "A changed strict sample published ON permission");
                fixture.StrictTransform = null;int writes = fixture.Writes;
                RetailBackendSessionTests.Check(await fixture.Session.ToggleAsync(1, "Friend1") is null
                    && fixture.Writes == writes && !fixture.Session.ReadView().CanToggle,
                    "Recovered owned facts revived an expected owner after strict identity or time changed");
            } finally { await fixture.Session.CloseAsync(); }
        }
    }

    public static async Task FirstOwnedProofCannotRollBackBelowStrictSelectionSample() {
        var fixture = new TeammateOwnedCleanupFixture {
            OwnedTransform = state => state with { CommandTime = 99 }
        };
        try {
            RetailBackendSessionTests.Check(!await fixture.Session.SelectAsync(1, "Friend1")
                && fixture.Binds == 0 && fixture.Writes == 0,
                "The first owned observation silently rolled back below the selection timestamp");
        } finally { await fixture.Session.CloseAsync(); }
    }

    public static async Task CloseDuringHeldBoundPreflightPreventsTheLaterEnableEntry() {
        var fixture = new TeammateOwnedCleanupFixture();
        RetailBackendSessionTests.Check(await fixture.Session.SelectAsync(1, "Friend1"), "Initial OFF capture failed");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeBoundRead = () => {
            fixture.BeforeBoundRead = null;entered.TrySetResult();release.Task.GetAwaiter().GetResult();
        };
        var toggle = Task.Run(() => fixture.Session.ToggleAsync(1, "Friend1"));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var close = fixture.Session.CloseAsync();release.TrySetResult();
            await Task.WhenAll(toggle, close).WaitAsync(TimeSpan.FromSeconds(5));
            RetailBackendSessionTests.Check(fixture.Writes == 0 && fixture.ReadCurrent().Flags == 0x200
                && close.Result.Stopped && close.Result.Restored,
                "A queued ON entered its write after close was requested");
        } finally { release.TrySetResult();await fixture.Session.CloseAsync(); }
    }

    private static syndishanx_iw_tool.Overlay.MenuRow PeerRow(Apparition.Backend.Session.RetailBackendSession session) =>
        syndishanx_iw_tool.Overlay.OverlayMenuBuilder.Build(syndishanx_iw_tool.Overlay.MenuPage.SelectedBasicScripts,
            session.ReadMenuState()).Rows.Single(row => row.Label == "God Mode");
}
