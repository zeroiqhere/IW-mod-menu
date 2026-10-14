namespace Apparition.Backend.Runtime;

// The owner loop supervises reads while the session retains all resource authority
internal sealed class SessionObservationPolling {
    private readonly Func<Task> read;
    private Task? pending;
    private Task? drain;
    private bool retired;

    public SessionObservationPolling(Func<Task> read) {
        ArgumentNullException.ThrowIfNull(read);
        this.read = read;
    }

    public void Tick(bool enabled) {
        // One loop owns this supervisor; provider completion does not mutate its fields
        if (retired) return;
        if (pending is not null) {
            if (!pending.IsCompleted) return;
            Task completed = pending;
            pending = null;
            try {
                // Pausing admission still observes a completed worker's failure
                completed.GetAwaiter().GetResult();
            } catch {
                retired = true;
                throw;
            }
        }
        if (!enabled) return;
        try {
            pending = read() ?? throw new InvalidOperationException("The observation source returned no task");
        } catch {
            // A failed source cannot begin another read after the loop starts cleanup
            retired = true;
            throw;
        }
    }

    public Task DrainAsync() {
        retired = true;
        // Repeated cleanup callers await the same captured last read
        return drain ??= DrainCoreAsync();
    }

    private async Task DrainCoreAsync() {
        Task? lastRead = pending;
        pending = null;
        if (lastRead is not null) await lastRead.ConfigureAwait(false);
    }
}
