using syndishanx_iw_tool.Features;

namespace Features.Tests;

internal static class InfiniteAmmoFeatureTests {
    private static InfiniteAmmoFeature Create(RecordingTransport transport) =>
        new(transport, new byte[] { 1, 2, 3, 4 }, new byte[] { 9, 9, 9, 9 });

    public static async Task EnableAndDisableRestoreOnlyOriginalWidthWithoutRescanning() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(1, transport.AmmoWrites.Count);
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(1, transport.Scans);
        TestAssert.Equal(2, transport.AmmoWrites.Count);
        TestAssert.Equal(4, transport.AmmoWrites[1].Bytes.Length);
        TestAssert.Equal(transport.AmmoWrites[0].Candidate, transport.AmmoWrites[1].Candidate);
        TestAssert.BytesEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, transport.Bytes);
    }

    public static async Task MissingDuplicateAndInvalidCandidatesNeverWrite() {
        foreach (var candidates in new IReadOnlyList<long>[] {
            Array.Empty<long>(), new long[] { 1, 1 }, new long[] { 1, 2 }, new long[] { 0 }, new long[] { -1 }
        }) {
            var transport = new RecordingTransport { Candidates = candidates };
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(0, transport.AmmoWrites.Count);
            TestAssert.Equal(0, transport.AmmoReads);
        }
    }

    public static async Task UnexpectedOriginalAndStalePrewriteBytesNeverWrite() {
        foreach (int changedRead in new[] { 1, 2 }) {
            var transport = new RecordingTransport {
                ReadOverride = count => count == changedRead ? new byte[] { 7, 7, 7, 7 } : null
            };
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(0, transport.AmmoWrites.Count);
        }
    }

    public static async Task FailedAndUnconfirmedWritesNeverReportEnabled() {
        foreach (bool rejectWrite in new[] { true, false }) {
            var transport = new RecordingTransport {
                AcceptAmmoWrite = !rejectWrite,
                ApplyAmmoWrite = false
            };
            await using var feature = Create(transport);
            TestAssert.Equal(false, await feature.SetAsync(true));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(true, feature.HasPendingRestore);
            TestAssert.Equal(true, feature.FailureReason is not null);
        }
    }

    public static async Task ExternalReplacementBeforeFirstWriteIsNotOwnedOrRestored() {
        var transport = new RecordingTransport();
        transport.ReadOverride = count => {
            if (count == 2) {
                // Simulate another owner changing the bytes after the first read
                new byte[] { 9, 9, 9, 9 }.CopyTo(transport.Bytes, 0);
            }
            return null;
        };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(0, transport.AmmoWrites.Count);
        TestAssert.BytesEqual(new byte[] { 9, 9, 9, 9 }, transport.Bytes.Take(4).ToArray());
    }

    public static async Task FailedConfirmationRetainsOriginalForSafeCleanup() {
        var transport = new RecordingTransport {
            ReadOverride = count => count == 3 ? Array.Empty<byte>() : null
        };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(1, transport.Scans);
        TestAssert.Equal(2, transport.AmmoWrites.Count);
        TestAssert.BytesEqual(new byte[] { 1, 2, 3, 4 }, transport.AmmoWrites[1].Bytes);
    }

    public static async Task DisableRefusesMismatchedBytesAndDisconnectedBackend() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        await feature.SetAsync(true);
        transport.Bytes[0] = 7;
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(1, transport.AmmoWrites.Count);
        transport.Connected = false;
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(1, transport.AmmoWrites.Count);
        TestAssert.Equal(true, feature.HasPendingRestore);
    }

    public static async Task DisconnectedEnableAndCancelledEnableNeverWrite() {
        var transport = new RecordingTransport { Connected = false };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(0, transport.Scans);
        transport.Connected = true;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        TestAssert.Equal(false, await feature.SetAsync(true, cancellation.Token));
        TestAssert.Equal(0, transport.Scans);
        TestAssert.Equal(0, transport.AmmoWrites.Count);
    }

    public static async Task ConstructorCopiesInputsAndCloseRestoresThenRejectsChanges() {
        var transport = new RecordingTransport();
        byte[] original = { 1, 2, 3, 4 };
        byte[] replacement = { 9, 9, 9, 9 };
        await using var feature = new InfiniteAmmoFeature(transport, original, replacement);
        original[0] = 8;
        replacement[0] = 8;
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(2, transport.AmmoWrites.Count);
        TestAssert.BytesEqual(new byte[] { 1, 2, 3, 4 }, transport.Bytes.Take(4).ToArray());
    }

    public static async Task RepeatedOnOffCyclesUseOneValidatedLocationAndRestoreEveryTime() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        // Later discovery results cannot change the recorded location
        transport.Candidates = Array.Empty<long>();
        for (int cycle = 0; cycle < 4; cycle++) {
            TestAssert.Equal(true, await feature.SetAsync(false));
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(false, feature.HasPendingRestore);
            TestAssert.BytesEqual(new byte[] { 1, 2, 3, 4 }, transport.Bytes.Take(4).ToArray());
            TestAssert.Equal(true, await feature.SetAsync(true));
            TestAssert.Equal(true, feature.Active);
            TestAssert.BytesEqual(new byte[] { 9, 9, 9, 9 }, transport.Bytes.Take(4).ToArray());
        }
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(1, transport.Scans);
        TestAssert.Equal(10, transport.AmmoWrites.Count);
    }

    public static async Task ExternalPatchBetweenCyclesIsNeitherAdoptedNorRestored() {
        var transport = new RecordingTransport();
        await using var feature = Create(transport);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, await feature.SetAsync(false));
        // Another owner changed the restored state before the next requested enable
        new byte[] { 9, 9, 9, 9 }.CopyTo(transport.Bytes, 0);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(2, transport.AmmoWrites.Count);
        TestAssert.BytesEqual(new byte[] { 9, 9, 9, 9 }, transport.Bytes.Take(4).ToArray());
    }

    public static async Task ExistingUnownedPatchRequiresRestartWithoutAnyWritesOrRetries() {
        var transport = new RecordingTransport { ScanFailure = new ExistingAmmoPatchException() };
        await using var feature = Create(transport);
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.RequiresGameRestart);
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(true, feature.FailureReason!.Contains("Fully exit Infinite Warfare"));
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(1, transport.Scans);
        // Stopping an unowned feature must not erase its restart explanation
        TestAssert.Equal(true, await feature.SetAsync(false));
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(true, feature.FailureReason!.Contains("Fully exit Infinite Warfare"));
        TestAssert.Equal(true, await feature.CloseAsync());
        TestAssert.Equal(0, transport.AmmoReads);
        TestAssert.Equal(0, transport.AmmoWrites.Count);
    }

    public static async Task ChangedTrailingContextAfterDisablePreventsAnotherWrite() {
        var transport = new RecordingTransport();
        await using var feature = new InfiniteAmmoFeature(transport, [1, 2, 3, 4],
            [9, 9, 9, 9], [5, 6, 7, 8, 9]);
        TestAssert.Equal(true, await feature.SetAsync(true));
        TestAssert.Equal(true, await feature.SetAsync(false));
        transport.Bytes[4] = 99;
        TestAssert.Equal(false, await feature.SetAsync(true));
        TestAssert.Equal(false, feature.Active);
        TestAssert.Equal(false, feature.HasPendingRestore);
        TestAssert.Equal(2, transport.AmmoWrites.Count);
        TestAssert.Equal(1, transport.Scans);
        TestAssert.Equal(true, feature.FailureReason!.Contains("instruction context"));
    }

    public static async Task ChangedTrailingContextBeforeDisablePreventsRestoration() {
        var transport = new RecordingTransport();
        await using var feature = new InfiniteAmmoFeature(transport, [1, 2, 3, 4],
            [9, 9, 9, 9], [5, 6, 7, 8, 9]);
        TestAssert.Equal(true, await feature.SetAsync(true));
        transport.Bytes[4] = 99;
        TestAssert.Equal(false, await feature.SetAsync(false));
        TestAssert.Equal(true, feature.HasPendingRestore);
        TestAssert.Equal(1, transport.AmmoWrites.Count);
        TestAssert.Equal(false, await feature.CloseAsync());
    }

    public static async Task ContextInputsAreCopiedAndNormalCyclesWriteOnlyOriginalWidth() {
        var transport = new RecordingTransport();
        byte[] context = [5, 6, 7, 8, 9];
        await using var feature = new InfiniteAmmoFeature(transport, [1, 2, 3, 4],
            [9, 9, 9, 9], context);
        context[0] = 99;
        for (int cycle = 0; cycle < 3; cycle++) {
            TestAssert.Equal(true, await feature.SetAsync(true));
            TestAssert.Equal(true, await feature.SetAsync(false));
        }
        TestAssert.Equal(1, transport.Scans);
        TestAssert.Equal(6, transport.AmmoWrites.Count);
        TestAssert.Equal(true, transport.AmmoWrites.All(write => write.Bytes.Length == 4));
        TestAssert.BytesEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, transport.Bytes);
    }
}
