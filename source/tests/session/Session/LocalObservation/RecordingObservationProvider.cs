using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// The source and its completion barriers belong only to this test process
internal sealed class RecordingObservationProvider : ILocalPlayerObservationProvider {
    public int Reads;
    public Func<CancellationToken, Task<LocalPlayerObservation?>> Read = _ => Task.FromResult<LocalPlayerObservation?>(Known());

    public Task<LocalPlayerObservation?> ReadAsync(CancellationToken cancellationToken) {
        Interlocked.Increment(ref Reads);
        return Read(cancellationToken);
    }

    internal static LocalPlayerObservation Known(int processId = 4242, LocalPlayerLife life = LocalPlayerLife.Alive) =>
        new(processId, new PlayerIdentity(2, 0x120000, 0x220000, 0x320000, 4, "Owned fixture", "0123456789ABCDEF"),
            life, true, false, "Owned fixture observation");

    internal static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
