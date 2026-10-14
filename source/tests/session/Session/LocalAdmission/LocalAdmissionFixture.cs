using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// The real provider and compiled actor reader consume only owned fixture bytes
internal sealed class LocalAdmissionFixture : IAsyncDisposable {
    public OwnedActorSourceTransport Transport { get; } = new();
    public ReceiptProvider Provider { get; }
    public RetailBackendSession Session { get; }

    public LocalAdmissionFixture(TimeSpan? stopTimeout = null) {
        Provider = new(new OwnedLocalPlayerObservationProvider(4242, Transport));
        Session = new(4242, () => true, () => Transport, () => false, _ => Provider, stopTimeout);
    }

    public async Task<LocalMenuAdmissionSnapshot> OpenAndObserveAsync() {
        await Session.ExecuteAsync(new(MenuActionKind.Connect));
        await Session.RefreshLocalObservationAsync();
        return Session.ReadLocalMenuAdmission()
            ?? throw new InvalidOperationException("A complete owned actor read did not produce menu admission");
    }

    public async ValueTask DisposeAsync() {
        Provider.BeforeRead = null;
        Transport.BeforeActorRead = null;
        Session.AllowShutdownRetry();
        await Session.ShutdownAsync();
    }

    // Barriers wrap the actual receipt provider rather than fabricate actor or account facts
    internal sealed class ReceiptProvider(OwnedLocalPlayerObservationProvider inner) : ILocalPlayerObservationReceiptProvider {
        public int Reads;
        public Func<int, CancellationToken, Task>? BeforeRead;
        public async Task<LocalPlayerObservation?> ReadAsync(CancellationToken token) {
            int count = Interlocked.Increment(ref Reads);
            if (BeforeRead is { } before) await before(count, token);
            return await inner.ReadAsync(token);
        }
        public async Task<LocalPlayerObservationReceipt?> ReadReceiptAsync(CancellationToken token) {
            int count = Interlocked.Increment(ref Reads);
            if (BeforeRead is { } before) await before(count, token);
            return await inner.ReadReceiptAsync(token);
        }
    }
}
