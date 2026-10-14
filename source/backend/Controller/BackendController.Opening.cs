using Apparition.Backend.Ipc;
using Apparition.Backend.QuickMenu;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

// The owner loop queues intent and consumes proof; the worker performs the fresh source read
public sealed partial class BackendController {
    private sealed record OpeningRequest(OverlayInput Input, ulong NavigationRevision,
        ulong PolicyRevision, LocalMenuAdmissionSnapshot Admission, Func<bool> ForegroundAllowed);
    private OpeningRequest? queuedOpening;
    private OpeningRequest? validatingOpening;
    private Task<LocalMenuAdmissionSnapshot?>? openingValidation;
    private object? admittedMenuOwner;
    private QuickOpeningTimeline? quickOpening;
    private bool quickInputsLocked;
    // An accepted callback replaces the completed opening monitor without borrowing its fade
    private bool quickWaitReplacedByCallback;
    private ulong? quickPresentationRevision;

    private bool HasVerifiedOpeningOwner => !cleanupRecovery && session.ReadLocalMenuAdmission() is { } proof
        && proof.Observation.ProcessId == processId && proof.Observation.HasMenu == true
        && proof.Observation.Life is LocalPlayerLife.Alive or LocalPlayerLife.Dead;

    private LocalMenuEligibilityState? ReadLocalMenuEligibility(bool frameBusy) {
        return ReadLocalMenuEligibility(session.ReadLocalMenuAdmission(), frameBusy);
    }

    private LocalMenuEligibilityState? ReadLocalMenuEligibility(LocalMenuAdmissionSnapshot? admission, bool frameBusy, bool? controlsDisabled = null) {
        // Administrative recovery has no actor grant and must keep local Exit navigation available
        if (cleanupRecovery || admission is null || admission.Observation.ProcessId != processId) return null;
        var observation = admission.Observation;
        string life = observation.Life switch {
            LocalPlayerLife.Alive => "alive", LocalPlayerLife.Dead => "dead", _ => "unknown"
        };
        return new(admission.Revision, life, observation.HasMenu,
            observation.Identity is null ? null : controlsDisabled ?? MenuMonitorControlsDisabled, frameBusy || cleanupRecovery);
    }

    private bool CanOpen(OverlayInput input, LocalMenuAdmissionSnapshot admission) {
        var observed = admission.Observation;
        if (observed.ProcessId != processId || observed.HasMenu != true || observed.Identity is null
            || cleanupRecovery || MonitorWaitBlocksInput || MenuMonitorControlsDisabled || Busy || session.ReadMenuState().Busy) return false;
        return input switch {
            OverlayInput.OpenMain => observed.Life == LocalPlayerLife.Alive && CurrentMainOpeningChord is not null,
            OverlayInput.OpenQuick => openingActions is not null && CurrentMainOpeningChord is not null
                && (openingPolicies?.QuickDisabled != true)
                && (observed.Life == LocalPlayerLife.Alive && openingPolicies is not null
                    || observed.Life == LocalPlayerLife.Dead
                        && openingActions.Contains(Apparition.Backend.OpenControls.OpeningAction.Aim)
                        && openingActions.Contains(Apparition.Backend.OpenControls.OpeningAction.Jump)),
            _ => false
        };
    }

    private bool HandleScopedMonitorInput(CommandEnvelope command, Func<bool> foregroundAllowed) {
        if (command.Input is OverlayInput.OpenMain or OverlayInput.OpenQuick) {
            var admission = session.ReadLocalMenuAdmission();
            if (navigation.IsOpen || admission is null || command.NavigationRevision != navigation.Revision
                || command.OpeningControlsRevision != OpeningAdmissionRevision
                || command.OpeningControlsRevision is null || command.LocalAdmissionRevision != admission.Revision
                || !CanOpen(command.Input.Value, admission) || !foregroundAllowed()) return true;
            // No source read starts until the queued worker is explicitly admitted
            CancelMonitorContinuation();
            queuedOpening = new(command.Input.Value, navigation.Revision,
                command.OpeningControlsRevision.Value, admission, foregroundAllowed);
            return true;
        }
        if (command.Input != OverlayInput.JumpBack) return false;
        var current = session.ReadLocalMenuAdmission();
        if (!navigation.IsOpen || navigation.Surface != MenuSurface.Quick || current is null
            || openingActions?.Contains(Apparition.Backend.OpenControls.OpeningAction.Jump) != true
            || command.NavigationRevision != navigation.Revision
            || command.LocalAdmissionRevision != current.Revision || command.OpeningControlsRevision is not null
            || current.Observation.Life != LocalPlayerLife.Dead || current.Observation.HasMenu != true
            || MenuMonitorControlsDisabled || Busy || session.ReadMenuState().Busy || !foregroundAllowed()) return true;
        // The source dead-jump route uses the same retained parent path as melee
        var wait = PrepareMonitorWait("back", command, foregroundAllowed);
        if (command.MenuRevision is not null && wait is null) return true;
        navigation.Handle(OverlayInput.Back);
        CommitMonitorWait(wait);
        CancelOpening();
        return true;
    }

