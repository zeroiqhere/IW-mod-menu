namespace Features.Tests;

internal static class TestAssert {
    public static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new InvalidOperationException($"Expected {expected}, received {actual}");
        }
    }

    public static void BytesEqual(byte[] expected, byte[] actual) {
        if (!expected.SequenceEqual(actual)) {
            throw new InvalidOperationException("The synthetic bytes did not match");
        }
    }

    public static async Task WaitUntilAsync(Func<bool> condition) {
        // Poll only synthetic state and fail promptly if the loop never stops
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) {
            await Task.Delay(1, timeout.Token);
        }
    }

    public static TaskCompletionSource Barrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
