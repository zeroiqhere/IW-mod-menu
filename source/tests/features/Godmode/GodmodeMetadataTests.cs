namespace Features.Tests.Godmode;

internal static class GodmodeMetadataTests {
    public static async Task CacheEpochExhaustionCannotPreventOriginalOwnedCleanupOnClose() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        TestAssert.Equal(true, (await fixture.Registry.SetCohortAsync(capture, true, fixture.Read)).Succeeded);
        // Only the cache boundary changes; the actual owned flag primitive remains intact
        var revision = typeof(syndishanx_iw_tool.Features.Godmode.PlayerGodmodeRegistry)
            .GetField("metadataRevision", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The cache epoch field was not found");
        revision.SetValue(fixture.Registry, long.MaxValue);
        var before = fixture.Registry.ReadMetadataStamp(capture.Identities);
        var closed = await fixture.Registry.CloseAsync();
        TestAssert.Equal(true, closed.SafeToRelease);
        TestAssert.Equal(false, before.Matches(fixture.Registry.ReadMetadataStamp(capture.Identities)));
        TestAssert.Equal(true, fixture.Flags.Values.All(flags => flags.Flags == 0x200));
    }

    public static async Task ASharedRoundTripInvalidatesAnEarlierOffReceiptEvenWithIdenticalFinalFlags() {
        await using var fixture = new CohortGodmodeFixture();
        var identities = fixture.Capture.Identities;
        var first = await fixture.Registry.CaptureAsync(identities);
        TestAssert.Equal(true, first.Succeeded);
        TestAssert.Equal(true, first.OwnerMetadata!.Matches(fixture.Registry.ReadMetadataStamp(identities)));
        TestAssert.Equal(true, (await fixture.Registry.SetAsync(identities[0], true)).Succeeded);
        TestAssert.Equal(true, (await fixture.Registry.SetAsync(identities[0], false)).Succeeded);
        // Repeated values do not revive an older action receipt after another owner command
        TestAssert.Equal(false, first.OwnerMetadata.Matches(fixture.Registry.ReadMetadataStamp(identities)));
    }

    public static async Task ReadingOwnerMetadataDoesNotReadFlagsOrInvokeIdentityValidation() {
        await using var fixture = new CohortGodmodeFixture();
        var identities = fixture.Capture.Identities;
        var captured = await fixture.Registry.CaptureAsync(identities);
        TestAssert.Equal(true, captured.Succeeded);
        foreach (var flags in fixture.Flags.Values) flags.OnRead = _ => throw new IOException("Metadata invoked a native read");
        fixture.OnValidate = _ => throw new IOException("Metadata invoked native identity validation");
        for (int index = 0; index < 20; index++)
            TestAssert.Equal(true, captured.OwnerMetadata!.Matches(fixture.Registry.ReadMetadataStamp(identities)));
        foreach (var flags in fixture.Flags.Values) flags.OnRead = null;
        fixture.OnValidate = null;
    }

    public static async Task ASharedCaptureInFlightMasksAnOlderMetadataReceipt() {
        await using var fixture = new CohortGodmodeFixture();
        var identities = fixture.Capture.Identities;
        var captured = await fixture.Registry.CaptureAsync(identities);
        using var released = new ManualResetEventSlim();
        var entered = TestAssert.Barrier();
        fixture.Flags[identities[0]].OnRead = _ => {
            entered.TrySetResult();
            if (!released.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Capture was not released");
            return fixture.Flags[identities[0]].Flags;
        };
        Task<syndishanx_iw_tool.Features.Godmode.GodmodeOperationResult> pending = Task.Run(() => fixture.Registry.CaptureAsync(identities));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            TestAssert.Equal(false, captured.OwnerMetadata!.Matches(fixture.Registry.ReadMetadataStamp(identities)));
        } finally {
            released.Set();
            await pending;
            fixture.Flags[identities[0]].OnRead = null;
        }
    }
}
