using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Session;

// These fixtures bind the production flag feature to owned in-memory transports
internal static class TeammateGodmodeSessionTests {
    private sealed class Fixture : IAsyncDisposable {
        public Dictionary<int, PlayerIdentity> Players { get; } = Enumerable.Range(1, 3)
            .ToDictionary(slot => slot, slot => new PlayerIdentity(slot, 0x10000 + slot * 1024,
                0x20000 + slot * 4096, 0x30000, 4, "Player" + slot, "Guid" + slot));
        public Dictionary<int, RecordingFlagsTransport> Transports { get; } = Enumerable.Range(1, 3)
            .ToDictionary(slot => slot, _ => new RecordingFlagsTransport());
        public int Factories { get; private set; }
        public int Reads { get; private set; }
        public int Health { get; set; } = 100;
        public TeammateGodmodeSession Session { get; }

        public Fixture(TimeSpan? interval = null, Func<IPlayerGodmodeFeature, IPlayerGodmodeFeature>? decorate = null) {
            Session = new(slot => {
                Reads++;
                return new(Players[slot], 100, Health, Transports[slot].Flags);
            }, identity => {
                Factories++;
                IPlayerGodmodeFeature feature = new FlagsPlayerGodmodeFeature(Transports[identity.Slot], interval ?? TimeSpan.FromSeconds(10));
                return decorate is null ? feature : decorate(feature);
            });
        }
        public string Name(int slot) => Players[slot].Name;
        public async ValueTask DisposeAsync() {
            // Remove owned-fixture failures before joining its real monitor tasks
            foreach (var transport in Transports.Values) { transport.OnRead = null; transport.OnWrite = null; }
            await Session.CloseAsync();
        }
    }

    public static async Task SwitchingSelectionRetainsBothOwnersAndReturnsToTheActualOriginalState() {
        await using var fixture = new Fixture();
        var session = fixture.Session;
        TestAssert.Equal(true, await session.SelectAsync(1, fixture.Name(1)));
        TestAssert.Equal(0, fixture.Transports[1].Writes);
        TestAssert.Equal(GodmodeValue.Off, session.ReadView().Value);
        TestAssert.Equal(true, (await session.ToggleAsync(1, fixture.Name(1)))!.Succeeded);
        TestAssert.Equal(true, await session.SelectAsync(2, fixture.Name(2)));
        TestAssert.Equal(0x201U, fixture.Transports[1].Flags);
        TestAssert.Equal(true, (await session.ToggleAsync(2, fixture.Name(2)))!.Succeeded);
        TestAssert.Equal(true, await session.SelectAsync(1, fixture.Name(1)));
        TestAssert.Equal(GodmodeValue.On, session.ReadView().Value);
        TestAssert.Equal(2, fixture.Factories);
        TestAssert.Equal(true, (await session.ToggleAsync(1, fixture.Name(1)))!.Succeeded);
        TestAssert.Equal(0x200U, fixture.Transports[1].Flags);
        TestAssert.Equal(0x201U, fixture.Transports[2].Flags);
        TestAssert.Equal(true, (await session.CloseAsync()).SafeToRelease);
        TestAssert.Equal(0x200U, fixture.Transports[2].Flags);
    }

