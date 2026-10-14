using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.SessionLifecycle;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Runtime;

internal static class BackendRunner {
    public static async Task<int> RunAsync(BackendOptions options, SessionFiles files) {
        using var game = new GameIdentity(options.ProcessId);
        if (ForegroundGame.OtherToolIsRunning()) {
            throw new InvalidOperationException("Another RTM or Apparition tool is running. Close it before starting this backend");
        }
        var session = new RetailBackendSession(game.ProcessId, game.IsForeground,
            RetailSessionTransportFactory.Create, ForegroundGame.OtherToolIsRunning,
            observationProviderFactory: RetailSessionTransportFactory.CreateObservationProvider);
        var controls = files.ReadSavedControlCapabilities();
        var capabilities = controls.Menu;
        var controller = new BackendController(game.ProcessId, session, capabilities.ChoiceControlsAvailable,
            capabilities.WidthEditorControlsAvailable,
            instructionPositionEditorControlsAvailable: capabilities.InstructionPositionEditorControlsAvailable,
            menuPositionEditorControlsAvailable: capabilities.AllowMenuPositionEditor(options.MenuPositionControlsAvailable),
            openingActions: controls.OpeningActions);
        var publication = new MenuPhasePublication(files.WriteState);
        // The session owns cancellation and resource retention; this loop only supervises completion
        var observations = new SessionObservationPolling(() => session.RefreshLocalObservationAsync());
        Task<SessionShutdownResult>? closing = null;
        try {
            publication.Publish(controller.Snapshot(), game.IsForeground());
            while (true) {
                bool alive = game.IsAlive;
                bool focused = game.IsForeground();
                if (!alive || files.HasStopRequest(game.ProcessId)) controller.RequestShutdown();
                controller.ObserveCompletion();
                observations.Tick(alive && !controller.ExitRequested && closing is null);
                var command = files.ReadCommand();
                if (command is not null) {
                    ulong previous = controller.LastSequence;
                    controller.Consume(command, game.IsForeground);
                    if (previous != controller.LastSequence) {
                        // The acknowledgment and busy frame reach the renderer before work begins
                        publication.Publish(controller.Snapshot(), game.IsForeground());
                        controller.StartQueuedAction();
                        publication.Publish(controller.Snapshot(), game.IsForeground());
                    }
                }
                if (!alive || controller.ExitRequested) {
                    closing ??= session.ShutdownAsync();
                }
                if (closing?.IsCompleted == true) {
                    var result = await closing;
                    if (result.Stopped) {
                        await controller.DrainOperationAsync();
                        await observations.DrainAsync();
                        publication.Publish(controller.ClosedState(), false);
                        if (!result.Restored) files.WriteStatus("Cleanup could not be confirmed. Restart Infinite Warfare before another match");
                        return result.Restored ? 0 : 3;
                    }
                    files.WriteStatus("Cleanup is incomplete: " + result.ErrorMessage);
                    controller.RetryAfterShutdownFailure(result.ErrorMessage);
                    session.AllowShutdownRetry();
                    closing = null;
                    // A dead process cannot keep a live worker writing to a replacement game
                    if (!alive) return 4;
                }
                // Local editor and SoftUnlock phases publish at the existing owner-loop cadence
                if (publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused)) {
                    // A closed frame on focus loss cannot become a delayed renderer command
                    var frame = controller.Snapshot();
                    publication.Publish(frame, focused);
                }
                await Task.Delay(25);
            }
        } catch (Exception exception) {
            files.WriteStatus("Backend stopped: " + exception.Message);
            throw;
        } finally {
            // File or renderer failures still stop workers before releasing process handles
            var result = await session.ShutdownAsync();
            Exception? observationFailure = null;
            if (result.Stopped) {
                try { await observations.DrainAsync(); }
                catch (Exception exception) { observationFailure = exception; }
                await controller.DrainOperationAsync();
            }
            try {
                publication.Publish(controller.ClosedState(), false);
                if (!result.Stopped || !result.Restored) files.WriteStatus("Cleanup is incomplete: " + result.ErrorMessage);
                else if (observationFailure is not null) files.WriteStatus("The local-player observation worker stopped with an error");
            } catch (IOException) {
                // An unavailable private directory cannot become a console or GUI prompt
            }
            // Publish closure before preserving a worker failure for the existing outer owner
            if (observationFailure is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observationFailure).Throw();
        }
    }
}
