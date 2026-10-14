using Apparition.Backend.Runtime;

namespace Backend.Tests;

internal static class SessionObservationPollingTests {
    public static void PollingStartsOneReadAndCoalescesUntilItsRealCompletion() {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var polling = new SessionObservationPolling(() => ++calls == 1 ? first.Task : Task.CompletedTask);
        polling.Tick(true);
        for (int index = 0; index < 32; index++) polling.Tick(true);
        TestAssert.Equal(1, calls);
        // A completed read allows exactly one fresh request on the next owner-loop tick
        first.SetResult();
        polling.Tick(true);
        TestAssert.Equal(2, calls);
        polling.DrainAsync().GetAwaiter().GetResult();
    }

    public static void DisabledPollingObservesCompletionWithoutStartingAnotherRead() {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var polling = new SessionObservationPolling(() => { calls++; return pending.Task; });
        polling.Tick(false);
        TestAssert.Equal(0, calls);
        polling.Tick(true);
        polling.Tick(false);
        TestAssert.Equal(1, calls);
        pending.SetResult();
        polling.Tick(false);
        TestAssert.Equal(1, calls);
        // A temporary pause does not permanently retire the single owner-loop supervisor
        polling.Tick(true);
        TestAssert.Equal(2, calls);
        polling.DrainAsync().GetAwaiter().GetResult();
    }

    public static void CompletedFailureIsObservedOnceAndRetiresFurtherPolling() {
        int calls = 0;
        var failure = new IOException("Owned observation failed");
        var polling = new SessionObservationPolling(() => { calls++; return Task.FromException(failure); });
        polling.Tick(true);
        TestAssert.Throws<IOException>(() => polling.Tick(false));
        polling.Tick(true);
        TestAssert.Equal(1, calls);
        // Draining must not rethrow a failure already consumed by the loop
        polling.DrainAsync().GetAwaiter().GetResult();
    }

    public static void SynchronousFailureAndNullTaskRetireFurtherPolling() {
        foreach (bool nullTask in new[] { false, true }) {
            int calls = 0;
            var polling = new SessionObservationPolling(() => {
                calls++;
                if (nullTask) return null!;
                throw new InvalidOperationException("Owned source refused admission");
            });
            TestAssert.Throws<InvalidOperationException>(() => polling.Tick(true));
            polling.Tick(true);
            TestAssert.Equal(1, calls);
            polling.DrainAsync().GetAwaiter().GetResult();
        }
    }

    public static void DrainJoinsLastReadAndPermanentlyStopsAdmission() {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var polling = new SessionObservationPolling(() => { calls++; return pending.Task; });
        polling.Tick(true);
        Task drain = polling.DrainAsync();
        TestAssert.True(!drain.IsCompleted, "A live read must remain joined by the drain");
        TestAssert.True(ReferenceEquals(drain, polling.DrainAsync()), "Repeated drains must share one completion");
        polling.Tick(true);
        TestAssert.Equal(1, calls);
        // Release the owned barrier rather than relying on a timing race
        pending.SetResult();
        drain.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        polling.Tick(true);
        TestAssert.Equal(1, calls);
    }

    public static void DrainObservesFailureAndRejectsMissingReader() {
        TestAssert.Throws<ArgumentNullException>(() => new SessionObservationPolling(null!));
        int calls = 0;
        var polling = new SessionObservationPolling(() => { calls++; return Task.FromException(new IOException("Owned failure")); });
        polling.Tick(true);
        TestAssert.Throws<IOException>(() => polling.DrainAsync().GetAwaiter().GetResult());
        polling.Tick(true);
        TestAssert.Equal(1, calls);
    }
}