    public static async Task WrongNamesAndSlotPayloadsNeverRetargetTheSelectedOwner() {
        await using var fixture = new Fixture();
        TestAssert.Equal(true, await fixture.Session.SelectAsync(1, fixture.Name(1)));
        foreach (int slot in new[] { -1, 0, 4, int.MaxValue })
            TestAssert.Equal(false, await fixture.Session.SelectAsync(slot, "Player1"));
        TestAssert.Equal(false, await fixture.Session.SelectAsync(2, "Wrong"));
        TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(2, fixture.Name(2)));
        TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(1, "Wrong"));
        TestAssert.Equal(1, fixture.Session.ReadView().Identity!.Slot);
        TestAssert.Equal(0, fixture.Transports.Values.Sum(transport => transport.Writes));
    }

    public static async Task SameSlotAndNameWithChangedFullIdentityDisableTheViewAndRejectActions() {
        await using var fixture = new Fixture();
        // Retain the original value so fixture teardown can restore only its owned flags
        var original = fixture.Players[1];
        await fixture.Session.SelectAsync(1, original.Name);
        await fixture.Session.ToggleAsync(1, original.Name);
        // The same name does not preserve storage, connection generation or GUID
        foreach (var replacement in new[] { original with { Client = original.Client + 8 },
            original with { Entity = original.Entity + 8 }, original with { Guid = "Replacement" },
            original with { ServerClients = original.ServerClients + 8 } }) {
            fixture.Players[1] = replacement;
            await fixture.Session.RefreshAsync();
            TestAssert.Equal(false, fixture.Session.ReadView().CanToggle);
            TestAssert.Equal(GodmodeValue.Unknown, fixture.Session.ReadView().Value);
            TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(1, original.Name));
            TestAssert.Equal(1, fixture.Transports[1].Writes);
        }
        fixture.Players[1] = original;
    }

    public static async Task ForeignGodFlagNeverBecomesABorrowedOffOrOnSelection() {
        await using var fixture = new Fixture();
        fixture.Transports[1].Flags = 0x201;
        TestAssert.Equal(false, await fixture.Session.SelectAsync(1, fixture.Name(1)));
        TestAssert.Equal(GodmodeValue.Unknown, fixture.Session.ReadView().Value);
        TestAssert.Equal(false, fixture.Session.ReadView().CanToggle);
        TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(1, fixture.Name(1)));
        TestAssert.Equal(0, fixture.Transports[1].Writes);
        TestAssert.Equal(0x201U, fixture.Transports[1].Flags);
    }

    public static async Task AForeignFlagAfterReadOnlySelectionCannotKeepAnOffDisplayOrEnableControl() {
        await using var fixture = new Fixture();
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        TestAssert.Equal(GodmodeValue.Off, fixture.Session.ReadView().Value);
        // An OFF selection is checked by the owned refresh rather than a render frame
        fixture.Transports[1].Flags = 0x201;
        await fixture.Session.RefreshAsync();
        var changed = fixture.Session.ReadView();
        TestAssert.Equal(GodmodeValue.Unknown, changed.Value);
        TestAssert.Equal(false, changed.CanToggle);
        TestAssert.Equal(0, fixture.Transports[1].Writes);
    }

    public static async Task FailedSelectionKeepsThePreviouslyActiveIdentityAndOwner() {
        await using var fixture = new Fixture();
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        await fixture.Session.ToggleAsync(1, fixture.Name(1));
        fixture.Transports[2].Flags = 0x201;
        TestAssert.Equal(false, await fixture.Session.SelectAsync(2, fixture.Name(2)));
        TestAssert.Equal(1, fixture.Session.ReadView().Identity!.Slot);
        TestAssert.Equal(GodmodeValue.On, fixture.Session.ReadView().Value);
        TestAssert.Equal(0x201U, fixture.Transports[1].Flags);
        TestAssert.Equal(1, fixture.Transports[1].Writes);
        TestAssert.Equal(0, fixture.Transports[2].Writes);
    }

    public static async Task PendingOriginalCleanupBlocksSelectionAndAllowsOnlyTheOriginalOffRetry() {
        await using var fixture = new Fixture();
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        await fixture.Session.ToggleAsync(1, fixture.Name(1));
        fixture.Transports[1].OnWrite = (_, _, _) => false;
        TestAssert.Equal(false, (await fixture.Session.ToggleAsync(1, fixture.Name(1)))!.Succeeded);
        var view = fixture.Session.ReadView();
        TestAssert.Equal(true, view.HasPendingCleanup);
        TestAssert.Equal(false, view.CanSelect);
        TestAssert.Equal(true, view.CanToggle);
        TestAssert.Equal(false, await fixture.Session.SelectAsync(2, fixture.Name(2)));
        TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(2, fixture.Name(2)));
        fixture.Transports[1].OnWrite = null;
        TestAssert.Equal(true, (await fixture.Session.ToggleAsync(1, fixture.Name(1)))!.Succeeded);
        TestAssert.Equal(0x200U, fixture.Transports[1].Flags);
        TestAssert.Equal(false, fixture.Session.ReadView().HasPendingCleanup);
    }

    public static async Task ReadViewsNeverCreateAdaptersOrWriteAndCapturedViewsRemainImmutable() {
        await using var fixture = new Fixture();
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        var original = fixture.Session.ReadView();
        // Repeated publication must not capture or manufacture new feature instances
        for (int count = 0; count < 10; count++) fixture.Session.ReadView();
        TestAssert.Equal(1, fixture.Factories);
        TestAssert.Equal(0, fixture.Transports[1].Writes);
        await fixture.Session.ToggleAsync(1, fixture.Name(1));
        TestAssert.Equal(GodmodeValue.Off, original.Value);
        TestAssert.Equal(GodmodeValue.On, fixture.Session.ReadView().Value);
    }

    public static async Task NonLivingSelectedStateRejectsActionsWithoutAFlagWrite() {
        await using var fixture = new Fixture();
        fixture.Health = 1;
        TestAssert.Equal(false, await fixture.Session.SelectAsync(1, fixture.Name(1)));
        TestAssert.Equal(0, fixture.Factories);
        fixture.Health = 100;
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        fixture.Health = 1;
        await fixture.Session.RefreshAsync();
        TestAssert.Equal(false, fixture.Session.ReadView().CanToggle);
        TestAssert.Equal<GodmodeOperationResult?>(null, await fixture.Session.ToggleAsync(1, fixture.Name(1)));
        TestAssert.Equal(0, fixture.Transports[1].Writes);
    }

    public static async Task ConcurrentReadViewsRemainBoundToIdentityAndDisableControlsAfterCloseStarts() {
        HeldCloseFeature? held = null;
        await using var fixture = new Fixture(decorate: feature => held = new HeldCloseFeature(feature));
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        await fixture.Session.ToggleAsync(1, fixture.Name(1));
        var firstView = TestAssert.Barrier();
        var afterClosing = TestAssert.Barrier();
        var reader = Task.Run(async () => {
            // Publication can overlap shutdown without sharing mutable selection or adapter fields
            for (int count = 0; count < 100; count++) {
                var view = fixture.Session.ReadView();
                if (view.Value == GodmodeValue.On) TestAssert.Equal(fixture.Players[1], view.Identity);
            }
            firstView.TrySetResult();
            await afterClosing.Task;
            for (int count = 0; count < 1000; count++) {
                var view = fixture.Session.ReadView();
                TestAssert.Equal(false, view.CanToggle);
                TestAssert.Equal(false, view.CanSelect);
                if (view.Value == GodmodeValue.On) TestAssert.Equal(fixture.Players[1], view.Identity);
            }
        });
        try {
            await firstView.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Task<GodmodeRegistryCloseResult> closing = fixture.Session.CloseAsync();
            await held!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // This barrier proves every later view overlaps an unfinished original cleanup
            TestAssert.Equal(false, closing.IsCompleted);
            afterClosing.TrySetResult();
            await reader.WaitAsync(TimeSpan.FromSeconds(3));
            TestAssert.Equal(false, closing.IsCompleted);
            held.Release.TrySetResult();
            TestAssert.Equal(true, (await closing.WaitAsync(TimeSpan.FromSeconds(3))).SafeToRelease);
            TestAssert.Equal(0x200U, fixture.Transports[1].Flags);
        } finally {
            afterClosing.TrySetResult();
            held?.Release.TrySetResult();
        }
    }

    public static async Task CloseJoinsTheRealMonitorAndRejectsQueuedSelectionAndToggle() {
        await using var fixture = new Fixture(TimeSpan.FromMilliseconds(1));
        await fixture.Session.SelectAsync(1, fixture.Name(1));
        await fixture.Session.ToggleAsync(1, fixture.Name(1));
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        fixture.Transports[1].OnRead = _ => {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return fixture.Transports[1].Flags;
        };
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // The blocked real monitor proves shutdown cannot release its owner early
            Task<GodmodeRegistryCloseResult> close = fixture.Session.CloseAsync();
            TestAssert.Equal(false, close.IsCompleted);
            var select = fixture.Session.SelectAsync(2, fixture.Name(2));
            var toggle = fixture.Session.ToggleAsync(1, fixture.Name(1));
            fixture.Transports[1].OnRead = null;
            release.TrySetResult();
            TestAssert.Equal(true, (await close.WaitAsync(TimeSpan.FromSeconds(3))).SafeToRelease);
            TestAssert.Equal(false, await select);
            TestAssert.Equal<GodmodeOperationResult?>(null, await toggle);
            TestAssert.Equal(0x200U, fixture.Transports[1].Flags);
            TestAssert.Equal(1, fixture.Factories);
        } finally {
            // An assertion failure must not leave the owned fake read blocking fixture teardown
            fixture.Transports[1].OnRead = null;
            release.TrySetResult();
        }
    }
}
