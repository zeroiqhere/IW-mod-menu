namespace Apparition.Backend.Session;

// Frames read a cached owner state while workers prove the original native process
internal sealed partial class RetailConnection {
    private readonly int expectedProcessId;
    private int liveness;
    private int newWritesClosed;

    internal bool HasConfirmedConnection => Volatile.Read(ref liveness) == 1;
    internal bool HasRetiredConnection => Volatile.Read(ref liveness) == 2;
    internal string ConnectionFailureStatus =>
        "The original game connection could not be confirmed; stop this session before reconnecting";

    internal void RequestNewWriteClose() => Interlocked.Exchange(ref newWritesClosed, 1);

    private bool AdmitNewWrite() {
        if (Volatile.Read(ref newWritesClosed) != 0 || !ObserveOriginalConnection()) return false;
        // Native observation can wait; a shutdown requested during that wait must win
        return Volatile.Read(ref newWritesClosed) == 0 && HasConfirmedConnection;
    }

    internal bool ObserveOriginalConnection() {
        if (HasRetiredConnection || expectedProcessId <= 0) return false;
        try {
            // Bracket identity so a getter that crosses process exit cannot publish a valid owner
            if (!Transport.Connected || Transport.ProcessId != expectedProcessId
                || !Transport.Connected || Transport.ProcessId != expectedProcessId) {
                Interlocked.Exchange(ref liveness, 2);
                return false;
            }
            // A concurrent failure wins permanently over this older positive sample
            Interlocked.CompareExchange(ref liveness, 1, 0);
            return HasConfirmedConnection;
        } catch (Exception) {
            // Failed native identity reads never revive a cached connection on a later poll
            Interlocked.Exchange(ref liveness, 2);
            return false;
        }
    }
}
