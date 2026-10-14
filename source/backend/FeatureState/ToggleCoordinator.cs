using System;
using System.Threading;
using System.Threading.Tasks;

namespace syndishanx_iw_tool.FeatureState;

// Coordinate one feature without depending on a window or a process
public sealed class ToggleCoordinator {
    private readonly Func<bool, CancellationToken, Task<bool>> applyAsync;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object stateLock = new();
    private bool desiredState;
    private bool appliedState;
    private bool isClosed;

    public ToggleCoordinator(Func<bool, CancellationToken, Task<bool>> applyAsync,
                             bool initialState = false) {
        this.applyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
        desiredState = initialState;
        appliedState = initialState;
    }

    // Reads remain safe while requests finish on a worker thread
    public bool DesiredState { get { lock (stateLock) { return desiredState; } } }
    public bool AppliedState { get { lock (stateLock) { return appliedState; } } }
    public bool IsClosed { get { lock (stateLock) { return isClosed; } } }

    public async Task<ToggleResult> SetAsync(bool requestedState,
                                           CancellationToken cancellationToken = default) {
        lock (stateLock) {
            if (isClosed) {
                return Result(ToggleOutcome.Closed, requestedState);
            }
            if (cancellationToken.IsCancellationRequested) {
                return Result(ToggleOutcome.Cancelled, requestedState);
            }
            desiredState = requestedState;
        }

        // Closing cancels both waiting requests and the active request
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, shutdown.Token);
        bool gateEntered = false;
        try {
            await operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
            gateEntered = true;

            lock (stateLock) {
                if (isClosed) {
                    return Result(ToggleOutcome.Closed, requestedState);
                }
                linked.Token.ThrowIfCancellationRequested();
                if (appliedState == requestedState) {
                    return Result(ToggleOutcome.Unchanged, requestedState);
                }
            }

            // The backend must report success only after completing the change
            bool accepted = await applyAsync(requestedState, linked.Token).ConfigureAwait(false);
            lock (stateLock) {
                if (!accepted) {
                    return Result(ToggleOutcome.Failed, requestedState, "The operation was not accepted");
                }
                // A completed backend change stays recorded even if cancellation arrived late
                appliedState = requestedState;
                return Result(ToggleOutcome.Applied, requestedState);
            }
        } catch (OperationCanceledException) when (linked.IsCancellationRequested) {
            lock (stateLock) {
                return Result(isClosed ? ToggleOutcome.Closed : ToggleOutcome.Cancelled,
                              requestedState);
            }
        } catch (Exception exception) {
            lock (stateLock) {
                return Result(ToggleOutcome.Failed, requestedState, exception.Message);
            }
        } finally {
            if (gateEntered) {
                operationGate.Release();
            }
        }
    }

    public async Task CloseAsync() {
        bool cancelPending;
        lock (stateLock) {
            cancelPending = !isClosed;
            isClosed = true;
        }
        if (cancelPending) {
            shutdown.Cancel();
        }

        // Wait for the backend to leave before reporting shutdown complete
        await operationGate.WaitAsync().ConfigureAwait(false);
        operationGate.Release();
    }

    // Call while holding stateLock so the result contains one consistent snapshot
    private ToggleResult Result(ToggleOutcome outcome, bool requestedState,
                                string? errorMessage = null) =>
        new(outcome, requestedState, appliedState, errorMessage);
}
