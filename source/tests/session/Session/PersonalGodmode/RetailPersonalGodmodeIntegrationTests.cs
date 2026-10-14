using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// These checks execute the production session against owned flags and actual bound adapters
internal static class RetailPersonalGodmodeIntegrationTests {
    public static async Task PersonalToggleAndRestoreFailuresReportOperationReasonAndFinalState() {
        foreach (var offAction in new[] { MenuActionKind.TogglePersonalGodmode, MenuActionKind.RestorePersonalGodmode }) {
            string prefix = offAction == MenuActionKind.RestorePersonalGodmode ? "Restore your Godmode: " : "Your Godmode: ";
            string failureLabel = offAction == MenuActionKind.RestorePersonalGodmode ? "restoration" : "toggle";
            var transport = new RecordingRetailTransport { LocalSlot = 2 };
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
                var enabled = session.ReadMenuState();
                Check(enabled.PersonalGodmodeValue == "ON"
                    && enabled.RecentMessages.EndsWith("Your Godmode: On", StringComparison.Ordinal)
                    && !enabled.RecentMessages.Contains("toggle failed", StringComparison.Ordinal),
                    "A successful personal ON did not keep its clean history entry");

                await session.ExecuteAsync(new(offAction));
                var disabled = session.ReadMenuState();
                Check(disabled.PersonalGodmodeValue == "OFF"
                    && disabled.RecentMessages.EndsWith(prefix + disabled.PersonalGodmodeStatus, StringComparison.Ordinal)
                    && !disabled.RecentMessages.Contains(failureLabel + " failed", StringComparison.Ordinal),
                    "A successful personal OFF did not keep its clean history entry");

                await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
                transport.RejectLocalFlagRestore = true;
                await session.ExecuteAsync(new(offAction));
                var failed = session.ReadMenuState();
                Check(transport.Flags(2) == 0x201 && transport.LocalFlagWrites == 3
                    && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.LocalBindCalls == 1
                    && failed.PersonalGodmodeValue == "UNKNOWN"
                    && failed.PersonalGodmodeStatus == "Restoration was rejected or could not be confirmed",
                    "Failed personal OFF changed its owner or lost the actual final state and status");
                Check(failed.RecentMessages.EndsWith(
                    prefix + "UNKNOWN (" + failureLabel + " failed: Restoration was rejected or could not be confirmed)",
                    StringComparison.Ordinal),
                    "A failed personal OFF omitted the operation reason or final state: " + failed.RecentMessages);
            } finally {
                transport.RejectLocalFlagRestore = false;
                await session.ShutdownAsync();
            }
        }
    }

    public static async Task PersonalToggleAndRestoreFailuresKeepRetainedOnStateAndStatus() {
        foreach (var offAction in new[] { MenuActionKind.TogglePersonalGodmode, MenuActionKind.RestorePersonalGodmode }) {
            string prefix = offAction == MenuActionKind.RestorePersonalGodmode ? "Restore your Godmode: " : "Your Godmode: ";
            string failureLabel = offAction == MenuActionKind.RestorePersonalGodmode ? "restoration" : "toggle";
            var transport = new RecordingRetailTransport { LocalSlot = 2 };
            var session = Create(transport);
            var monitorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseMonitor = new ManualResetEventSlim();
            Task? failedOff = null;
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
                Check(session.ReadMenuState().PersonalGodmodeValue == "ON", "Personal ON setup was not confirmed");

                int firstRead = 0;
                transport.OnLocalFlagRead = () => {
                    if (Interlocked.Exchange(ref firstRead, 1) != 0) return;
                    monitorEntered.TrySetResult();
                    releaseMonitor.Wait();
                };
                await monitorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // Two session liveness reads precede the registry identity check; release the monitor for fresh flag snapshots
                int connectedReads = 0;
                transport.OnConnected = () => {
                    if (Interlocked.Increment(ref connectedReads) != 3) return;
                    transport.OnConnected = null;
                    releaseMonitor.Set();
                    throw new IOException("Owned identity query failed once");
                };
                failedOff = session.ExecuteAsync(new(offAction));
                await failedOff.WaitAsync(TimeSpan.FromSeconds(5));

                var failed = session.ReadMenuState();
                Check(transport.Flags(2) == 0x201 && transport.LocalFlagWrites == 1
                    && transport.HealthWrites == 0 && transport.AmmoWrites == 0 && transport.LocalBindCalls == 1
                    && failed.PersonalGodmodeValue == "ON" && failed.PersonalGodmodeStatus == "On",
                    "A failed OFF changed the retained personal state or status");
                Check(failed.RecentMessages.EndsWith(
                    prefix + "ON (" + failureLabel + " failed: Owned identity query failed once)", StringComparison.Ordinal),
                    "Personal history lost the operation reason while the local view stayed ON: " + failed.RecentMessages);
            } finally {
                transport.OnConnected = null;
                transport.OnLocalFlagRead = null;
                releaseMonitor.Set();
                if (failedOff is not null) await failedOff.WaitAsync(TimeSpan.FromSeconds(5));
                await session.ShutdownAsync();
            }
        }
    }

    public static async Task VerifiedPersonalGodmodeChangesOnlyTheAuthenticatedActorFlagForEverySlot() {
        for (int slot = 0; slot < 4; slot++) {
            var transport = new RecordingRetailTransport { LocalSlot = slot };
            var session = Create(transport);
            try {
                await session.ExecuteAsync(new(MenuActionKind.Connect));
                var off = session.ReadMenuState();
                Check(off.PersonalGodmodeValue == "OFF" && off.CanPersonalGodmode
                    && off.PersonalGodmodeIdentity == transport.ReadTeammateState(slot).Identity,
                    "The session did not expose the verified personal actor");
                await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
                Check(transport.Flags(slot) == 0x201 && transport.HealthWrites == 0 && transport.LocalFlagWrites == 1,
                    "Personal Godmode used health or changed a different player");
                for (int other = 0; other < 4; other++) {
                    if (other != slot) Check(transport.Flags(other) == 0x200, "Personal Godmode changed another actor");
                }
                Check(session.ReadMenuState().PersonalGodmodeValue == "ON", "Confirmed personal flag was not shown as ON");
            } finally {
                Check((await session.ShutdownAsync()).Restored, "The original personal actor was not restored");
            }
            Check(transport.Flags(slot) == 0x200 && transport.DisposeCalls == 1,
                "Shutdown did not restore the original flags before disposal");
        }
    }

    public static async Task FailedLocalVerificationRemainsUnknownWithoutHealthFallback() {
        var transport = new RecordingRetailTransport { FailLocalRead = true };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var frame = session.ReadMenuState();
            Check(frame.Connected && frame.PersonalGodmodeValue == "UNKNOWN" && !frame.CanPersonalGodmode
                && frame.PersonalGodmodeIdentity is null, "Missing local proof exposed a personal capability");
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            Check(transport.HealthWrites == 0 && transport.FlagWrites == 0 && transport.LocalBindCalls == 0,
                "Missing local proof reached a legacy health or flags writer");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task PersonalCaptureDoesNotHoldTheLifecycleGateOrBlockCachedFrames() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var transport = new RecordingRetailTransport { OnLocalRead = () => {
            entered.TrySetResult();
            release.Wait();
        } };
        var session = Create(transport);
        var connecting = session.ExecuteAsync(new(MenuActionKind.Connect));
        Task? shutdown = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (int index = 0; index < 1000; index++) {
                var frame = session.ReadMenuState();
                Check(frame.PersonalGodmodeValue == "UNKNOWN" && !frame.CanPersonalGodmode,
                    "An unfinished capture claimed a personal state");
            }
            // This real admission must finish while the local native read is held
            await session.ExecuteAsync(new(MenuActionKind.None)).WaitAsync(TimeSpan.FromSeconds(2));
            shutdown = session.ShutdownAsync();
            Check(!shutdown.IsCompleted && transport.DisposeCalls == 0,
                "Cleanup released the transport while its local source callback was live");
        } finally {
            transport.OnLocalRead = null;
            release.Set();
            await connecting.WaitAsync(TimeSpan.FromSeconds(5));
            if (shutdown is not null) await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            else await session.ShutdownAsync();
        }
        Check(transport.LocalBindCalls == 0 && transport.FlagWrites == 0 && transport.DisposeCalls == 1,
            "Late capture created an owner after shutdown admission");
    }

    public static async Task FailedPersonalRestoreRetainsTransportUntilOriginalCleanupRetry() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        transport.RejectLocalFlagRestore = true;
        var failed = await session.ShutdownAsync();
        Check(!failed.Stopped && !failed.Restored && transport.DisposeCalls == 0
            && !session.ReadMenuState().CanConnect && transport.Flags(2) == 0x201,
            "Unconfirmed local restoration released its original transport");
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        Check(transport.OpenCalls == 1, "An unresolved local owner allowed reconnect");
        transport.RejectLocalFlagRestore = false;
        session.AllowShutdownRetry();
        var retried = await session.ShutdownAsync();
        Check(retried.Stopped && retried.Restored && transport.Flags(2) == 0x200 && transport.DisposeCalls == 1,
            "Retry did not restore through the original personal owner");
    }
    public static async Task TransportWithoutLocalCapabilityCannotFallBackToTheLegacyHealthWriter() {
        var storage = new RecordingRetailTransport();
        var unsupported = new UnsupportedLocalRetailTransport(storage);
        var session = new RetailBackendSession(4242, () => true, () => unsupported, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            var frame = session.ReadMenuState();
            Check(frame.Connected && frame.PersonalGodmodeRequiresVerifiedTarget
                && frame.PersonalGodmodeValue == "UNKNOWN" && frame.PersonalGodmodeIdentity is null
                && !frame.CanPersonalGodmode && !frame.CanRestorePersonalGodmode,
                "A transport without a local flag source exposed a personal capability");
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            Check(storage.LocalReadCalls == 0 && storage.HealthWrites == 0 && storage.FlagWrites == 0,
                "Unsupported personal actions reached a legacy writer or local source");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task RepeatedPersonalFramesUseOnlyCachedLocalMetadata() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            int sourceReads = transport.LocalReadCalls;
            transport.OnLocalRead = () => throw new IOException("A frame must not read the local source");
            transport.OnLocalFlagRead = () => throw new IOException("A frame must not preflight the native local adapter");
            for (int index = 0; index < 1000; index++) {
                var frame = session.ReadMenuState();
                Check(frame.PersonalGodmodeValue == "OFF" && frame.CanPersonalGodmode,
                    "A cached confirmed frame changed without a worker observation");
            }
            Check(transport.LocalReadCalls == sourceReads && transport.HealthWrites == 0 && transport.FlagWrites == 0,
                "Frame publication invoked local source or feature writes");
        } finally {
            transport.OnLocalRead = null;
            transport.OnLocalFlagRead = null;
            await session.ShutdownAsync();
        }
    }

}
