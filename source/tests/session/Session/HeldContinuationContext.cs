using System.Collections.Concurrent;

namespace Session.Tests;

// A held continuation models a delayed worker without replacing a feature implementation
internal sealed class HeldContinuationContext : SynchronizationContext {
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> pending = new();
    private readonly SemaphoreSlim posted = new(0);
    public override void Post(SendOrPostCallback callback, object? state) {
        pending.Enqueue((callback, state));
        posted.Release();
    }
    public Task Start(Func<Task> action) {
        var previous = Current;
        SetSynchronizationContext(this);
        try { return action(); }
        finally { SetSynchronizationContext(previous); }
    }
    public async Task WaitForPostAsync() {
        await posted.WaitAsync(TimeSpan.FromSeconds(5));
        if (pending.IsEmpty) throw new Exception("The real worker did not reach its held continuation");
    }
    public async Task DrainAsync(Task completion) {
        while (!completion.IsCompleted) {
            if (!pending.TryDequeue(out var work)) {
                if (!await posted.WaitAsync(TimeSpan.FromSeconds(5))) throw new Exception("Worker continuation did not complete");
                continue;
            }
            var previous = Current;
            SetSynchronizationContext(this);
            try { work.Callback(work.State); }
            finally { SetSynchronizationContext(previous); }
        }
        await completion;
    }
}
