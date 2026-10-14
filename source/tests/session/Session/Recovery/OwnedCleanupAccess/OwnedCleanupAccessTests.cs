using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Missing actor admission permits only a deliberate cleanup visit for retained changes
internal static class OwnedCleanupAccessTests {
    public static async Task FreshForegroundOpeningExposesOnlyExitAndRestoresBothOriginalFlagOwners() {
        foreach (bool readFailure in new[] { false, true }) {
            await using var rig = new OwnedCleanupAccessRig();
            await rig.ConnectAsync();
            await rig.OwnFlagsAsync();
            await rig.OpenNormalAsync();
            int writes = rig.Transport.Inner.FlagWrites;
            int binds = rig.Transport.Inner.LocalBindCalls + rig.Transport.Inner.TeammateBindCalls;
            await rig.LoseAdmissionAsync(readFailure);
            Check(!rig.Controller.Snapshot().IsOpen && !rig.Controller.ExitRequested
                && !rig.Session.ReadMenuState().NeedsSessionRecovery && rig.Transport.Inner.FlagWrites == writes,
                "Lost actor information opened cleanup or changed flags automatically");
            rig.Send(OverlayInput.Toggle, foreground: false);
            rig.Send(OverlayInput.Toggle, replay: true);
            rig.Send(OverlayInput.Toggle, processId: 4243);
            Check(!rig.Controller.Snapshot().IsOpen, "An unfocused, replayed, or foreign opening granted cleanup access");
            rig.Send(OverlayInput.Toggle);
            AssertExitOnly(rig);
            Check(rig.Transport.Inner.FlagWrites == writes
                && rig.Transport.Inner.LocalBindCalls + rig.Transport.Inner.TeammateBindCalls == binds,
                "Opening the administrative menu wrote flags or replaced an original owner");
            rig.Send(OverlayInput.Select);
            Check(rig.Controller.ExitRequested, "The sole Exit row failed to request existing shutdown");
            var result = await rig.Session.ShutdownAsync();
            Check(result.Stopped && result.Restored && rig.Transport.Inner.Flags(2) == 0x200
                && rig.Transport.Inner.Flags(1) == 0x200 && rig.Transport.Inner.HealthWrites == 0
                && rig.Transport.Inner.AmmoWrites == 0, "Explicit Exit failed to restore both original flag words");
        }
    }

    public static async Task BackAndToggleCloseWithoutCleanupAndHealthyAdmissionWithdrawsTheVisit() {
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        int writes = rig.Transport.Inner.FlagWrites;
        int binds = rig.Transport.Inner.LocalBindCalls + rig.Transport.Inner.TeammateBindCalls;
        foreach (var close in new[] { OverlayInput.Back, OverlayInput.Toggle }) {
            rig.Send(OverlayInput.Toggle);
            AssertExitOnly(rig);
            rig.Send(close);
            Check(!rig.Controller.Snapshot().IsOpen && !rig.Controller.ExitRequested
                && rig.Transport.Inner.FlagWrites == writes, "Closing a cleanup visit also performed cleanup");
        }
        rig.Send(OverlayInput.Toggle);
        AssertExitOnly(rig);
        await rig.RecoverAdmissionAsync();
        Check(!rig.Controller.Snapshot().IsOpen, "Healthy actor permission silently inherited the administrative visit");
        await rig.OpenNormalAsync();
        Check(rig.Controller.Snapshot().Rows.Any(row => row.Label == "Basic Scripts")
            && rig.Transport.Inner.FlagWrites == writes
            && rig.Transport.Inner.LocalBindCalls + rig.Transport.Inner.TeammateBindCalls == binds,
            "Healthy opening stayed cleanup-only or changed the retained owners");
    }

    public static async Task RegisteredOffOwnersCannotGrantActorlessCleanupAccess() {
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        int writes = rig.Transport.Inner.FlagWrites;
        rig.Send(OverlayInput.Toggle);
        Check(!rig.Controller.Snapshot().IsOpen && !rig.Controller.ExitRequested
            && rig.Transport.Inner.FlagWrites == writes, "Inactive registered owners granted a cleanup visit");
    }

    public static async Task PersonalPeerAndAmmoOwnersEachPermitCachedExitOnlyAccess() {
        foreach (string kind in new[] { "personal", "peer", "ammo" }) {
            await using var rig = new OwnedCleanupAccessRig();
            await rig.ConnectAsync();
            if (kind == "ammo") await rig.Session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
            else await rig.OwnFlagsAsync(personal: kind == "personal", peer: kind == "peer");
            await rig.OpenNormalAsync();
            await rig.LoseAdmissionAsync();
            var reads = (rig.Transport.ActorReads, rig.Transport.CohortReads,
                rig.Transport.Inner.LocalReadCalls, rig.Transport.Inner.AmmoReadCalls);
            var writes = (rig.Transport.Inner.FlagWrites, rig.Transport.Inner.AmmoWrites);
            rig.Send(OverlayInput.Toggle);
            AssertExitOnly(rig);
            foreach (var input in Enum.GetValues<OverlayInput>().Where(input => input is not
                (OverlayInput.Toggle or OverlayInput.Back or OverlayInput.Close or OverlayInput.Select))) {
                rig.Send(input);
                AssertExitOnly(rig);
            }
            rig.Send(OverlayInput.Select, foreground: false);
            rig.Send(OverlayInput.Select, replay: true);
            var frame = rig.Controller.Snapshot();
            rig.SelectWire(frame.NavigationRevision + 1);
            rig.SelectWire(frame.NavigationRevision, localRevision: 1);
            AssertExitOnly(rig);
            Check(!rig.Controller.ExitRequested && reads == (rig.Transport.ActorReads, rig.Transport.CohortReads,
                rig.Transport.Inner.LocalReadCalls, rig.Transport.Inner.AmmoReadCalls)
                && writes == (rig.Transport.Inner.FlagWrites, rig.Transport.Inner.AmmoWrites),
                "Administrative snapshots or rejected commands performed adapter reads or feature writes");
            // The same serialized selection that the native worker emits reaches existing shutdown
            rig.SelectWire(frame.NavigationRevision);
            var result = await rig.Session.ShutdownAsync();
            Check(rig.Controller.ExitRequested && result.Stopped && result.Restored,
                "An individual original resource failed administrative restoration");
        }
    }

