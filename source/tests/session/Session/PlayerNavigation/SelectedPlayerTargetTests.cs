using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Receipts exercise real session selection and stored flags without opening a process
internal static class SelectedPlayerTargetTests {
    public static async Task ScopedPeerSelectionPublishesOneExactIdentityAndGeneration() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var before = Scope(session);
            Check(before.Revision == 0 && Target(session) is null, "A new owner must not invent a selection");

            await session.ExecuteAsync(Select(1) with { SelectionScope = before });

            var target = Target(session)!;
            Check(target.Identity == transport.ReadTeammateState(1).Identity
                && target.Route == PlayerMenuRoute.Teammate && target.SelectionRevision == 1
                && ReferenceEquals(target.OwnerToken, before.OwnerToken)
                && session.ReadMenuState().SelectedPlayerTarget == target,
                "The frame and command receipt must share the same strict captured target");
            await session.ExecuteAsync(TargetAction(target));
            Check(transport.Flags(1) == 0x201 && transport.Flags(2) == 0x200,
                "The receipt must reach only its existing original flags owner");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task OldSelectionScopesAndTargetReceiptsCannotApplyAfterReselection() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var firstScope = Scope(session);
            await session.ExecuteAsync(Select(1) with { SelectionScope = firstScope });
            var first = Target(session)!;
            await session.ExecuteAsync(Select(2) with { SelectionScope = Scope(session) });
            var second = Target(session)!;
            int bindings = transport.TeammateBindCalls;

            await session.ExecuteAsync(Select(3) with { SelectionScope = firstScope });
            await session.ExecuteAsync(TargetAction(first));

            Check(Target(session) == second && Scope(session).Revision == 2
                && transport.TeammateBindCalls == bindings && transport.FlagWrites == 0,
                "A delayed old scope or target must not select or mutate a later target");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task FailedSelectionsAndForgedReceiptsCannotGrantAnotherTarget() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var original = Target(session)!;
            var before = Scope(session);
            var dead = transport.ReadTeammateState(2);
            transport.SetPlayerState(2, dead with { Health = 0 });
            await session.ExecuteAsync(Select(2) with { SelectionScope = before });
            var forged = new PlayerMenuTarget(original.Identity, PlayerMenuRoute.Personal,
                original.OwnerToken, original.SelectionRevision);
            await session.ExecuteAsync(TargetAction(forged));
            await session.ExecuteAsync(TargetAction(original) with { Kind = MenuActionKind.TogglePersonalGodmode });
            await session.ExecuteAsync(TargetAction(new(original.Identity, original.Route,
                new object(), original.SelectionRevision)));
            await session.ExecuteAsync(TargetAction(new(original.Identity, original.Route,
                new EqualOwnerToken(), original.SelectionRevision)));
            await session.ExecuteAsync(Select(2) with {
                SelectionScope = new(new EqualOwnerToken(), before.Revision)
            });
            await session.ExecuteAsync(TargetAction(original) with { Slot = 2 });
            await session.ExecuteAsync(TargetAction(original) with { ExpectedName = "Wrong name" });
            await session.ExecuteAsync(new(MenuActionKind.ToggleTeammateGodmode,
                MenuPage.SelectedBasicScripts, original.Identity.Slot, original.Identity.Name));

