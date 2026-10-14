using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class PersonalObservationRecoveryTests {
    private static RetailBackendSession CreateObserved(CohortRetailTransport transport, TimeProvider? clock = null) =>
        new(4242, () => true, () => transport, () => false,
            _ => new OwnedLocalPlayerObservationProvider(4242, transport), observationRecoveryClock: clock);

    public static async Task ObservationRecoveryRecapturesTransientPersonalFailureWithoutEnablingFlags() {
        var transport = new CohortRetailTransport();
        transport.Inner.LocalSlot = 2;
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(!session.ReadMenuState().CanPersonalGodmode && transport.Inner.LocalBindCalls == 0,
                "The failed initial flags read exposed a personal writer");
            transport.Inner.FailLocalRead = false;
            await session.RefreshLocalObservationAsync();
            var frame = session.ReadMenuState();
            var row = OverlayMenuBuilder.Build(MenuPage.Personal, frame).Rows[0];
            Check(row.Enabled && row.Value == "OFF" && frame.PersonalGodmodeIdentity?.Slot == 2,
                "A fresh owned observation did not recover the unavailable personal control");
            Check(transport.Inner.FlagWrites == 0 && transport.Inner.HealthWrites == 0
                && transport.Inner.AmmoWrites == 0 && transport.Inner.LocalBindCalls == 1,
                "Background recovery enabled a feature or created repeated local owners");
            await session.ExecuteAsync(row.Action);
            Check(session.ReadMenuState().PersonalGodmodeValue == "ON" && transport.Inner.Flags(2) == 0x201,
                "The recovered personal row could not enable its authenticated actor");
            Check(Enumerable.Range(0, 4).Where(slot => slot != 2).All(slot => transport.Inner.Flags(slot) == 0x200),
                "Personal recovery or the explicit toggle changed a teammate");
        } finally { Check((await session.ShutdownAsync()).Restored, "Recovered personal owner was not restored"); }
    }

    public static async Task SuccessfulPersonalCleanupRetryRecoversTheMenuToggleOnlyFromFreshReadOnlyObservation() {
        foreach (int localSlot in new[] { 0, 2, 3 }) {
            var transport = new CohortRetailTransport();
            transport.Inner.LocalSlot = localSlot;
            var session = CreateObserved(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                var identity = session.ReadMenuState().PersonalGodmodeIdentity;
                var initial = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
                Check(initial.Enabled && initial.Action.Kind == MenuActionKind.TogglePersonalGodmode,
                    "The healthy original actor has no ordinary personal toggle");
                await session.ExecuteAsync(initial.Action);
                transport.Inner.RejectLocalFlagRestore = true;
                await session.ExecuteAsync(initial.Action);
                var retry = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
                Check(retry.Enabled && retry.Value == "UNKNOWN" && retry.Action.Kind == MenuActionKind.RestorePersonalGodmode
                    && transport.Inner.Flags(localSlot) == 0x201,
                    "An unconfirmed OFF did not expose the original pure restoration route");
                transport.Inner.RejectLocalFlagRestore = false;
                await session.ExecuteAsync(retry.Action);
                var restored = session.ReadMenuState();
                var waiting = OverlayMenuBuilder.Build(MenuPage.Personal, restored).Rows[0];
                Check(waiting.Value == "OFF" && !waiting.Enabled && !restored.CanRestorePersonalGodmode
                    && transport.Inner.Flags(localSlot) == 0x200,
                    "Confirmed restoration exposed a new enable before fresh living eligibility");
                int writes = transport.Inner.FlagWrites;
                int reads = transport.Inner.LocalReadCalls;

                // An owned observation recovers availability without enabling any player
                await session.RefreshLocalObservationAsync();
                var recovered = session.ReadMenuState();
                var row = OverlayMenuBuilder.Build(MenuPage.Personal, recovered).Rows[0];
                Check(row.Enabled && row.Value == "OFF" && row.Action.Kind == MenuActionKind.TogglePersonalGodmode
                    && recovered.PersonalGodmodeIdentity == identity && transport.Inner.LocalReadCalls > reads
                    && transport.Inner.LocalBindCalls == 1 && transport.Inner.FlagWrites == writes,
                    "Read-only observation did not recover the original OFF menu capability");
                Check(Enumerable.Range(0, 4).All(slot => transport.Inner.Flags(slot) == 0x200)
                    && transport.Inner.HealthWrites == 0 && transport.Inner.AmmoWrites == 0,
                    "Restoration recovery changed player flags, health or ammo");

                // A later deliberate selection is the only action that may set the flag again
                await session.ExecuteAsync(row.Action);
                Check(session.ReadMenuState().PersonalGodmodeValue == "ON"
                    && transport.Inner.Flags(localSlot) == 0x201 && transport.Inner.FlagWrites == writes + 1
                    && transport.Inner.LocalBindCalls == 1
                    && Enumerable.Range(0, 4).Where(slot => slot != localSlot)
                        .All(slot => transport.Inner.Flags(slot) == 0x200),
                    "The recovered toggle replaced its writer or enabled another player");
            } finally {
                transport.Inner.RejectLocalFlagRestore = false;
                Check((await session.ShutdownAsync()).Restored, "Restoration recovery lost its original cleanup owner");
            }
        }
    }

    public static async Task ObservationRecoveryRejectsAChangedStrictBindingBeforeCreatingAnOwner() {
        var transport = new CohortRetailTransport();
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            transport.Inner.FailLocalRead = false;
            // The receipt captures the old account before the independent flags read changes it
            transport.Inner.OnLocalRead = () => transport.Inner.LocalAccountId = 456;
            await session.RefreshLocalObservationAsync();
            var frame = session.ReadMenuState();
            Check(!frame.CanPersonalGodmode && frame.PersonalGodmodeIdentity is null
                && transport.Inner.LocalBindCalls == 0 && transport.Inner.FlagWrites == 0,
                "A mismatched receipt bound the newer strict actor under old observation authority");
        } finally { transport.Inner.OnLocalRead = null; await session.ShutdownAsync(); }
    }

    public static async Task ObservationRecoverySkipsDeadUnknownUnverifiedAndForeignOnActors() {
        for (int scenario = 0; scenario < 4; scenario++) {
            var transport = new CohortRetailTransport();
            transport.Inner.FailLocalRead = true;
            var session = CreateObserved(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                transport.Inner.FailLocalRead = false;
                if (scenario == 0) transport.Host = false;
                if (scenario == 1) { transport.Inner.Health = 0; transport.Inner.LocalSessionState = 1; }
                if (scenario == 2) transport.Inner.LocalSessionState = 99;
                if (scenario == 3) transport.Inner.SetFlags(0, 0x201);
                int reads = transport.Inner.LocalReadCalls;
                await session.RefreshLocalObservationAsync();
                Check(!session.ReadMenuState().CanPersonalGodmode && transport.Inner.LocalBindCalls == 0
                    && transport.Inner.FlagWrites == 0 && transport.Inner.HealthWrites == 0 && transport.Inner.AmmoWrites == 0,
                    "Unavailable actor proof or a foreign ON flag granted automatic personal control");
                if (scenario != 3) Check(transport.Inner.LocalReadCalls == reads,
                    "An ineligible observation started an unnecessary strict capture");
            } finally { await session.ShutdownAsync(); }
        }
    }

    public static async Task ObservationRecoveryMismatchedReceiptRetiresTheOriginalOwnerAfterAChangedActorRead() {
        var clock = new RecoveryTimeProvider();
        var transport = new CohortRetailTransport();
        var session = CreateObserved(transport, clock);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var original = session.ReadMenuState().PersonalGodmodeIdentity;
            var originalAccount = transport.Inner.LocalAccountId;
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            int writes = transport.Inner.FlagWrites;
            // The owned observation still names A, but the later strict read proves its replacement B
            transport.Inner.OnLocalRead = () => transport.Inner.LocalAccountId = originalAccount + 1;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().CanPersonalGodmode && transport.Inner.LocalBindCalls == 1,
                "An unmatched receipt granted control over the changed strict actor");
            transport.Inner.OnLocalRead = null;
            transport.Inner.LocalAccountId = originalAccount;
            int reads = transport.Inner.LocalReadCalls;
            clock.Milliseconds = 250;
            await session.RefreshLocalObservationAsync();
            var frame = session.ReadMenuState();
            Check(!frame.CanPersonalGodmode && frame.PersonalGodmodeIdentity == original
                && transport.Inner.LocalReadCalls > reads && transport.Inner.LocalBindCalls == 1
                && transport.Inner.FlagWrites == writes,
                "Returned account scalars revived the original owner after a proven replacement");
        } finally {
            transport.Inner.OnLocalRead = null;
            Check((await session.ShutdownAsync()).Restored, "The retired original OFF owner lost its cleanup path");
        }
    }

    public static async Task ObservationRecoveryLeavesOwnedOnAndPendingCleanupWithTheirOriginalOwner() {
        var transport = new CohortRetailTransport();
        var session = CreateObserved(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            int reads = transport.Inner.LocalReadCalls;
            int writes = transport.Inner.FlagWrites;
            await session.RefreshLocalObservationAsync();
            Check(session.ReadMenuState().PersonalGodmodeValue == "ON"
                && transport.Inner.LocalReadCalls == reads && transport.Inner.FlagWrites == writes,
                "Observation recovery recaptured or changed an already owned ON feature");
            transport.Inner.RejectLocalFlagRestore = true;
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            reads = transport.Inner.LocalReadCalls;
            writes = transport.Inner.FlagWrites;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().CanPersonalGodmode && session.ReadMenuState().CanRestorePersonalGodmode
                && transport.Inner.LocalReadCalls == reads && transport.Inner.FlagWrites == writes
                && transport.Inner.LocalBindCalls == 1,
                "Automatic capture bypassed or replaced the retained cleanup owner");
        } finally { transport.Inner.RejectLocalFlagRestore = false; await session.ShutdownAsync(); }
    }

    public static async Task ObservationRecoveryRetriesRegistrationAfterPeerCleanupWithoutReplacingTheLocalAdapter() {
        var clock = new RecoveryTimeProvider();
        var transport = new CohortRetailTransport();
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport, clock);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1));
            await session.ExecuteAsync(Toggle(1));
            transport.Inner.RejectFlagRestore = true;
            await session.ExecuteAsync(Toggle(1));
            Check(!session.ReadMenuState().CanRefreshPlayers
                && session.ReadMenuState().TeammateGodmodeValue == "UNKNOWN" && transport.Inner.Flags(1) == 0x201,
                "The original peer restore failure did not retain its cleanup owner");
            transport.Inner.FailLocalRead = false;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().CanPersonalGodmode && transport.Inner.LocalBindCalls == 1,
                "Local capture bypassed the registry's pending peer cleanup");
            transport.Inner.RejectFlagRestore = false;
            await session.ExecuteAsync(Toggle(1));
            Check(session.ReadMenuState().CanRefreshPlayers && transport.Inner.Flags(1) == 0x200,
                "The retained peer cleanup path could not restore its own flag");
            int writes = transport.Inner.FlagWrites;
            clock.Milliseconds = 250;
            await session.RefreshLocalObservationAsync();
            var frame = session.ReadMenuState();
            Check(frame.CanPersonalGodmode && frame.PersonalGodmodeValue == "OFF"
                && transport.Inner.LocalBindCalls == 1 && transport.Inner.FlagWrites == writes
                && transport.Inner.HealthWrites == 0 && transport.Inner.AmmoWrites == 0,
                "A readable local adapter stayed unavailable after registration could safely retry");
        } finally {
            transport.Inner.RejectFlagRestore = false;
            Check((await session.ShutdownAsync()).Restored, "Registration recovery lost original cleanup ownership");
        }
    }
    public static async Task ObservationRecoveryLimitsFailedRetriesAndRecoversAtTheNextDeadline() {
        var clock = new RecoveryTimeProvider();
        var transport = new CohortRetailTransport();
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport, clock);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            int reads = transport.Inner.LocalReadCalls;
            transport.Inner.FailLocalRead = false;
            for (int poll = 0; poll < 20; poll++) await session.RefreshLocalObservationAsync();
            Check(transport.Inner.LocalReadCalls == reads && !session.ReadMenuState().CanPersonalGodmode,
                "A stable failed receipt triggered a strict capture on every background poll");
            clock.Milliseconds = 249;
            await session.RefreshLocalObservationAsync();
            Check(transport.Inner.LocalReadCalls == reads, "Recovery retried before its finite deadline");
            clock.Milliseconds = 250;
            await session.RefreshLocalObservationAsync();
            Check(session.ReadMenuState().CanPersonalGodmode && transport.Inner.LocalReadCalls > reads
                && transport.Inner.LocalBindCalls == 1 && transport.Inner.FlagWrites == 0,
                "The first eligible retry deadline did not recover read-only personal availability");
            reads = transport.Inner.LocalReadCalls;
            clock.Milliseconds = 1000;
            await session.RefreshLocalObservationAsync();
            Check(transport.Inner.LocalReadCalls == reads, "A usable OFF owner kept performing recovery captures");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task ObservationRecoveryClockRollbackDoesNotCreateAnUnboundedRetryWait() {
        var clock = new RecoveryTimeProvider { Milliseconds = 1000 };
        var transport = new CohortRetailTransport();
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport, clock);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            transport.Inner.FailLocalRead = false;
            clock.Milliseconds = 100;
            await session.RefreshLocalObservationAsync();
            Check(session.ReadMenuState().CanPersonalGodmode && transport.Inner.FlagWrites == 0,
                "A clock rollback preserved an obsolete future retry deadline");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task ObservationRecoveryShutdownJoinsItsBlockedReadAndRejectsLateBinding() {
        var transport = new CohortRetailTransport();
        transport.Inner.FailLocalRead = true;
        var session = CreateObserved(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        transport.Inner.FailLocalRead = false;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        transport.Inner.OnLocalRead = () => { entered.TrySetResult(); release.Wait(); };
        var reading = session.RefreshLocalObservationAsync();
        Task<syndishanx_iw_tool.SessionLifecycle.SessionShutdownResult>? shutdown = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await session.ExecuteAsync(new(MenuActionKind.None)).WaitAsync(TimeSpan.FromSeconds(2));
            shutdown = session.ShutdownAsync();
            Check(!shutdown.IsCompleted && transport.Inner.DisposeCalls == 0
                && !session.ReadMenuState().CanPersonalGodmode,
                "Shutdown disposed the source or exposed control while recovery still owned a read");
        } finally {
            transport.Inner.OnLocalRead = null;
            release.Set();
            await reading.WaitAsync(TimeSpan.FromSeconds(5));
            if (shutdown is not null) Check((await shutdown.WaitAsync(TimeSpan.FromSeconds(5))).Restored,
                "The blocked read did not join before successful shutdown");
            else await session.ShutdownAsync();
        }
        Check(transport.Inner.LocalBindCalls == 0 && transport.Inner.FlagWrites == 0
            && transport.Inner.DisposeCalls == 1,
            "A late recovery completion created a writer after its owner closed");
    }

}
