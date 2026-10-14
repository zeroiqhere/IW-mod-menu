using syndishanx_iw_tool.Diagnostics;

namespace Session.Tests;

// The real history is shared by action workers and publication readers
internal static class MessageHistoryTests {
    public static Task AppendKeepsNewestBoundedMessagesAndSnapshotsCannotChangeRetainedEntries() {
        var history = new MessageHistory(2, 5);
        history.Append("first");
        history.Append("second");
        var copied = history.Entries;
        history.Append("third");
        Check(copied.SequenceEqual(new[] { "first", "secon" }), "A copied frame changed after append");
        Check(history.Entries.SequenceEqual(new[] { "secon", "third" }), "Oldest entries were not removed");
        // Mutating a returned copy cannot modify the owner's queue
        if (copied is string[] array) array[0] = "changed";
        Check(history.Entries.SequenceEqual(new[] { "secon", "third" }), "A reader changed retained history");
        return Task.CompletedTask;
    }

    public static Task RenderPreservesBlankLineSeparationAndReturnsEmptyForEmptyHistory() {
        var history = new MessageHistory();
        Check(history.Render() == "", "An empty history gained text");
        history.Append("one");
        history.Append("two");
        Check(history.Render() == "one" + Environment.NewLine + Environment.NewLine + "two",
            "Rendered history changed its entry separators");
        return Task.CompletedTask;
    }

    public static Task InvalidLimitsAndNullAppendRejectWithoutChangingHistory() {
        Reject<ArgumentOutOfRangeException>(() => new MessageHistory(0, 1));
        Reject<ArgumentOutOfRangeException>(() => new MessageHistory(1, 0));
        Reject<ArgumentOutOfRangeException>(() => new MessageHistory(-1, 1));
        Reject<ArgumentOutOfRangeException>(() => new MessageHistory(1, -1));
        var history = new MessageHistory();
        history.Append("retained");
        Reject<ArgumentNullException>(() => history.Append(null!));
        Check(history.Entries.SequenceEqual(new[] { "retained" }), "A rejected append changed history");
        return Task.CompletedTask;
    }

    public static Task TruncationNeverSplitsACompleteSurrogatePairAtTheCharacterLimit() {
        var history = new MessageHistory(4, 2);
        history.Append("a\U0001F642tail");
        history.Append("\U0001F642tail");
        history.Append("ab\U0001F642");
        Check(history.Entries.SequenceEqual(new[] { "a", "\U0001F642", "ab" }),
            "Truncation split a complete Unicode character");
        var narrow = new MessageHistory(1, 1);
        narrow.Append("\U0001F642");
        Check(narrow.Entries.Single() == "", "A one-unit limit retained half of a Unicode character");
        return Task.CompletedTask;
    }

    public static async Task ConcurrentAppendSnapshotAndRenderStayBoundedWithoutMissingOrInvalidEntries() {
        var history = new MessageHistory(30, 40);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] workers = Enumerable.Range(0, 8).Select(worker => Task.Run(async () => {
            // Awaiting the barrier leaves thread-pool workers available for the readers
            await start.Task.ConfigureAwait(false);
            for (int index = 0; index < 12000; index++) {
                if (worker < 4) {
                    history.Append($"writer:{worker}:{index}");
                } else {
                    var snapshot = history.Entries;
                    Check(snapshot.Count <= 30, "A frame exceeded the configured entry bound");
                    foreach (string entry in snapshot) CheckCommitted(entry);
                    string rendered = history.Render();
                    if (rendered.Length != 0) {
                        foreach (string entry in rendered.Split(Environment.NewLine + Environment.NewLine))
                            CheckCommitted(entry);
                    }
                }
                // Regular yields exercise overlap without a time-based correctness assertion
                if (index % 128 == 0) await Task.Yield();
            }
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(15));
        Check(history.Entries.Count == 30, "Concurrent writers lost the bounded final history");
    }

    private static void CheckCommitted(string? entry) {
        Check(entry is not null, "A snapshot contained an uncommitted null entry");
        string[] parts = entry!.Split(':');
        Check(parts.Length == 3 && parts[0] == "writer"
            && int.TryParse(parts[1], out int worker) && worker is >= 0 and < 4
            && int.TryParse(parts[2], out int index) && index is >= 0 and < 12000,
            "A snapshot contained an invalid committed message");
    }

    private static void Reject<T>(Action action) where T : Exception {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }

    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
