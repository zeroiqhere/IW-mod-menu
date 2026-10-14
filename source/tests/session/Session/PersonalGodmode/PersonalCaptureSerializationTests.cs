using syndishanx_iw_tool.Features.Godmode;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class PersonalCaptureSerializationTests {
    public static async Task RetainedCaptureWaitsForSharedAdmissionAndUsesOnlyFreshProof() {
        // Both unavailable and healthy post-admission proof must be handled from the current reader
        foreach (var (hadAdapter, freshProofAvailable) in new[] {
            (true, false), (true, true), (false, false), (false, true)
        }) {
            await using var fixture = new SharedGodmodeFixture(localSlot: 2);
            var owner = fixture.Owner;
            var transport = fixture.Transport;
            if (hadAdapter) {
                await fixture.CaptureLocalAsync();
                Check(await owner.LocalGodmode!.SetAsync(false), "The retained OFF owner was unavailable");
            } else {
                // A foreign flag can latch full identity before any local adapter is constructed
                transport.SetFlags(2, 0x201);
                Check(!await owner.CapturePersonalGodmodeAsync(), "A foreign ON flag became an owned adapter");
                transport.SetFlags(2, 0x200);
            }
            var binding = owner.LocalGodmode!.ReadView().Binding!;
            Check(!owner.LocalGodmode.ReadView().CanToggle, "The retained owner did not require fresh capture");
            var actionIdentity = hadAdapter ? binding.Identity : fixture.Identity(1);
            if (!hadAdapter) Check((await owner.GodmodeOwners.CaptureAsync(new[] { actionIdentity })).Succeeded,
                "The peer action owner could not hold shared admission");
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            Task<GodmodeOperationResult>? operation = null;
            Task<bool>? capture = null;
            int first = 0;
            void HoldFirstRead() {
                if (Interlocked.Exchange(ref first, 1) != 0) return;
                entered.TrySetResult();
                release.Wait();
            }
            // Peer flag callbacks run outside fixture storage locks, leaving strict local reads available
            if (hadAdapter) transport.OnConnected = HoldFirstRead;
            else transport.OnFlagRead = HoldFirstRead;
            try {
                // The registry action owns admission while the local strict reader is still available
                operation = owner.GodmodeOwners.SetAsync(actionIdentity, false);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                int reads = transport.LocalReadCalls;
                capture = owner.CapturePersonalGodmodeAsync();
                bool readWhileQueued = transport.LocalReadCalls != reads;
                Check(!capture.IsCompleted, "The retained capture did not wait for the shared action");
                // Only strict eligibility fails; the original adapter can still validate its clear word
                transport.FailLocalRead = !freshProofAvailable;
                transport.OnConnected = null;
                transport.OnFlagRead = null;
                release.Set();
                var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));
                bool captured = await capture.WaitAsync(TimeSpan.FromSeconds(5));
                var view = owner.LocalGodmode.ReadView();
                Check(result.Succeeded && !readWhileQueued && captured == freshProofAvailable
                    && view.CanToggle == freshProofAvailable
                    && view.Value == (freshProofAvailable ? GodmodeValue.Off : GodmodeValue.Unknown),
                    $"Retained capture used stale proof: hadAdapter={hadAdapter}, freshProofAvailable={freshProofAvailable}, shared={result.Succeeded}, readWhileQueued={readWhileQueued}, captured={captured}, canToggle={view.CanToggle}, value={view.Value}");
                Check(view.Binding == binding && transport.LocalBindCalls == (hadAdapter || freshProofAvailable ? 1 : 0)
                    && transport.TeammateBindCalls == (hadAdapter ? 0 : 1) && transport.FlagWrites == 0
                    && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.Flags(2) == 0x200,
                    "Failed read-only refresh changed its original owner or another feature");
            } finally {
                transport.OnConnected = null;
                transport.OnFlagRead = null;
                transport.FailLocalRead = false;
                release.Set();
                if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(5));
                if (capture is not null) await capture.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    public static async Task CancelledRetainedCaptureWaitsWithoutReadingAndPreservesItsOwner() {
        await using var fixture = new SharedGodmodeFixture(localSlot: 2);
        var owner = fixture.Owner;
        var transport = fixture.Transport;
        await fixture.CaptureLocalAsync();
        var binding = owner.LocalGodmode!.ReadView().Binding!;
        Check(await owner.LocalGodmode.SetAsync(false), "The retained OFF owner was unavailable");
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task<GodmodeOperationResult>? operation = null;
        Task<bool>? capture = null;
        int first = 0;
        transport.OnConnected = () => {
            if (Interlocked.Exchange(ref first, 1) != 0) return;
            entered.TrySetResult();
            release.Wait();
        };
        try {
            operation = owner.GodmodeOwners.SetAsync(binding.Identity, false);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            int reads = transport.LocalReadCalls;
            capture = owner.CapturePersonalGodmodeAsync(cancellation.Token);
            cancellation.Cancel();
            bool captured = await capture.WaitAsync(TimeSpan.FromSeconds(5));
            bool readWhileQueued = transport.LocalReadCalls != reads;
            transport.OnConnected = null;
            release.Set();
            Check((await operation.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded && !captured
                && !readWhileQueued && !owner.GodmodeOwners.IsOperating(binding.Identity),
                $"Cancelled retained capture read while queued: captured={captured}, readWhileQueued={readWhileQueued}");
            // A later capture proves cancellation released admission without replacing the owner
            Check(await owner.CapturePersonalGodmodeAsync() && owner.LocalGodmode.ReadView().Binding == binding
                && owner.LocalGodmode.ReadView().CanToggle && transport.LocalBindCalls == 1
                && transport.FlagWrites == 0 && transport.HealthWrites == 0 && transport.AmmoWrites == 0,
                "Cancelled refresh retained admission, changed the owner or blocked later capture");
        } finally {
            transport.OnConnected = null;
            release.Set();
            if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(5));
            if (capture is not null) await capture.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
