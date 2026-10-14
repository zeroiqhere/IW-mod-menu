using System;
using System.Threading;
using System.Threading.Tasks;
using syndishanx_iw_tool.Validation;

namespace syndishanx_iw_tool.Features;

// Own only the original span that this feature attempts to change
public sealed class InfiniteAmmoFeature : IAsyncDisposable {
    private readonly IFeatureTransport transport;
    private readonly byte[] expectedOriginal;
    private readonly byte[] replacement;
    private readonly byte[] trailingContext;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ByteStateSnapshot? snapshot;
    private long candidate;
    private volatile bool active;
    private volatile bool pendingRestore;
    private volatile string? failureReason;
    private volatile bool requiresGameRestart;
    private bool closed;

    public InfiniteAmmoFeature(IFeatureTransport transport, byte[] expectedOriginal,
                               byte[] replacement, byte[]? trailingContext = null) {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentNullException.ThrowIfNull(expectedOriginal);
        ArgumentNullException.ThrowIfNull(replacement);
        if (expectedOriginal.Length == 0 || expectedOriginal.Length != replacement.Length) {
            throw new ArgumentException("Original and replacement bytes must have the same nonzero width");
        }
        if (expectedOriginal.AsSpan().SequenceEqual(replacement)) {
            throw new ArgumentException("Original and replacement bytes must describe different states");
        }
        this.expectedOriginal = (byte[])expectedOriginal.Clone();
        this.replacement = (byte[])replacement.Clone();
        // Retain unchanged nearby instructions without including them in writes
        this.trailingContext = trailingContext is null ? [] : (byte[])trailingContext.Clone();
        _ = checked(expectedOriginal.Length + this.trailingContext.Length);
    }

    public bool Active => active;
    public bool HasPendingRestore => pendingRestore;
    public string? FailureReason => failureReason;
    public bool RequiresGameRestart => requiresGameRestart;

    public async Task<bool> SetAsync(bool desired, CancellationToken cancellationToken = default) {
        bool entered = false;
        try {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (closed) {
                return Fail("The feature is closed");
            }
            if (requiresGameRestart && desired) {
                // This connection must not take ownership of an unrecorded patch
                return false;
            }
            return await ApplyAsync(desired, cancellationToken).ConfigureAwait(false);
        } catch (ExistingAmmoPatchException exception) {
            requiresGameRestart = true;
            return Fail(exception.Message);
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

    private async Task<bool> ApplyAsync(bool desired, CancellationToken cancellationToken) {
        if (!requiresGameRestart) {
            failureReason = null;
        }
        if (!pendingRestore && !desired) {
            active = false;
            return true;
        }
        if (!transport.Connected) {
            return Fail("The backend is disconnected");
        }

        if (snapshot is null) {
            // Reject absent and ambiguous results before reading or changing a candidate
            var candidates = await transport.FindAmmoCandidatesAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!CandidateSelection.TrySelectUnique(candidates, value => value > 0,
                                                    out long selected, out var candidateFailure)) {
                return Fail($"Candidate validation failed: {candidateFailure}");
            }
            if (!transport.Connected) {
                return Fail("The backend disconnected during discovery");
            }
            byte[] observed = ReadValidatedPrefix(selected);
            if (!ByteStateSnapshot.TryCapture(expectedOriginal, observed, replacement,
                                              out var captured, out var byteFailure)) {
                return Fail($"Original byte validation failed: {byteFailure}");
            }
            candidate = selected;
            // Retain this validated location until the connected session closes
            snapshot = captured;
        }

        // A retry or disable always uses the retained candidate and original span
        var retained = snapshot;
        byte[] current = ReadValidatedPrefix(candidate);
        if (!pendingRestore && !retained.MatchesOriginal(current)) {
            return Fail("The original bytes changed before enabling");
        }
        if (desired ? retained.MatchesReplacement(current) : retained.MatchesOriginal(current)) {
            active = desired;
            if (!desired) {
                pendingRestore = false;
            }
            return true;
        }
        if (!(desired ? retained.MatchesOriginal(current) : retained.MatchesReplacement(current))) {
            return Fail("The retained bytes no longer match either expected state");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!transport.Connected) {
            return Fail("The backend disconnected before the change");
        }
        // Keep the original snapshot even when a write reports failure or cannot be read back
        pendingRestore = true;
        byte[] bytes = desired ? retained.CopyReplacementBytes() : retained.CopyOriginalBytes();
        if (!transport.WriteAmmoBytes(candidate, bytes)) {
            return Fail("The backend rejected the byte change");
        }
        if (!transport.Connected) {
            return Fail("The backend disconnected before confirming the change");
        }
        byte[] confirmed = ReadValidatedPrefix(candidate);
        if (!(desired ? retained.MatchesReplacement(confirmed) : retained.MatchesOriginal(confirmed))) {
            return Fail("The byte change could not be confirmed");
        }
        active = desired;
        if (!desired) {
            // Retain original bytes and location so the next toggle needs no new scan
            pendingRestore = false;
        }
        return true;
    }

    private byte[] ReadValidatedPrefix(long location) {
        byte[] bytes = transport.ReadAmmoBytes(location, expectedOriginal.Length + trailingContext.Length);
        // Both the changed span and unchanged instruction context must be present
        if (bytes.Length != expectedOriginal.Length + trailingContext.Length
            || !bytes.AsSpan(expectedOriginal.Length).SequenceEqual(trailingContext)) {
            throw new InvalidOperationException("The ammo instruction context could not be confirmed");
        }
        return bytes.AsSpan(0, expectedOriginal.Length).ToArray();
    }

    public async Task<bool> CloseAsync() {
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            closed = true;
            // Restoration remains guarded and never scans for a new candidate
            return await ApplyAsync(false, CancellationToken.None).ConfigureAwait(false);
        } catch (Exception exception) {
            return Fail(exception.Message);
        } finally {
            if (!pendingRestore) {
                // A closed feature cannot reuse its old process location
                snapshot = null;
                candidate = 0;
            }
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
