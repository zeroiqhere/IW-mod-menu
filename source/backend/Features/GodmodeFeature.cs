using System;
using System.Threading;
using System.Threading.Tasks;

namespace syndishanx_iw_tool.Features;

// Own the repeating operation so stopping waits for its last write to finish
public sealed class GodmodeFeature : IAsyncDisposable {
    private readonly IFeatureTransport transport;
    private readonly int frozenHealth;
    private readonly TimeSpan interval;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? loopCancellation;
    private Task? loop;
    private volatile bool active;
    private volatile string? failureReason;
    private bool closed;

    public GodmodeFeature(IFeatureTransport transport, int frozenHealth, TimeSpan? interval = null) {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        // Frozen health must be above the downed and dead range
        if (frozenHealth <= 1) {
            throw new ArgumentOutOfRangeException(nameof(frozenHealth));
        }
        this.frozenHealth = frozenHealth;
        this.interval = interval ?? TimeSpan.FromMilliseconds(25);
        if (this.interval.TotalMilliseconds < 1 || this.interval.TotalMilliseconds > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
    }

    public bool Active => active;
    public string? FailureReason => failureReason;

    public async Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) {
        bool entered = false;
        try {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (closed) {
                return Fail("The feature is closed");
            }
            if (!desired) {
                // Stopping does not restore old health or revive a dead player
                await StopLoopAsync().ConfigureAwait(false);
                return true;
            }
            if (active) {
                return true;
            }
            await StopLoopAsync().ConfigureAwait(false);
            failureReason = null;
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryWriteHealth()) {
                return false;
            }

            // The loop owns its lifetime after a confirmed first write
            loopCancellation = new CancellationTokenSource();
            active = true;
            loop = RunLoopAsync(loopCancellation.Token);
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Fail("The operation was cancelled");
        } catch (Exception exception) {
            return Fail(exception.Message);
        } finally {
            if (entered) {
                gate.Release();
            }
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken) {
        try {
            while (true) {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryWriteHealth()) {
                    return;
                }
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // A requested stop leaves the failure message unchanged
        } catch (Exception exception) {
            failureReason = exception.Message;
        } finally {
            active = false;
        }
    }

    private bool TryWriteHealth() {
        if (!transport.Connected) {
            return Fail("The backend is disconnected");
        }
        int health = transport.ReadHealth();
        // IW last stand sets health to one, so positive alone is insufficient
        // CODIW-Source d1a226dbd553229503290cfb73f8051ee3b34749 cp_laststand.gsc
        // Reject last stand, dead, or implausible values before any write
        if (health <= 1 || health > frozenHealth) {
            return Fail("The observed health is outside the supported living range");
        }
        if (!transport.Connected) {
            return Fail("The backend disconnected before the health change");
        }
        if (!transport.WriteHealth(frozenHealth)) {
            return Fail("The backend rejected the health change");
        }
        if (!transport.Connected || transport.ReadHealth() != frozenHealth) {
            return Fail("The health change could not be confirmed");
        }
        return true;
    }

    private async Task StopLoopAsync() {
        loopCancellation?.Cancel();
        if (loop is not null) {
            await loop.ConfigureAwait(false);
        }
        loopCancellation?.Dispose();
        loopCancellation = null;
        loop = null;
        active = false;
    }

    public async Task<bool> CloseAsync() {
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            closed = true;
            await StopLoopAsync().ConfigureAwait(false);
            return true;
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
