using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private ulong monitorWaitRevision;
    private LocalMenuMonitorWaitState? monitorWait;

    private bool OrdinaryMonitorVisit => navigation.Page is not (MenuPage.WidthEditor or MenuPage.MenuCredits
        or MenuPage.InstructionPositionEditor or MenuPage.MenuPositionEditor);

    private LocalMenuMonitorWaitState? PrepareMonitorWait(string kind, CommandEnvelope? command = null, Func<bool>? foregroundAllowed = null) {
        var proof = session.ReadLocalMenuAdmission();
        var state = retainedState ?? session.ReadMenuState();
        if (!OrdinaryMonitorVisit || cleanupRecovery || finished || ExitRequested || !navigation.IsOpen
            || state.Preview || !state.Connected || proof is null || admittedMenuOwner is null
            || !ReferenceEquals(admittedMenuOwner, proof.OwnerToken) || proof.Observation.ProcessId != processId
            || proof.Observation.HasMenu != true || proof.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)) return null;
        var intent = CaptureMonitorIntent(command, foregroundAllowed, proof, state);
        if (command?.MenuRevision is not null && intent is null) return null;
        // Reserve the checked invocation and timestamp before any draft, cursor or parent mutation
        ulong revision = checked(monitorWaitRevision + 1);
        long timestamp = clock.GetTimestamp();
        return new(new(revision, kind), proof.OwnerToken, navigation.Revision, timestamp, timestamp) {
            ContinuationIntent = intent
        };
    }

    private void CommitMonitorWait(LocalMenuMonitorWaitState? prepared) {
        if (prepared is null) return;
        // The branch's own final parent or close transition is part of this invocation
        CancelUseContinuation();
        monitorWait = prepared with { NavigationRevision = navigation.Revision };
        monitorWaitRevision = prepared.Frame.Revision;
        CommitMonitorInvocation(prepared);
    }

    private MenuMonitorWait? AdvanceMonitorWait() {
        if (monitorWait is not { } pending) return null;
        var proof = session.ReadLocalMenuAdmission();
        var state = retainedState ?? session.ReadMenuState();
        // Independent navigation, proof loss or replacement cannot transfer a sleeping branch
        if (finished || ExitRequested || cleanupRecovery || state.Preview || !state.Connected
            || proof is null || !ReferenceEquals(pending.OwnerToken, proof.OwnerToken)
            || proof.Observation.ProcessId != processId || proof.Observation.HasMenu != true
            || proof.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)
            || navigation.Revision != pending.NavigationRevision || !OrdinaryMonitorVisit) {
            monitorWait = null;
            CancelMonitorContinuation();
            return null;
        }
        monitorWait = pending.Read(clock);
        if (monitorWait is null) MarkMonitorNormallyExpired(pending.Frame.Revision);
        return monitorWait?.Frame;
    }

    private bool MonitorWaitBlocksInput => AdvanceMonitorWait() is not null;
}
