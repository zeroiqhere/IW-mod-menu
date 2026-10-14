using System;
using System.Threading;
using System.Threading.Tasks;

namespace syndishanx_iw_tool.Features;

// Own a single flag change and watch it without repeating writes
public sealed class TeammateGodmodeFeature : IAsyncDisposable {
    private const uint GodmodeBit = 1;
    private readonly ITeammateFlagsTransport transport;
    private readonly TimeSpan interval;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? monitorCancellation;
    private Task? monitor;
    private uint originalFlags;
    private uint enabledFlags;
    private volatile bool active;
    private volatile bool pendingRestore;
    private volatile string? failureReason;
    private bool closed;

    public TeammateGodmodeFeature(ITeammateFlagsTransport transport, TimeSpan? interval = null) {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.interval = interval ?? TimeSpan.FromMilliseconds(50);
        if (this.interval.TotalMilliseconds < 1 || this.interval.TotalMilliseconds > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
    }

    public bool Active => active;
    public bool HasPendingRestore => pendingRestore;
    public string? FailureReason => failureReason;

    public async Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) {
        bool entered = false;
        try {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (desired && closed) {
                return Fail("The feature is closed");
            }
            if (!desired) {
                await StopMonitorAsync().ConfigureAwait(false);
                return Restore();
            }
            if (active) {
                return true;
            }
            await StopMonitorAsync().ConfigureAwait(false);
            if (pendingRestore) {
                return Fail("The previous flag change still needs confirmed cleanup");
            }
            failureReason = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (!transport.Connected) {
                return Fail("The original process is disconnected");
            }
            uint observed = transport.ReadFlags();
            if ((observed & GodmodeBit) != 0) {
                return Fail("The godmode flag is already set and is not owned by this feature");
            }
            cancellationToken.ThrowIfCancellationRequested();
            originalFlags = observed;
            enabledFlags = observed | GodmodeBit;
            // A failed write can still leave a partial or unconfirmed change
            pendingRestore = true;
            if (!transport.WriteFlags(originalFlags, enabledFlags)) {
                ConfirmOriginalWithoutWriting();
                return Fail("The flag change was rejected or could not be confirmed");
            }
            uint confirmed = transport.ReadFlags();
            if (confirmed != enabledFlags) {
                if (confirmed == originalFlags) {
                    pendingRestore = false;
                }
                return Fail("The enabled flag value could not be confirmed");
            }

            // The monitor owns its lifetime after the first confirmed change
            monitorCancellation = new CancellationTokenSource();
            active = true;
            monitor = MonitorAsync(monitorCancellation.Token);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Fail("The operation was cancelled");
        } catch (Exception exception) {
            // Confirmation can release ownership only when the original is known
            if (pendingRestore) {
                ConfirmOriginalWithoutWriting();
            }
            return Fail(exception.Message);
        } finally {
            if (entered) {
                gate.Release();
            }
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken) {
        try {
            while (true) {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!transport.Connected) {
                    pendingRestore = false;
                    Fail("The original process disconnected; no restoration was attempted");
                    return;
                }
                uint observed = transport.ReadFlags();
                if (observed == originalFlags) {
                    // A cleared flag is observed once and is never reapplied
                    pendingRestore = false;
                    Fail("The game cleared the owned flag; it was not reapplied");
                    return;
                }
                if (observed != enabledFlags) {
                    Fail("The player flags changed; cleanup remains unconfirmed");
                    return;
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // A requested stop keeps any earlier failure available
        } catch (Exception exception) {
            Fail(exception.Message);
        } finally {
            active = false;
        }
    }

    private bool Restore() {
        if (!pendingRestore) {
            return true;
        }
        try {
            if (!transport.Connected) {
                // An ended process cannot retain this feature's change
                pendingRestore = false;
                return true;
            }
            uint observed = transport.ReadFlags();
            if (observed == originalFlags) {
                pendingRestore = false;
                return true;
            }
            if (observed != enabledFlags) {
                return Fail("The player flags changed; restoration was refused");
            }
            if (!transport.WriteFlags(enabledFlags, originalFlags)) {
                ConfirmOriginalWithoutWriting();
                return Fail("Restoration was rejected or could not be confirmed");
            }
            if (transport.ReadFlags() != originalFlags) {
                return Fail("Restoration could not be confirmed");
            }
            pendingRestore = false;
            return true;
        } catch (Exception exception) {
            ConfirmOriginalWithoutWriting();
            return Fail(exception.Message);
        }
    }

    private void ConfirmOriginalWithoutWriting() {
        try {
            if (!transport.Connected || transport.ReadFlags() == originalFlags) {
                pendingRestore = false;
            }
        } catch (Exception) {
            // Unknown bytes or a changed identity cannot release ownership
        }
    }

    private async Task StopMonitorAsync() {
        monitorCancellation?.Cancel();
        if (monitor is not null) {
            await monitor.ConfigureAwait(false);
        }
        monitorCancellation?.Dispose();
        monitorCancellation = null;
        monitor = null;
        active = false;
    }

    public async Task<bool> CloseAsync() {
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            closed = true;
            await StopMonitorAsync().ConfigureAwait(false);
            // Repeated closes can retry an earlier unconfirmed restoration
            return Restore();
        } finally {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        await CloseAsync().ConfigureAwait(false);
    }

    private bool Fail(string reason) {
        failureReason = reason;
        return false;
    }
}