            Check(Target(session) == original && Scope(session).Equals(before)
                && transport.FlagWrites == 0 && transport.TeammateBindCalls == 1,
                "Failed capture and malformed authority must retain the old target without granting a new action");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task SelectedSelfUsesTheExistingPersonalOwnerAtNonzeroSlot() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(3) with { SelectionScope = Scope(session) });
            var self = Target(session)!;
            Check(self.Route == PlayerMenuRoute.Personal
                && self.Identity == session.ReadMenuState().PersonalGodmodeIdentity,
                "Selected self must be proved by the personal owner rather than a slot convention");

            await session.ExecuteAsync(TargetAction(self));
            await session.ExecuteAsync(TargetAction(self));

            Check(transport.Flags(3) == 0x200 && transport.LocalBindCalls == 1
                && transport.TeammateBindCalls == 0 && transport.HealthWrites == 0,
                "The selected self child must reuse one personal flags adapter for ON and OFF");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task RetainedDeadTargetCanRestoreButCannotEnableAgain() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var target = Target(session)!;
            await session.ExecuteAsync(TargetAction(target));
            var living = transport.ReadTeammateState(1);
            transport.SetPlayerState(1, living with { Health = 0 });

            Check(session.ReadMenuState().SelectedPlayerTarget == target
                && session.ReadMenuState().CanTeammateGodmode,
                "Life changes must retain the exact original cleanup target");
            await session.ExecuteAsync(TargetAction(target));
            int writes = transport.FlagWrites;
            await session.ExecuteAsync(TargetAction(target));
            Check(transport.Flags(1) == 0x200 && transport.FlagWrites == writes
                && !session.ReadMenuState().CanTeammateGodmode,
                "Dead OFF must preserve owned cleanup without admitting a later ON");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task ReconnectAndSameNameIdentityReplacementCannotReuseAnOldReceipt() {
        var old = new RecordingRetailTransport();
        var fresh = new RecordingRetailTransport();
        var queue = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => queue.Dequeue(), () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var oldScope = Scope(session);
            await session.ExecuteAsync(Select(1) with { SelectionScope = oldScope });
            var retired = Target(session)!;
            await session.ShutdownAsync();
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var current = Target(session)!;
            await session.ExecuteAsync(TargetAction(retired));
            await session.ExecuteAsync(Select(2) with { SelectionScope = oldScope });
            Check(Target(session) == current && fresh.FlagWrites == 0
                && !ReferenceEquals(current.OwnerToken, retired.OwnerToken),
                "A replacement connection must reject old scopes even when names and identity bytes match");
            var original = fresh.ReadTeammateState(1);
            fresh.SetPlayerState(1, original with {
                Identity = original.Identity with { Guid = "1111111111111111" }
            });
            await session.ExecuteAsync(TargetAction(current));
            Check(fresh.FlagWrites == 0 && !session.ReadMenuState().CanTeammateGodmode
                && Target(session) is null,
                "A same-name GUID replacement must not borrow the retained writer");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AViewCrossingSelectionCommitCannotPublishMixedTargetFacts() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? refresh = null, selecting = null;
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var next = Scope(session);
            transport.OnFlagRead = () => {
                transport.OnFlagRead = null;entered.TrySetResult();release.Task.GetAwaiter().GetResult();
            };
            // A held native refresh overlaps the next selection without blocking render frames
            refresh = session.RefreshLocalObservationAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            selecting = session.ExecuteAsync(Select(2) with { SelectionScope = next });
            var crossed = await Task.Run(session.ReadMenuState).WaitAsync(TimeSpan.FromSeconds(5));
            Check(crossed.SelectedPlayerTarget is null && !crossed.CanTeammateGodmode
                && crossed.TeammateGodmodeValue == "UNKNOWN",
                "A frame crossing selection commit must not combine an old value with a new target");
            release.TrySetResult();
            await Task.WhenAll(refresh, selecting).WaitAsync(TimeSpan.FromSeconds(5));
            Check(Target(session)?.Identity.Slot == 2 && session.ReadMenuState().SelectedPlayerTarget == Target(session),
                "The next complete frame must publish the newly captured target");
        } finally {
            transport.OnFlagRead = null;release.TrySetResult();
            if (refresh is not null) await refresh.WaitAsync(TimeSpan.FromSeconds(5));
            if (selecting is not null) await selecting.WaitAsync(TimeSpan.FromSeconds(5));
            await session.ShutdownAsync();
        }
    }

    public static async Task LegacyQueuedTargetIsCapturedBeforeWaitingAndCannotFollowAReselection() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? selecting = null;
        Task? queued = null;
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var before = Target(session)!;
            transport.OnFlagRead = () => {
                transport.OnFlagRead = null;
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            };
            // The real capture reads its original flag adapter before committing a new generation
            selecting = session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = session.ExecuteAsync(Toggle(1));
            release.TrySetResult();
            await Task.WhenAll(selecting, queued).WaitAsync(TimeSpan.FromSeconds(5));

            Check(Target(session)!.SelectionRevision == before.SelectionRevision + 1
                && transport.FlagWrites == 0,
                "A legacy queued name-only action must retain its entry receipt instead of borrowing the new selection");
        } finally {
            transport.OnFlagRead = null;
            release.TrySetResult();
            if (selecting is not null) await selecting.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5));
            await session.ShutdownAsync();
        }
    }

    public static async Task AuthenticatedSelfWithoutAPersonalAdapterCannotCreateATeammateWriter() {
        await using var fixture = new LocalAdmissionFixture();
        fixture.Transport.Actor = new Backend.Tests.LocalActorMemoryFixture(2);
        var identity = fixture.Transport.Actor.Read().Binding.Identity;
        var state = fixture.Transport.Features.ReadTeammateState(2);
        fixture.Transport.Features.SetPlayerState(2, state with { Identity = identity });
        await fixture.OpenAndObserveAsync();
        var scope = Scope(fixture.Session);

        await fixture.Session.ExecuteAsync(new(MenuActionKind.SelectPlayer, Slot: 2,
            ExpectedName: identity.Name) { SelectionScope = scope });

        Check(Target(fixture.Session) is { Route: PlayerMenuRoute.Personal } target
            && target.Identity == identity && target.SelectionRevision == scope.Revision + 1
            && !fixture.Session.ReadMenuState().CanPersonalGodmode
            && fixture.Transport.Features.TeammateBindCalls == 0
            && fixture.Transport.Features.FlagWrites == 0,
            "Authenticated self must not become a teammate writer when its personal flags source is unavailable");
    }

    public static async Task ConnectionLossAndCounterExhaustionWithdrawDispatchWithoutBindingAnotherOwner() {
        foreach (bool wrongPid in new[] { true, false }) {
            var transport = new RecordingRetailTransport();
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
                var original = Target(session)!;
                if (wrongPid) transport.ReportedPid = 99;
                else transport.Disposed = true;
                // Cached publication does not perform the next native identity query
                Check(Target(session) == original, "A frame queried liveness before the retained worker observation");
                await session.RefreshLocalObservationAsync();
                Check(Target(session) is null && session.ReadPlayerSelectionScope() is null,
                    "Observed original-process failure did not withdraw target and selection admission");
                transport.ReportedPid = 4242;
                transport.Disposed = false;
                await session.RefreshLocalObservationAsync();
                Check(Target(session) is null && session.ReadPlayerSelectionScope() is null,
                    "Recovered native scalars revived a terminally retired owner");
            } finally {
                transport.ReportedPid = 4242;
                transport.Disposed = false;
                await session.ShutdownAsync();
            }
        }

        // Counter exhaustion uses a fresh owner rather than borrowing a retired connection
        var live = new RecordingRetailTransport();
        var current = Create(live);
        try {
            await current.ExecuteAsync(new(MenuActionKind.Connect));
            await current.ExecuteAsync(Select(1) with { SelectionScope = Scope(current) });
            var original = Target(current)!;
            var field = typeof(RetailBackendSession).GetField("playerSelection",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            object context = field.GetValue(current)!;
            context.GetType().GetProperty("Revision")!.SetValue(context, ulong.MaxValue);
            var terminal = new PlayerMenuTarget(original.Identity, original.Route, original.OwnerToken, ulong.MaxValue);
            context.GetType().GetProperty("Target")!.SetValue(context, terminal);
            var exhausted = Scope(current);
            int bindings = live.TeammateBindCalls;
            await current.ExecuteAsync(Select(2) with { SelectionScope = exhausted });
            Check(Scope(current).Revision == ulong.MaxValue && Target(current) == terminal
                && live.TeammateBindCalls == bindings && live.FlagWrites == 0,
                "Exhaustion must reject before retaining a new adapter or changing selected metadata");
        } finally { await current.ShutdownAsync(); }
    }

    public static async Task ObservedRosterRemovalInvalidatesAnEarlierSelectionScope() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1) with { SelectionScope = Scope(session) });
            var beforeRemoval = Scope(session);
            var original = transport.ReadTeammateState(1);
            transport.SetPlayerState(1, original with {
                Identity = original.Identity with { Name = "Another roster player" }
            });
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            Check(Target(session) is null && session.ReadMenuState().SelectedSlot == -1
                && Scope(session).Revision == beforeRemoval.Revision + 1,
                "An observed removal must withdraw the target and invalidate its earlier selection scope");
            transport.SetPlayerState(1, original with {
                Identity = original.Identity with { Guid = "1111111111111111" }
            });
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            int bindings = transport.TeammateBindCalls;

            await session.ExecuteAsync(Select(1) with { SelectionScope = beforeRemoval });

            Check(Target(session) is null && session.ReadMenuState().SelectedSlot == -1
                && transport.TeammateBindCalls == bindings && transport.FlagWrites == 0,
                "A reappearing name must not make an observed old selection scope current again");
            await session.ExecuteAsync(Select(3) with { SelectionScope = Scope(session) });
            Check(Target(session)?.Identity.Slot == 3
                && Scope(session).Revision == beforeRemoval.Revision + 2,
                "A fresh scope must still capture a different valid original target after withdrawal");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AScopedSelfSelectionWaitingForDispatchCannotCommitAfterProcessProofLoss() {
        foreach (bool pidChanged in new[] { true, false }) {
            await using var fixture = new LocalAdmissionFixture();
            fixture.Transport.Actor = new Backend.Tests.LocalActorMemoryFixture(2);
            var identity = fixture.Transport.Actor.Read().Binding.Identity;
            var state = fixture.Transport.Features.ReadTeammateState(2);
            fixture.Transport.Features.SetPlayerState(2, state with { Identity = identity });
            await fixture.OpenAndObserveAsync();
            var expected = Scope(fixture.Session);
            var field = typeof(RetailBackendSession).GetField("targetGate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var gate = (SemaphoreSlim)field.GetValue(fixture.Session)!;
            await gate.WaitAsync();
            bool gateHeld = true;
            Task? selecting = null;
            try {
                // The actual worker passes its outer process guard before waiting at this owned gate
                selecting = fixture.Session.ExecuteOnWorkerAsync(new(MenuActionKind.SelectPlayer,
                    Slot: 2, ExpectedName: identity.Name) { SelectionScope = expected });
                Check(!selecting.IsCompleted, "The selection must reach its real dispatch wait before revocation");
                if (pidChanged) fixture.Transport.Features.ReportedPid = 99;
                else fixture.Transport.Features.Disposed = true;
                gate.Release();
                gateHeld = false;
                await selecting.WaitAsync(TimeSpan.FromSeconds(5));
                fixture.Transport.Features.ReportedPid = 4242;
                fixture.Transport.Features.Disposed = false;
                Check(fixture.Session.ReadPlayerSelectionScope() is null && Target(fixture.Session) is null
                    && RetainedSelectionRevision(fixture.Session) == expected.Revision
                    && fixture.Transport.Features.TeammateBindCalls == 0,
                    "Process proof lost during the queue must not commit an authenticated cached self receipt");
            } finally {
                fixture.Transport.Features.ReportedPid = 4242;
                fixture.Transport.Features.Disposed = false;
                if (gateHeld) gate.Release();
                if (selecting is not null) await selecting.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var expected = Scope(session);
            transport.OnLocalRead = () => {
                transport.OnLocalRead = null;
                transport.ReportedPid = 99;
            };
            // This real personal capture loses process proof after admission, before metadata commit
            await session.ExecuteAsync(Select(3) with { SelectionScope = expected });
            transport.ReportedPid = 4242;
            Check(session.ReadPlayerSelectionScope() is null && Target(session) is null
                && RetainedSelectionRevision(session) == expected.Revision && transport.FlagWrites == 0,
                "Proof lost during personal capture must not commit a selected-target receipt");
        } finally {
            transport.OnLocalRead = null;
            transport.ReportedPid = 4242;
            await session.ShutdownAsync();
        }
    }

    // Withdrawal hides authority without spending the private selection counter
    private static ulong RetainedSelectionRevision(RetailBackendSession session) {
        var field = typeof(RetailBackendSession).GetField("playerSelection",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        object context = field.GetValue(session)!;
        return (ulong)context.GetType().GetProperty("Revision")!.GetValue(context)!;
    }

    // Object value equality is deliberately hostile to opaque-owner admission
    private sealed class EqualOwnerToken {
        public override bool Equals(object? other) => other is not null;
        public override int GetHashCode() => 0;
    }

    // Interface defaults make absent authority testable before production implements the seam
    private static PlayerMenuSelectionScope Scope(IBackendSession session) =>
        session.ReadPlayerSelectionScope() ?? throw new Exception("The connected session did not publish a selection scope");
    private static PlayerMenuTarget? Target(IBackendSession session) => session.ReadSelectedPlayerTarget();
    private static MenuAction TargetAction(PlayerMenuTarget target) => new(
        target.Route == PlayerMenuRoute.Personal ? MenuActionKind.TogglePersonalGodmode
            : MenuActionKind.ToggleTeammateGodmode, MenuPage.SelectedBasicScripts,
        target.Identity.Slot, target.Identity.Name) { ExpectedTarget = target };
}
