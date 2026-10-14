using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Backend.Tests;

internal sealed record OrdinarySourceLevelFixtureProfile(string Name, string Event, long ClockMilliseconds,
    long RelativeMilliseconds, StateDocument State, CommandEnvelope? OriginalCommand,
    OrdinarySourceLevelFixtureProfiles.CommandCapture? Capture, IReadOnlyList<MenuActionKind> SessionActions);

// Source-clock frames come from one actual controller visit and one inert test session
internal static class OrdinarySourceLevelFixtureProfiles {
    internal sealed record CommandCapture(ulong NavigationRevision, ulong OpeningControlsRevision,
        ulong AdmissionRevision, ulong MenuRevision, ulong LastSequence);
    internal sealed record CommandInvocation(CommandEnvelope Command, CommandCapture Capture);

    private sealed class SourceLevelSession : IBackendSession {
        private readonly object owner = new();
        // The controller sees a living local actor and one verified personal target
        private readonly LocalPlayerObservation observation = new(42, ScopedOpeningFixture.Actor,
            LocalPlayerLife.Alive, true, false, "Controlled source-level test actor", false);
        private readonly TaskCompletionSource actionCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private const ulong AdmissionRevision = 10;

        internal List<MenuActionKind> Actions { get; } = new();
        internal OverlayMenuState State { get; private set; } = new(false, true, false, false,
            true, "OFF", "Off", false, "UNKNOWN", "Not connected", true, true,
            [new(1, "Friend")], 1, "Friend", false, "UNKNOWN", "Select a spawned teammate", false, "") {
            PersonalGodmodeIdentity = ScopedOpeningFixture.Actor,
            PersonalGodmodeRequiresVerifiedTarget = true
        };

        public bool RequiresLocalMenuAdmission => true;
        public LocalPlayerObservation ReadLocalObservation() => observation;
        public LocalMenuAdmissionSnapshot? ReadLocalMenuAdmission() =>
            new(AdmissionRevision, observation, owner);
        public OverlayMenuState ReadMenuState() => State;

        public Task<LocalMenuAdmissionSnapshot?> ValidateLocalMenuOpeningAsync(ulong expectedRevision,
            CancellationToken cancellationToken = default) => Task.FromResult<LocalMenuAdmissionSnapshot?>(
                expectedRevision == AdmissionRevision ? new(AdmissionRevision, observation, owner) : null);

        public Task ExecuteAsync(MenuAction action) {
            if (action.Kind != MenuActionKind.TogglePersonalGodmode
                || State.PersonalGodmodeIdentity != ScopedOpeningFixture.Actor)
                throw new InvalidOperationException("The fixture accepts only the captured local God Mode action");
            Actions.Add(action.Kind);
            // Keep the visible result pending until the graph releases this operation
            return CompleteActionAsync(actionCompletion.Task);
        }

        private async Task CompleteActionAsync(Task pending) {
            await pending.ConfigureAwait(false);
            // Only this fake session changes the flag shown in the menu
            bool enabled = State.PersonalGodmodeValue != "ON";
            State = State with {
                PersonalGodmodeValue = enabled ? "ON" : "OFF",
                PersonalGodmodeStatus = enabled ? "On" : "Off"
            };
        }

        internal void CompleteAction() => actionCompletion.TrySetResult();
        public Task<SessionShutdownResult> ShutdownAsync() =>
            Task.FromResult(new SessionShutdownResult(true, true, null));
    }

    private sealed class Rig {
        private ulong sequence;
        private readonly long origin;
        internal ManualTimeProvider Clock { get; } = new();
        internal SourceLevelSession Session { get; } = new();
        internal BackendController Controller { get; }
        internal long Now => Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;

        internal Rig() {
            origin = Now;
            Controller = new(42, Session, choiceControlsAvailable: true,
                clock: Clock, openingActions: QuickOpeningTestSupport.AllCapabilities());
        }