    public static async Task RemovedChangesAndReplacementConnectionsWithdrawTheCapturedCleanupVisit() {
        foreach (bool replacement in new[] { false, true }) {
            await using var rig = new OwnedCleanupAccessRig();
            await rig.ConnectAsync();
            await rig.OwnFlagsAsync();
            await rig.OpenNormalAsync();
            await rig.LoseAdmissionAsync();
            rig.Send(OverlayInput.Toggle);
            AssertExitOnly(rig);
            var old = rig.Controller.Snapshot();
            if (replacement) {
                Check((await rig.Session.ShutdownAsync()).Stopped, "The owned original connection failed to stop");
                rig.ReplaceRecordedTransport();
                await rig.ConnectAsync();
            }
            // A fresh original connection with its own ON owners must not inherit the old Exit capture
            await rig.OwnFlagsAsync();
            if (replacement) await rig.LoseAdmissionAsync();
            int writes = rig.Transport.Inner.FlagWrites;
            Check(!rig.Controller.Snapshot().IsOpen, "A retired cleanup owner retained an open administrative menu");
            rig.SelectWire(old.NavigationRevision);
            Check(!rig.Controller.ExitRequested && rig.Transport.Inner.FlagWrites == writes,
                "Stale Exit selected a replacement owner or performed cleanup after responsibility ended");
            if (replacement) {
                rig.Send(OverlayInput.Toggle);
                AssertExitOnly(rig);
            } else {
                rig.Send(OverlayInput.Toggle);
                Check(!rig.Controller.Snapshot().IsOpen, "Removed changes could reacquire cleanup permission");
            }
        }
    }

    public static async Task StaleOpeningPolicyCannotSelectExitForAnOtherwiseCurrentCleanupVisit() {
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        rig.Send(OverlayInput.Toggle);
        AssertExitOnly(rig);
        var frame = rig.Controller.Snapshot();
        int writes = rig.Transport.Inner.FlagWrites;
        rig.SelectWire(frame.NavigationRevision, openingRevision: frame.MainOpeningControls!.Revision + 1);
        Check(!rig.Controller.ExitRequested && rig.Transport.Inner.FlagWrites == writes,
            "A stale opening-policy capture requested administrative shutdown");
        AssertExitOnly(rig);
    }

    public static async Task MissingOrStaleOpeningScopesCannotOpenOwnedCleanup() {
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        var frame = rig.Controller.Snapshot();
        foreach (var scope in new (ulong? Navigation, ulong? Policy)[] {
            (null, null), (frame.NavigationRevision, null), (null, frame.MainOpeningControls!.Revision),
            (frame.NavigationRevision + 1, frame.MainOpeningControls.Revision),
            (frame.NavigationRevision, frame.MainOpeningControls.Revision + 1) }) {
            rig.CapturedToggle(scope.Navigation, scope.Policy);
            Check(!rig.Controller.Snapshot().IsOpen && !rig.Controller.ExitRequested,
                "An incomplete or stale opening capture acquired owned cleanup access");
        }
        rig.CapturedToggle(frame.NavigationRevision, frame.MainOpeningControls.Revision);
        AssertExitOnly(rig);
    }

    public static async Task ReplacingAClosedCleanupOwnerInvalidatesThePreviouslyDisplayedOpeningScope() {
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        var old = rig.Controller.Snapshot();
        Check((await rig.Session.ShutdownAsync()).Stopped, "The original closed cleanup owner did not stop");
        rig.ReplaceRecordedTransport();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.LoseAdmissionAsync();
        var current = rig.Controller.Snapshot();
        Check(current.NavigationRevision != old.NavigationRevision && !current.IsOpen,
            "A replacement cleanup owner inherited the original closed opening epoch");
        rig.CapturedToggle(old.NavigationRevision, old.MainOpeningControls!.Revision);
        Check(!rig.Controller.Snapshot().IsOpen && !rig.Controller.ExitRequested,
            "A delayed opening captured a different original connection");
        rig.CapturedToggle(current.NavigationRevision, current.MainOpeningControls!.Revision);
        AssertExitOnly(rig);
    }

    private static void AssertExitOnly(OwnedCleanupAccessRig rig) {
        var frame = rig.Controller.Snapshot();
        Check(frame.IsOpen && frame.IsRootMenu && frame.Rows.Count == 1
            && frame.Rows[0].Label == "Exit" && frame.Rows[0].Enabled
            && frame.LocalMenuEligibility is null && frame.MenuUseContinuation is null
            && frame.MenuOptionCallback is null && !rig.Controller.Busy,
            "Missing player information did not expose exactly one administrative Exit action");
    }
}
