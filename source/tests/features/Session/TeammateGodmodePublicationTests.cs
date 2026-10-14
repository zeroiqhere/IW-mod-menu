using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Session;

// Frame reads must remain cheap even when a native reader would block
internal static class TeammateGodmodePublicationTests {
    public static async Task RepeatedFramesNeverReadTheSelectedPlayerOrFlags() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        int reads = 0;
        var session = new TeammateGodmodeSession(_ => {
            reads++;
            return new(identity, 100, 100, transport.Flags);
        }, _ => new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        try {
            TestAssert.Equal(true, await session.SelectAsync(1, identity.Name));
            int sourceReads = reads, flagReads = transport.Reads;
            for (int index = 0; index < 1000; index++) {
                var view = session.ReadView();
                TestAssert.Equal(GodmodeValue.Off, view.Value);
                TestAssert.Equal(true, view.CanToggle);
            }
            TestAssert.Equal(sourceReads, reads);
            TestAssert.Equal(flagReads, transport.Reads);
        } finally { await session.CloseAsync(); }
    }
    public static async Task ABlockedRefreshNeverBlocksFramesAndCloseJoinsTheOriginalRead() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        bool hold = false;
        int reads = 0;
        var session = new TeammateGodmodeSession(_ => {
            Interlocked.Increment(ref reads);
            if (hold) { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }
            return new(identity, 100, 100, transport.Flags);
        }, _ => new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        await session.SelectAsync(1, identity.Name);
        hold = true;
        var refreshing = session.RefreshAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Run(() => {
                for (int index = 0; index < 1000; index++) {
                    var view = session.ReadView();
                    TestAssert.Equal(false, view.CanToggle);
                    TestAssert.Equal(GodmodeValue.Unknown, view.Value);
                }
            }).WaitAsync(TimeSpan.FromSeconds(3));
            // Cancelling a queued waiter leaves the original held read and owner intact
            using var cancellation = new CancellationTokenSource();
            var queued = session.RefreshAsync(cancellation.Token);
            cancellation.Cancel();
            try { await queued; throw new InvalidOperationException("The cancelled waiter completed"); }
            catch (OperationCanceledException) { }
            var closing = session.CloseAsync();
            TestAssert.Equal(false, closing.IsCompleted);
            TestAssert.Equal(false, session.ReadView().CanSelect);
            hold = false;release.TrySetResult();
            await Task.WhenAll(refreshing, closing).WaitAsync(TimeSpan.FromSeconds(3));
            int completedReads = reads;
            await session.RefreshAsync();
            TestAssert.Equal(completedReads, reads);
            TestAssert.Equal(false, session.ReadView().HasCurrentIdentity);
            TestAssert.Equal(0, transport.Writes);
        } finally { hold = false;release.TrySetResult();await session.CloseAsync(); }
    }

    public static async Task RejectedPayloadsKeepTheCopiedTargetAndDoNotStartNativeReads() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        int reads = 0;
        var session = new TeammateGodmodeSession(_ => {
            reads++;return new(identity, 100, 100, transport.Flags);
        }, _ => new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        try {
            await session.SelectAsync(1, identity.Name);
            int sourceReads = reads, flagReads = transport.Reads;
            TestAssert.Equal(false, await session.SelectAsync(-1, identity.Name));
            TestAssert.Equal<GodmodeOperationResult?>(null, await session.ToggleAsync(2, identity.Name));
            var view = session.ReadView();
            TestAssert.Equal(identity, view.Identity);
            TestAssert.Equal(true, view.CanToggle);
            TestAssert.Equal(GodmodeValue.Off, view.Value);
            TestAssert.Equal(sourceReads, reads);
            TestAssert.Equal(flagReads, transport.Reads);
        } finally { await session.CloseAsync(); }
    }

    public static async Task AnActionBeforeRefreshRejectsAReplacementAndWithdrawsTheOldReceipt() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var current = identity;
        var transport = new RecordingFlagsTransport();
        var session = new TeammateGodmodeSession(_ => new(current, 100, 100, transport.Flags),
            _ => new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)),
            _ => new(current, 100, transport.Flags, true));
        try {
            await session.SelectAsync(1, identity.Name);
            var copied = session.ReadView();
            TestAssert.Equal(true, copied.CanToggle);
            current = identity with { Guid = "Replacement" };
            // The stale cached display cannot authorize a write on its own
            TestAssert.Equal<GodmodeOperationResult?>(null, await session.ToggleAsync(1, identity.Name));
            TestAssert.Equal(0, transport.Writes);
            current = identity;
            await session.RefreshAsync();
            TestAssert.Equal(false, session.ReadView().CanToggle);
            TestAssert.Equal(false, session.ReadView().HasCurrentIdentity);
            TestAssert.Equal(GodmodeValue.Off, copied.Value);
        } finally { await session.CloseAsync(); }
    }

    public static async Task SharedRoundTripsInvalidateCopiedPeerValuesUntilTheOwnedRefresh() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        var registry = new PlayerGodmodeRegistry(_ => new FlagsPlayerGodmodeFeature(transport,
            TimeSpan.FromSeconds(10)), _ => true);
        var session = new TeammateGodmodeSession(_ => new(identity, 100, 100, transport.Flags),
            registry, null, _ => false);
        try {
            await session.SelectAsync(1, identity.Name);
            var copied = session.ReadView();
            await registry.SetAsync(identity, true);
            await registry.SetAsync(identity, false);
            TestAssert.Equal(GodmodeValue.Unknown, session.ReadView().Value);
            TestAssert.Equal(false, session.ReadView().CanSelect);
            await session.RefreshAsync();
            TestAssert.Equal(GodmodeValue.Off, session.ReadView().Value);
            TestAssert.Equal(true, session.ReadView().CanToggle);
            TestAssert.Equal(GodmodeValue.Off, copied.Value);
        } finally { await session.CloseAsync();await registry.CloseAsync(); }
    }

    public static async Task AnotherOwnersMonitorFailureWithdrawsTheSelectedReceiptWithoutFrameReads() {
        var identities = new[] {
            new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend1", "Guid1"),
            new PlayerIdentity(2, 0x11000, 0x22000, 0x30000, 4, "Friend2", "Guid2")
        };
        var transports = new[] { new RecordingFlagsTransport(), new RecordingFlagsTransport() };
        var features = new[] {
            new FlagsPlayerGodmodeFeature(transports[0], TimeSpan.FromSeconds(10)),
            new FlagsPlayerGodmodeFeature(transports[1], TimeSpan.FromMilliseconds(1))
        };
        var registry = new PlayerGodmodeRegistry(identity => features[identity.Slot - 1], _ => true);
        var session = new TeammateGodmodeSession(slot => new(identities[slot - 1], 100, 100,
            transports[slot - 1].Flags), registry, null, _ => false);
        try {
            await session.SelectAsync(1, identities[0].Name);
            TestAssert.Equal(true, (await registry.SetAsync(identities[1], true)).Succeeded);
            await session.RefreshAsync();
            TestAssert.Equal(GodmodeValue.Off, session.ReadView().Value);
            // A different retained owner's foreign flag must revoke the shared receipt too
            transports[1].Flags = 0x401;
            await TestAssert.WaitUntilAsync(() => !features[1].Active);
            int flagReads = transports[0].Reads;
            var invalidated = session.ReadView();
            TestAssert.Equal(GodmodeValue.Unknown, invalidated.Value);
            TestAssert.Equal(false, invalidated.CanSelect);
            TestAssert.Equal(false, invalidated.HasCurrentIdentity);
            TestAssert.Equal(true, invalidated.HasPendingCleanup);
            TestAssert.Equal(flagReads, transports[0].Reads);
        } finally {
            // End the fixture connection instead of writing through a foreign flag conflict
            transports[1].Connected = false;
            await session.CloseAsync();await registry.CloseAsync();
        }
    }
    public static async Task MetadataFailureDuringAnActionReleasesItsGateForCleanupRetry() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        var feature = new FailingMetadataFeature(new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        bool armed = false;
        var session = new TeammateGodmodeSession(_ => {
            if (armed) feature.FailActive = true;
            return new(identity, 100, 100, transport.Flags);
        }, _ => feature);
        await session.SelectAsync(1, identity.Name);
        Exception? failure = null;
        try {
            armed = true;
            try { await session.ToggleAsync(1, identity.Name); }
            catch (Exception exception) { failure = exception; }
            armed = false;feature.FailActive = false;
            var closed = await session.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.Equal(true, closed.SafeToRelease);
            TestAssert.Equal<Exception?>(null, failure);
            TestAssert.Equal(0, transport.Writes);
        } finally { armed = false;feature.FailActive = false; }
    }

    public static async Task FailedMetadataReadMasksFramesAndRefreshThenRecoversWithoutAWriter() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "Guid1");
        var transport = new RecordingFlagsTransport();
        var feature = new FailingMetadataFeature(new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        var session = new TeammateGodmodeSession(_ => new(identity, 100, 100, transport.Flags), _ => feature);
        try {
            await session.SelectAsync(1, identity.Name);
            feature.FailActive = true;
            var failed = session.ReadView();
            TestAssert.Equal(GodmodeValue.Unknown, failed.Value);
            TestAssert.Equal(false, failed.CanToggle);
            TestAssert.Equal(false, failed.CanSelect);
            TestAssert.Equal(true, failed.HasPendingCleanup);
            await session.RefreshAsync();
            feature.FailActive = false;
            await session.RefreshAsync();
            TestAssert.Equal(GodmodeValue.Off, session.ReadView().Value);
            TestAssert.Equal(true, session.ReadView().CanToggle);
            TestAssert.Equal(0, transport.Writes);
        } finally { feature.FailActive = false;await session.CloseAsync(); }
    }

}
