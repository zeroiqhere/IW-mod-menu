using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace syndishanx_iw_tool.SessionLifecycle;

// Own one session's captured cleanup callbacks and one shared shutdown result
public sealed class SessionShutdown {
    private readonly Func<Task>[] stopOperations;
    private readonly Func<Task<bool>> restoreAndStop;
    private readonly Action release;
    private readonly object taskLock = new();
    private Task<SessionShutdownResult>? shutdownTask;

    public SessionShutdown(IReadOnlyList<Func<Task>> stopOperations,
                           Func<Task<bool>> restoreAndStop,
                           Action release) {
        ArgumentNullException.ThrowIfNull(stopOperations);
        ArgumentNullException.ThrowIfNull(restoreAndStop);
        ArgumentNullException.ThrowIfNull(release);

        // Copy the callbacks so later changes to a caller's list cannot switch sessions
        this.stopOperations = new Func<Task>[stopOperations.Count];
        for (int index = 0; index < stopOperations.Count; index++) {
            this.stopOperations[index] = stopOperations[index]
                ?? throw new ArgumentException("A stop callback cannot be null", nameof(stopOperations));
        }
        this.restoreAndStop = restoreAndStop;
        this.release = release;
    }

    public Task<SessionShutdownResult> ShutdownAsync() {
        TaskCompletionSource<SessionShutdownResult> completion;
        lock (taskLock) {
            if (shutdownTask is not null) {
                return shutdownTask;
            }

            // Publish the shared task before invoking any session callback
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdownTask = completion.Task;
        }

        // Completion reports failures as a result instead of leaving a detached fault
        _ = CompleteAsync(completion);
        return completion.Task;
    }

    private async Task CompleteAsync(TaskCompletionSource<SessionShutdownResult> completion) {
        try {
            completion.SetResult(await RunAsync().ConfigureAwait(false));
        } catch (Exception exception) {
            completion.SetResult(new(false, false, "Shutdown failed: " + exception.Message));
        }
    }

    private async Task<SessionShutdownResult> RunAsync() {
        var errors = new List<string>();
        bool stopped = true;
        bool restored = false;

        // Each operation gets its own attempt even when an earlier stop fails
        for (int index = 0; index < stopOperations.Length; index++) {
            try {
                await stopOperations[index]().ConfigureAwait(false);
            } catch (Exception exception) {
                stopped = false;
                errors.Add($"Stop operation {index + 1} failed: {exception.Message}");
            }
        }

        try {
            // A false result confirms stopping but cannot confirm restoration
            restored = await restoreAndStop().ConfigureAwait(false);
            if (!restored) {
                errors.Add("Original state restoration could not be confirmed");
            }
        } catch (Exception exception) {
            stopped = false;
            errors.Add("Restore and stop failed: " + exception.Message);
        }

        // Retain the resource whenever an operation may still be using it
        if (stopped) {
            try {
                release();
            } catch (Exception exception) {
                stopped = false;
                errors.Add("Resource release failed: " + exception.Message);
            }
        }

        string? errorMessage = errors.Count == 0 ? null : string.Join("; ", errors);
        return new(stopped, restored, errorMessage);
    }
}