    private bool StartQueuedOpening() {
        if (queuedOpening is not { } request) return false;
        queuedOpening = null;
        validatingOpening = request;
        // Even a synchronously completing provider stays off the render and command owner loop
        openingValidation = Task.Run(() => session.ValidateLocalMenuOpeningAsync(request.Admission.Revision));
        return true;
    }

    private void ObserveOpeningCompletion() {
        if (openingValidation?.IsCompleted != true) return;
        var request = validatingOpening;
        LocalMenuAdmissionSnapshot? result = null;
        try { result = openingValidation.GetAwaiter().GetResult(); }
        catch (Exception exception) { error = "Menu opening failed: " + exception.Message; }
        openingValidation = null;
        validatingOpening = null;
        var current = session.ReadLocalMenuAdmission();
        // Validate every captured owner before committing any family, clock or navigation counter
        if (request is null || result is null || current is null || ExitRequested || finished || navigation.IsOpen
            || navigation.Revision != request.NavigationRevision || OpeningAdmissionRevision != request.PolicyRevision
            || !ReferenceEquals(result.OwnerToken, request.Admission.OwnerToken)
            || !ReferenceEquals(current.OwnerToken, request.Admission.OwnerToken)
            || result.Revision != request.Admission.Revision || current.Revision != result.Revision
            || result.Observation != current.Observation || !request.ForegroundAllowed() || !CanOpen(request.Input, current)) return;
        ulong revision = checked(openingRevision + 1);
        var chrome = menuChrome.Prepare(current.OwnerToken);
        if (request.Input == OverlayInput.OpenMain) {
            long timestamp = clock.GetTimestamp();
            var transition = new LocalMenuOpenTransitionState(revision, timestamp, timestamp);
            navigation.Open(MenuSurface.Main);
            openTransition = transition;
            openTransitionPhase = "rebuild";
            openReadyToDraw = false;
        } else {
            // Rows are committed by Snapshot, so this preparation starts with no guessed row count
            var prepared = QuickOpeningTimeline.Start(clock, 0);
            navigation.Open(MenuSurface.Quick);
            quickOpening = prepared;
            quickWaitReplacedByCallback = false;
            quickInputsLocked = true;
            quickPresentationRevision = null;
            // Quick source HUD creation is immediate; its actual snapshot commits the instance
            chrome = chrome.MarkReady();
        }
        menuChrome = chrome;
        openingRevision = revision;
        admittedMenuOwner = current.OwnerToken;
    }

    private void AdvanceQuickOpening() {
        if (quickOpening is null) return;
        if (!navigation.IsOpen || navigation.Surface != MenuSurface.Quick) {
            quickOpening = null;
            quickInputsLocked = false;
            quickWaitReplacedByCallback = false;
            quickPresentationRevision = null;
            return;
        }
        var read = quickOpening.Read();
        quickOpening = read.State;
        quickInputsLocked = !quickWaitReplacedByCallback && read.Frame.InputsLocked;
        openingFrame = read.Frame.OpeningElapsedMilliseconds is { } elapsed ? new(openingRevision, elapsed) : null;
    }

    private void BindQuickOpeningRows(int count, MenuAppearance displayedAppearance) {
        if (quickOpening is null || !navigation.IsOpen || navigation.Surface != MenuSurface.Quick) return;
        if (quickPresentationRevision is null) {
            // Bind actual shown rows while retaining the original admission timestamp
            quickOpening = quickOpening.WithShownRows(Math.Min(count, MenuOptionCapacity.For(displayedAppearance)));
            quickPresentationRevision = navigation.MenuRevision;
            AdvanceQuickOpening();
        } else if (quickPresentationRevision != navigation.MenuRevision) {
            CancelQuickFade();
            quickPresentationRevision = navigation.MenuRevision;
        }
    }

    private void CancelQuickFade() {
        if (quickOpening is null) return;
        quickOpening = quickOpening.CancelFade();
        openingFrame = null;
    }

    private void ObserveAdmittedMenuOwner() {
        if (!navigation.IsOpen || cleanupRecovery) return;
        // The disconnected Connect page owns no actor and cannot silently become a scoped visit
        bool retireBootstrap = admittedMenuOwner is null && session.RequiresLocalMenuAdmission
            && session.ReadMenuState().Connected && !Busy;
        if (admittedMenuOwner is null && !retireBootstrap) return;
        var current = session.ReadLocalMenuAdmission();
        bool changedOwner = current is null || !ReferenceEquals(admittedMenuOwner, current.OwnerToken)
            || current.Observation.ProcessId != processId || current.Observation.HasMenu != true;
        bool mainDeath = navigation.Surface == MenuSurface.Main
            && current?.Observation.Life == LocalPlayerLife.Dead && !MenuMonitorControlsDisabled;
        if (!retireBootstrap && !changedOwner && !mainDeath) return;
        // Retire dedicated editor state before closing its retained family
        LeaveWidthEditor();
        LeaveInstructionPositionEditor();
        LeaveMenuPositionEditor();
        LeaveMenuCredits();
        menuUnlock = LocalMenuUnlockState.None;
        navigation.Close();
        admittedMenuOwner = null;
        CancelPreferenceRefresh();
    }
}
