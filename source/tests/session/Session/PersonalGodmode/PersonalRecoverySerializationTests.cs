using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class PersonalRecoverySerializationTests {
    public static async Task CancellationAfterPreparationSkipsTheRegistrySourceValidation() {
        await using var fixture = new SharedGodmodeFixture(localSlot: 2);
        await fixture.CaptureLocalAsync();
        var identity = fixture.Owner.LocalGodmode!.ReadView().Identity!;
        using var cancellation = new CancellationTokenSource();
        int sourceReads = 0;
        fixture.Transport.OnConnected = () => Interlocked.Increment(ref sourceReads);
        try {
            // A completed preparation can race cancellation before registry publication begins
            bool captured = await fixture.Owner.GodmodeOwners.CapturePreparedAsync(new[] { identity }, () => {
                cancellation.Cancel();
                return Task.FromResult(true);
            }, waitForAvailable: false, cancellation.Token);
            Check(!captured && sourceReads == 0 && !fixture.Owner.GodmodeOwners.IsOperating(identity)
                && fixture.Transport.FlagWrites == 0 && fixture.Transport.LocalBindCalls == 1,
                "Cancelled preparation entered source validation or retained registry admission");
        } finally { fixture.Transport.OnConnected = null; }
    }

    public static async Task CancelledRecoveryReleasesCoordinationWithoutChangingTheOriginalOwner() {
        for (int scenario = 0; scenario < 2; scenario++) {
            await using var fixture = new SharedGodmodeFixture(localSlot: 2);
            var transport = fixture.Transport;
            var owner = fixture.Owner;
            await fixture.CaptureLocalAsync();
            var binding = owner.LocalGodmode!.ReadView().Binding!;
            Check(await owner.LocalGodmode.SetAsync(false), "The original OFF actor was not retained");
            using var cancellation = new CancellationTokenSource();
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool>? recovery = null;
            int reads = transport.LocalReadCalls;
            try {
                if (scenario == 0) cancellation.Cancel();
                else transport.OnLocalRead = () => { entered.TrySetResult(); release.Wait(); };
                recovery = Task.Run(() => owner.RecoverPersonalGodmodeAsync(binding,
                    new RecoveryTimeProvider(), cancellation.Token));
                if (scenario == 1) {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    cancellation.Cancel();
                    release.Set();
                }
                Check(!await recovery.WaitAsync(TimeSpan.FromSeconds(5))
                    && !owner.GodmodeOwners.IsOperating(binding.Identity)
                    && owner.LocalGodmode.ReadView().Binding == binding,
                    "Cancellation retained a coordination lease or replaced the original actor");
                if (scenario == 0) Check(transport.LocalReadCalls == reads,
                    "Already cancelled recovery read the original actor");
                // Fresh read-only capture proves that cancellation released the registry gate
                Check((await owner.GodmodeOwners.CaptureAsync(new[] { binding.Identity })).Succeeded
                    && transport.LocalBindCalls == 1 && transport.FlagWrites == 0
                    && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.Flags(2) == 0x200,
                    "Cancelled recovery blocked capture or changed owned state");
            } finally {
                transport.OnLocalRead = null;
                release.Set();
                if (recovery is not null) await recovery.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    public static async Task RecoveryCannotReverseAPeerOffWhileTheSharedActionIsCompleting() {
        await using var fixture = new SharedGodmodeFixture(localSlot: 2);
        var transport = fixture.Transport;
        var owner = fixture.Owner;
        await fixture.CaptureLocalAsync();
        var binding = owner.LocalGodmode!.ReadView().Binding!;
        var peer = fixture.Identity(1);
        Check((await owner.GodmodeOwners.CaptureAsync(new[] { peer })).Succeeded
            && (await owner.GodmodeOwners.SetAsync(peer, true)).Succeeded,
            "The peer ON owner was not captured");
        Check(await owner.LocalGodmode.SetAsync(false), "The local OFF owner could not prepare recovery");
        var peerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var localEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePeer = new ManualResetEventSlim();
        using var releaseLocal = new ManualResetEventSlim();
        Task<GodmodeOperationResult>? operation = null;
        Task<bool>? recovery = null;
        int first = 0;
        transport.OnFlagRead = () => {
            // Only the peer OFF write's readback can reach this barrier
            if (transport.Flags(1) != 0x200 || Interlocked.Exchange(ref first, 1) != 0) return;
            peerEntered.TrySetResult();
            releasePeer.Wait();
        };
        transport.OnLocalRead = () => {
            localEntered.TrySetResult();
            releaseLocal.Wait();
        };
        try {
            operation = owner.GodmodeOwners.SetBatchAsync(new[] { peer, binding.Identity }, false);
            await peerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            int strictReads = transport.LocalReadCalls;
            recovery = Task.Run(() => owner.RecoverPersonalGodmodeAsync(binding,
                new RecoveryTimeProvider(), CancellationToken.None));
            // Both schedules are explicit: old recovery enters the local gate; rejection completes without it
            await Task.WhenAny(localEntered.Task, recovery).WaitAsync(TimeSpan.FromSeconds(5));
            releasePeer.Set();
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));
            uint peerFlags = transport.Flags(1);
            uint localFlags = transport.Flags(2);
            Check(result.Succeeded && peerFlags == 0x200 && localFlags == 0x200,
                $"Background recovery interrupted OFF: peer=0x{peerFlags:X}, local=0x{localFlags:X}; {result.FailureReason}");
            Check(recovery.IsCompleted && !await recovery && !localEntered.Task.IsCompleted
                && transport.LocalReadCalls == strictReads && transport.FlagWrites == 2
                && transport.LocalBindCalls == 1 && transport.TeammateBindCalls == 1
                && transport.HealthWrites == 0 && transport.AmmoWrites == 0,
                "Shared OFF recovery read local state, queued its receipt or changed another feature");
        } finally {
            transport.OnFlagRead = null;
            transport.OnLocalRead = null;
            releasePeer.Set();
            releaseLocal.Set();
            if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(5));
            if (recovery is not null) await recovery.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public static async Task BusyRegistryRejectsRecoveryWithoutReadingOrConsumingItsRetryDeadline() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        transport.Open(4242);
        var owner = new RetailConnection(transport, processId: 4242);
        var clock = new RecoveryTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task<GodmodeOperationResult>? operation = null;
        Task<bool>? recovery = null;
        try {
            Check(owner.ObserveOriginalConnection() && await owner.CapturePersonalGodmodeAsync(),
                "The original personal owner was not captured");
            var binding = owner.LocalGodmode!.ReadView().Binding!;
            // Explicit OFF withdraws new enable admission until a fresh capture confirms it
            Check(await owner.LocalGodmode.SetAsync(false) && !owner.LocalGodmode.ReadView().CanToggle,
                "The original OFF owner did not need recovery");
            int strictReads = transport.LocalReadCalls;
            int first = 0;
            transport.OnConnected = () => {
                if (Interlocked.Exchange(ref first, 1) != 0) return;
                entered.TrySetResult();
                release.Wait();
            };
            // The real registry publishes operation ownership before its first identity check
            operation = owner.GodmodeOwners.SetAsync(binding.Identity, false);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(owner.GodmodeOwners.IsOperating(binding.Identity), "The registry barrier did not hold its action");
            recovery = owner.RecoverPersonalGodmodeAsync(binding, clock, CancellationToken.None);
            Check(recovery.IsCompleted && !await recovery && transport.LocalReadCalls == strictReads,
                "Busy registry recovery captured local state or queued an old receipt");

            transport.OnConnected = null;
            release.Set();
            Check((await operation.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded,
                "Rejected background recovery interrupted the original registry action");
            // Rejection before capture must leave the same clock instant available for recovery
            Check(await owner.RecoverPersonalGodmodeAsync(binding, clock, CancellationToken.None)
                && owner.LocalGodmode.ReadView().CanToggle && transport.LocalReadCalls > strictReads,
                "A busy rejection consumed the next eligible recovery deadline");
            Check(transport.LocalBindCalls == 1 && transport.FlagWrites == 0
                && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.Flags(2) == 0x200,
                "Read-only recovery replaced its owner or changed game state");
        } finally {
            transport.OnConnected = null;
            release.Set();
            try {
                if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(5));
                if (recovery is not null) await recovery.WaitAsync(TimeSpan.FromSeconds(5));
            } finally {
                Check((await owner.GodmodeOwners.CloseAsync()).Restored, "The original owner did not close cleanly");
                await owner.LocalGodmode!.CloseAsync();
                transport.Dispose();
            }
        }
    }
}
