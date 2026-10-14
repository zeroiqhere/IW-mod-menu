using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class RetailPersonalOwnershipTests {
    public static async Task ExplicitPersonalRestoreAfterDeathNeverBecomesANewEnable() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            transport.Health = 0;
            transport.LocalSessionState = 1;
            transport.FailLocalRead = true;
            int strictReads = transport.LocalReadCalls;
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            Check(transport.Flags(3) == 0x200 && transport.LocalFlagWrites == 2
                && transport.LocalReadCalls == strictReads && transport.HealthWrites == 0,
                "Explicit restoration read strict life or became a repeated enable");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task SelectingVerifiedSelfInANonzeroSlotUsesOnlyThePersonalOwner() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(2));
            var selected = session.ReadMenuState();
            Check(selected.SelectedSlot == 2 && selected.PersonalGodmodeValue == "OFF"
                && !selected.CanTeammateGodmode && transport.TeammateBindCalls == 0,
                "Verified self selection constructed a second teammate writer");
            await session.ExecuteAsync(Toggle(2));
            Check(transport.Flags(2) == 0x201 && transport.LocalBindCalls == 1 && transport.TeammateBindCalls == 0
                && session.ReadMenuState().PersonalGodmodeValue == "ON",
                "Selected self did not use the existing exact personal owner");
            await session.ExecuteAsync(Select(1));
            await session.ExecuteAsync(Toggle(1));
            Check(transport.Flags(1) == 0x201 && transport.Flags(2) == 0x201,
                "A different teammate could not keep an independent flag owner");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task LocalReservationBlocksDirectTeammateBindingEvenAfterConfirmedOff() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        transport.Open(4242);
        var owner = new RetailConnection(transport, processId: 4242);
        try {
            Check(await owner.LocalGodmode!.CaptureAsync(), "Verified local reservation setup failed");
            Check(!await owner.Teammates.SelectAsync(2, "Friend2") && transport.TeammateBindCalls == 0,
                "A direct teammate request constructed a writer for the reserved local actor");
            Check(await owner.LocalGodmode.ToggleAsync() && await owner.LocalGodmode.RestoreAsync(),
                "Reservation OFF setup failed");
            Check(!await owner.Teammates.SelectAsync(2, "Friend2") && transport.TeammateBindCalls == 0,
                "Confirmed OFF released a still retained local adapter reservation");
        } finally {
            await owner.LocalGodmode!.CloseAsync();
            await owner.Teammates.CloseAsync();
            transport.Dispose();
        }
    }

    public static async Task TeammateFirstReservationCannotBeBorrowedByLaterLocalVerification() {
        var transport = new RecordingRetailTransport { LocalSlot = 2, FailLocalRead = true };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(2));
            await session.ExecuteAsync(Toggle(2));
            await session.ExecuteAsync(Toggle(2));
            transport.FailLocalRead = false;
            int writes = transport.FlagWrites;
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            var frame = session.ReadMenuState();
            Check(frame.PersonalGodmodeValue == "UNKNOWN" && !frame.CanPersonalGodmode
                && frame.PersonalGodmodeIdentity is null && transport.LocalBindCalls == 0 && transport.TeammateBindCalls == 1
                && transport.FlagWrites == writes && transport.Flags(2) == 0x200,
                "Late local verification borrowed an already retained teammate writer");
            await session.ExecuteAsync(Toggle(2));
            await session.ExecuteAsync(Toggle(2));
            Check(transport.Flags(2) == 0x200 && transport.LocalBindCalls == 0,
                "Failed local verification stole the existing teammate recovery route");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task TeammateCaptureRetryReusesItsSuccessfullyConstructedOriginalAdapter() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            transport.OnFlagRead = () => {
                transport.OnFlagRead = null;
                transport.SetFlags(1, 0x201);
            };
            await session.ExecuteAsync(Select(1));
            Check(transport.TeammateBindCalls == 1 && session.ReadMenuState().SelectedSlot == -1,
                "The failed after-construction capture did not preserve its original writer");
            transport.SetFlags(1, 0x200);
            await session.ExecuteAsync(Select(1));
            await session.ExecuteAsync(Toggle(1));
            Check(transport.TeammateBindCalls == 1 && transport.Flags(1) == 0x201,
                "Retry recreated a retained teammate adapter or lost its original pin");
        } finally {
            transport.OnFlagRead = null;
            await session.ShutdownAsync();
        }
    }
    public static async Task PersonalRestoreCapabilityIsDisabledWhileItsRealMonitorIsJoining() {
        var transport = new RecordingRetailTransport();
        var session = Create(transport);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = 0;
        transport.OnLocalFlagRead = () => {
            if (Interlocked.Exchange(ref first, 1) != 0) return;
            entered.TrySetResult();
            release.Wait();
        };
        Task? restoring = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // This actual worker call starts OFF synchronously and waits on its held monitor
            restoring = session.ExecuteOnWorkerAsync(new(MenuActionKind.RestorePersonalGodmode));
            var frame = session.ReadMenuState();
            Check(!restoring.IsCompleted && !frame.CanPersonalGodmode && !frame.CanRestorePersonalGodmode,
                "An unfinished local operation exposed a second restore command");
        } finally {
            transport.OnLocalFlagRead = null;
            release.Set();
            if (restoring is not null) await restoring.WaitAsync(TimeSpan.FromSeconds(5));
            await session.ShutdownAsync();
        }
    }

    public static async Task PersonalMenuRowCanEnableAgainAfterAnOrdinaryConfirmedOff() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            foreach (string expected in new[] { "ON", "OFF", "ON" }) {
                var before = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
                Check(before.Label == "God Mode" && before.Enabled,
                    "The actual personal menu row lost its ordinary toggle after OFF");
                await session.ExecuteAsync(before.Action);
                var after = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
                Check(after.Value == expected, "The actual personal row did not confirm its requested state");
            }
            Check(transport.LocalBindCalls == 1 && transport.LocalFlagWrites == 3
                && transport.Flags(2) == 0x201 && transport.HealthWrites == 0,
                "Repeated personal toggles changed writer, target or legacy health");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task RefreshPlayersRecapturesReturnedPersonalEligibilityWithoutEnablingFlags() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            transport.Health = 0;
            transport.LocalSessionState = 1;
            transport.FailLocalRead = true;
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            Check(!OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0].Enabled,
                "An ineligible restored actor retained new enable permission");

            // The same binding becomes eligible again; refresh only observes its current flags
            transport.Health = 100;
            transport.LocalSessionState = 0;
            transport.FailLocalRead = false;
            int writes = transport.FlagWrites;
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var row = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
            Check(row.Enabled && row.Value == "OFF" && transport.FlagWrites == writes
                && transport.LocalBindCalls == 1, "Read-only refresh failed to restore same-actor eligibility or enabled flags");
            await session.ExecuteAsync(row.Action);
            Check(transport.Flags(3) == 0x201, "A refreshed actual personal row could not enable its original actor");
        } finally { await session.ShutdownAsync(); }
    }

}