        internal CommandInvocation Send(OverlayInput input) {
            if (input is not (OverlayInput.OpenQuick or OverlayInput.Select))
                throw new InvalidOperationException("The source-level graph admits only Quick opening and Select");
            // Capture source scopes before the controller consumes the original command
            StateDocument before = Controller.Snapshot();
            ulong admission = before.LocalMenuEligibility?.Revision
                ?? throw new InvalidOperationException("The original command requires a captured local admission");
            ulong policy = before.OpeningPolicies?.Revision ?? before.MainOpeningControls?.Revision
                ?? throw new InvalidOperationException("The original command requires a captured opening policy");
            ulong menu = before.MenuRevision
                ?? throw new InvalidOperationException("The original command requires a captured menu revision");
            var capture = new CommandCapture(before.NavigationRevision, policy, admission, menu,
                before.LastSequence);
            var command = new CommandEnvelope(++sequence, 1, 42, input, true,
                before.NavigationRevision,
                policy,
                input == OverlayInput.OpenQuick ? admission : null);
            Controller.Consume(command, () => true);
            return new(command, capture);
        }

        internal void Add(ICollection<OrdinarySourceLevelFixtureProfile> profiles, string name, string eventName,
            CommandEnvelope? command = null, CommandCapture? capture = null) {
            var state = Controller.Snapshot();
            profiles.Add(new(name + ".json", eventName, Now, Now - origin, state,
                command, capture, Session.Actions.ToArray()));
        }

        internal void FinishOpening() {
            Controller.StartQueuedAction();
            TestAssert.True(SpinWait.SpinUntil(() => {
                Controller.ObserveCompletion();
                return !Controller.Busy;
            }, TimeSpan.FromSeconds(3)), "The actual source Quick opening must complete its admission");
        }

        internal void FinishAction() {
            Session.CompleteAction();
            TestAssert.True(SpinWait.SpinUntil(() => {
                Controller.ObserveCompletion();
                return !Controller.Busy;
            }, TimeSpan.FromSeconds(3)), "The actual session action must publish its completed result");
        }

        internal void Wait(int milliseconds) {
            Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            // A snapshot lets the real controller update state against the fake clock
            _ = Controller.Snapshot();
        }
    }

    internal static IReadOnlyList<OrdinarySourceLevelFixtureProfile> Build() {
        var profiles = new List<OrdinarySourceLevelFixtureProfile>();
        var rig = new Rig();
        rig.Add(profiles, "quick-before0", "closed-before-open");
        var opening = rig.Send(OverlayInput.OpenQuick);
        rig.Add(profiles, "quick-open-pending0", "open-command-accepted", opening.Command, opening.Capture);
        rig.FinishOpening();
        rig.Add(profiles, "quick-drawn0", "immediate-quick-hud", opening.Command, opening.Capture);
        // The HUD is already drawn while the ordinary Quick input wait remains active
        rig.Wait(549);
        rig.Add(profiles, "quick-wait549", "quick-input-wait-remaining", opening.Command, opening.Capture);
        rig.Wait(1);
        rig.Add(profiles, "quick-ready550", "quick-input-ready", opening.Command, opening.Capture);

        var use = rig.Send(OverlayInput.Select);
        rig.Add(profiles, "quick-use-command550", "select-command-accepted", use.Command, use.Capture);
        // Keep the actual queued callback pending so intent and its later effect stay distinct
        rig.Controller.StartQueuedAction();
        rig.Add(profiles, "quick-use-operation-pending550", "session-operation-pending", use.Command, use.Capture);
        rig.FinishAction();
        rig.Add(profiles, "quick-use-effect550", "session-result-published", use.Command, use.Capture);
        rig.Wait(180);
        rig.Add(profiles, "quick-use-refresh730", "boolean-row-refreshed", use.Command, use.Capture);
        rig.Wait(249);
        rig.Add(profiles, "quick-use-before-ready979", "boolean-caller-wait-pending", use.Command, use.Capture);
        rig.Wait(1);
        rig.Add(profiles, "quick-use-ready980", "boolean-caller-wait-complete", use.Command, use.Capture);

        return profiles.AsReadOnly();
    }
}
