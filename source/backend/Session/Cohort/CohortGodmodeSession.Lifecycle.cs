namespace Apparition.Backend.Session;

// Stop joins borrowed-source work before connection cleanup can release its transport
public sealed partial class CohortGodmodeSession {
    public void RequestClose() => Interlocked.Exchange(ref closing, 1);

    public async Task StopAsync() {
        RequestClose();
        Task retained;
        lock (workGate) {
            cancellationWork ??= lifetime.CancelAsync();
            retained = Task.WhenAll(work ?? Task.CompletedTask, cancellationWork);
        }
        try {
            await retained.WaitAsync(stopTimeout).ConfigureAwait(false);
        } catch (TimeoutException error) {
            // A held source remains owned so a later stop can join it safely
            throw new IOException("The cohort source operation has not stopped", error);
        }
    }
}
