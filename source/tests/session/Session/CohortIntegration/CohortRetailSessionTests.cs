using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class CohortRetailSessionTests {
    private static RetailBackendSession Create(CohortRetailTransport transport, TimeSpan? timeout = null) =>
        new(4242, () => true, () => transport, () => false,
            _ => new OwnedLocalPlayerObservationProvider(4242, transport), timeout);
    private static MenuAction Toggle(OverlayMenuState state) => new(MenuActionKind.ToggleAllPlayersGodmode,
        MenuPage.AllPlayers) { ExpectedCohort = state.AllPlayersGodmodeTarget };

    public static async Task CompleteHostCohortSharesAllOnMixedAndOffWithOriginalPersonalOwners() {
        foreach (int localSlot in Enumerable.Range(0, 4)) {
            var transport = new CohortRetailTransport();
            transport.Inner.LocalSlot = localSlot;
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                var initial = session.ReadMenuState();
                Check(initial.CanAllPlayersGodmode && initial.AllPlayersGodmodeValue == "OFF"
                    && initial.AllPlayersGodmodeTarget is not null, "Complete host cohort was not published");
                Check(transport.Inner.FlagWrites == 0, "Connect changed a flag");
                await session.ExecuteAsync(Toggle(initial));
                var enabled = session.ReadMenuState();
                Check(enabled.CanAllPlayersGodmode && enabled.AllPlayersGodmodeValue == "ON"
                    && Enumerable.Range(0, 4).All(slot => transport.Inner.Flags(slot) == 0x201), "Group ON did not share the actual owners");
                await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
                var mixed = session.ReadMenuState();
                Check(mixed.CanAllPlayersGodmode && mixed.AllPlayersGodmodeValue == "OFF"
                    && transport.Inner.Flags(localSlot) == 0x200, "Individual OFF did not refresh source all-ON meaning");
                await session.ExecuteAsync(Toggle(mixed));
                await session.ExecuteAsync(Toggle(session.ReadMenuState()));
                Check(Enumerable.Range(0, 4).All(slot => transport.Inner.Flags(slot) == 0x200), "Group OFF left an owned flag");
                Check(transport.Inner.LocalBindCalls == 1 && transport.Inner.TeammateBindCalls == 3
                    && transport.Inner.HealthWrites == 0 && transport.Inner.AmmoWrites == 0, "Group created fallback or duplicate writers");
            } finally { Check((await session.ShutdownAsync()).Stopped, "Owned group did not stop"); }
        }
    }

    public static async Task PartialOrNonhostCohortCannotBorrowThePersonalGodmodeCapability() {
        foreach (bool partial in new[] { false, true }) {
            var transport = new CohortRetailTransport { Host = partial, UnknownSlot = partial };
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                var state = session.ReadMenuState();
                Check(state.CanPersonalGodmode && !state.CanAllPlayersGodmode
                    && state.AllPlayersGodmodeTarget is null, "Partial or nonhost source borrowed local capability");
                await session.ExecuteAsync(Toggle(state));
                Check(transport.Inner.FlagWrites == 0, "Missing receipt dispatched a group change");
            } finally { await session.ShutdownAsync(); }
        }
    }

    public static async Task LostHostProofRetiresTheQueuedTargetAndRecoveryCannotReplayIt() {
        var transport = new CohortRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var original = session.ReadMenuState();
            Check(original.AllPlayersGodmodeTarget is not null, "Initial target missing");
            transport.Host = false;
            await session.ExecuteAsync(Toggle(original));
            Check(!session.ReadMenuState().CanAllPlayersGodmode && transport.Inner.FlagWrites == 0, "Lost host proof wrote flags");
            transport.Host = true;
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var recovered = session.ReadMenuState();
            Check(recovered.CanAllPlayersGodmode && !ReferenceEquals(original.AllPlayersGodmodeTarget,
                recovered.AllPlayersGodmodeTarget), "Recovery revived the withdrawn target");
            await session.ExecuteAsync(Toggle(original));
            Check(transport.Inner.FlagWrites == 0, "Old target replayed after recovery");
            await session.ExecuteAsync(Toggle(recovered));
            Check(transport.Inner.FlagWrites == 4, "Fresh recovered target failed");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task CohortObservationReadsOnlyWhenItsMenuVisitRequestsIt() {
        var transport = new CohortRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            int before = transport.CohortReads;
            await session.RefreshLocalObservationAsync();
            Check(transport.CohortReads == before, "Closed group page polled the cohort");
            session.SetAllPlayersGodmodeObservationEnabled(true);
            await session.RefreshLocalObservationAsync();
            Check(transport.CohortReads > before, "Open group page failed to request a retained read");
            session.SetAllPlayersGodmodeObservationEnabled(false);
            before = transport.CohortReads;
            await session.RefreshLocalObservationAsync();
            Check(transport.CohortReads == before, "Closed group visit continued cohort reads");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task GroupPollingWithoutALocalProviderRefreshesUnselectedPeerMetadata() {
        var transport = new CohortRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(session.ReadMenuState().CanSelectPlayers, "Initial peer selection metadata was unavailable");
            int before = transport.CohortReads;
            session.SetAllPlayersGodmodeObservationEnabled(true);
            await session.RefreshLocalObservationAsync();
            var refreshed = session.ReadMenuState();
            Check(transport.CohortReads > before && refreshed.CanAllPlayersGodmode,
                "A requested group refresh was skipped without a local observation provider");
            Check(refreshed.CanSelectPlayers && refreshed.SelectedSlot == -1,
                "Group capture invalidated unselected peer metadata without refreshing its publication");
            Check(transport.Inner.FlagWrites == 0 && transport.Inner.TeammateBindCalls == 3,
                "Read-only group polling wrote or rebound its captured actor owners");
            session.SetAllPlayersGodmodeObservationEnabled(false);
            before = transport.CohortReads;
            await session.RefreshLocalObservationAsync();
            Check(transport.CohortReads == before, "A closed group visit kept reading without a selected peer");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task WrongPageAndAnotherConnectionReceiptCannotDispatchTheCurrentCohort() {
        var firstTransport = new CohortRetailTransport();
        var secondTransport = new CohortRetailTransport();
        var first = Create(firstTransport);
        var second = Create(secondTransport);
        try {
            await first.ExecuteAsync(new(MenuActionKind.Connect));
            await second.ExecuteAsync(new(MenuActionKind.Connect));
            var original = first.ReadMenuState();
            var other = second.ReadMenuState();
            Check(original.CanAllPlayersGodmode && other.CanAllPlayersGodmode, "Positive owners were unavailable");
            await first.ExecuteAsync(Toggle(original) with { Page = MenuPage.Main });
            await first.ExecuteAsync(Toggle(other));
            Check(firstTransport.Inner.FlagWrites == 0 && secondTransport.Inner.FlagWrites == 0,
                "Wrong route or foreign connection receipt borrowed the current cohort");
            await first.ExecuteAsync(Toggle(original));
            Check(firstTransport.Inner.FlagWrites == 4 && secondTransport.Inner.FlagWrites == 0,
                "Valid owned route could not dispatch without affecting the other source");
        } finally {
            await first.ShutdownAsync();
            await second.ShutdownAsync();
        }
    }

    public static async Task ShutdownRetainsTheTransportUntilAnAdmittedCohortReadHasEnded() {
        var transport = new CohortRetailTransport();
        var session = Create(transport, TimeSpan.FromMilliseconds(30));
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        var state = session.ReadMenuState();
        Check(state.CanAllPlayersGodmode, "Positive group fixture was unavailable");
        using var released = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnCohortRead = () => {
            entered.TrySetResult();
            if (!released.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Cohort source was not released");
        };
        var action = session.ExecuteAsync(Toggle(state));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var retained = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Check(!retained.Stopped && !transport.Inner.Disposed, "An in-flight cohort source was released");
        } finally {
            released.Set();
            await action;
            transport.OnCohortRead = null;
        }
        Check(transport.Inner.FlagWrites == 0, "A closing cohort source enabled flags");
        session.AllowShutdownRetry();
        Check((await session.ShutdownAsync()).Stopped && transport.Inner.Disposed, "Joined original source could not finish cleanup");
    }
}
