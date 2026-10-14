using Apparition.Backend.Controller;
using Apparition.Backend.Session;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class InstructionHudBoundaryTests {
    public static void ExhaustedCreationCounterRejectsDesignAndInstructionChangesBeforeAnyMutation() {
        foreach (string action in new[] { "Alternate Style", "Disable Background", "Design" }) {
            var rig = new InstructionHudBoundaryFixture(ulong.MaxValue - 1);
            rig.Open();
            rig.Select("Menu Customization");
            rig.Select(action == "Design" ? "Menu Preferences" : "Menu Instructions");
            var frame = rig.Current();
            // Cursor movement is allowed at the boundary; the selected callback is not
            int index = frame.Rows.ToList().FindIndex(row => row.Label == action);
            while (rig.Current().SelectedIndex != index) rig.Send(OverlayInput.Down);
            if (action == "Design") rig.StageChoice(OverlayInput.NextChoice);
            var before = rig.Current();
            TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
            var after = rig.Current();
            TestAssert.Equal(before.Appearance, after.Appearance);
            TestAssert.Equal(before.MenuRevision, after.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
            TestAssert.Equal(before.InstructionHud, after.InstructionHud);
            TestAssert.True(after.MenuDesignTransition is null && after.MenuOptionCallback is null,
                "A failed footer reservation cannot arm either the Design or BOOL caller timeline");
        }
    }

    public static void FailedRowsCannotCommitAnInitialOrDeathTriggeredFooterCreation() {
        var navigation = new OverlayNavigation(0, ulong.MaxValue);
        var initial = new InstructionHudBoundaryFixture(0, navigation);
        TestAssert.Throws<OverflowException>(() => initial.Current());
        TestAssert.Equal(0UL, initial.Controller.InstructionCreationCounter);

        var nearLimit = new OverlayNavigation(0, ulong.MaxValue - 2);
        nearLimit.OpenPage(MenuPage.Players);
        var death = new InstructionHudBoundaryFixture(0, nearLimit);
        death.Current();
        TestAssert.Equal(1UL, death.Controller.InstructionCreationCounter);
        death.Session.Change(LocalPlayerLife.Dead);
        death.Session.State = death.Session.State with { Players = new[] { new OverlayPlayer(1, "Changed friend") } };
        TestAssert.Throws<OverflowException>(() => death.Current());
        TestAssert.Equal(1UL, death.Controller.InstructionCreationCounter);
    }

    public static void LateDesignDrawAndClockRecoveryNeverRepeatAnAlreadyCommittedFooterRefresh() {
        foreach (int elapsed in new[] { 50, 1000 }) {
            var rig = new MenuChromeFixture();
            rig.SettleMain();
            rig.Preferences();
            rig.Highlight("Design");
            ulong first = rig.Current().InstructionHud!.CreationRevision!.Value;
            rig.StageChoice(OverlayInput.NextChoice);
            rig.Send(OverlayInput.Select);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
            rig.Controller.ReadMenuPublicationPhase();
            TestAssert.Equal(first, rig.Controller.InstructionCreationCounter);
            TestAssert.Equal(first + 1, rig.Current().InstructionHud!.CreationRevision!.Value);
            rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(elapsed - 1));
            TestAssert.Equal(first + 1, rig.Current().InstructionHud!.CreationRevision!.Value);
            rig.Wait(300);
            TestAssert.Equal(first + 1, rig.Current().InstructionHud!.CreationRevision!.Value);
            rig.NoGameWrites();
        }
    }

    public static void VaryingCachedAdmissionCannotMixFooterAndEligibilityOrStartFreshSourceReads() {
        var source = new VaryingSession();
        var controller = new BackendController(42, source);
        var frame = controller.Snapshot();
        TestAssert.True(frame.InstructionHud is { Exists: true }, "An eligible copied receipt must create the footer while closed");
        TestAssert.Equal(frame.InstructionHud!.AdmissionRevision, frame.LocalMenuEligibility!.Revision);
        TestAssert.Equal(source.Returned[frame.LocalMenuEligibility.Revision].Life == LocalPlayerLife.Alive ? "alive" : "dead",
            frame.LocalMenuEligibility.Life);
        TestAssert.Equal(0, source.FreshReads);
        // A second snapshot must keep the same single-frame fact pairing despite changing cached reads
        var next = controller.Snapshot();
        TestAssert.Equal(next.InstructionHud!.AdmissionRevision, next.LocalMenuEligibility!.Revision);
        TestAssert.Equal(0, source.FreshReads);
    }

    public static void DeathDuringDesignWaitPreservesItsLaterRefreshAndReservesBothObservedCreations() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        ulong first = rig.Current().InstructionHud!.CreationRevision!.Value;
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
        rig.Session.Change(LocalPlayerLife.Dead);
        var death = rig.Current();
        TestAssert.Equal(first + 1, death.InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("Classic", death.InstructionHud.CreationDesign);
        rig.Wait(25);
        TestAssert.Equal(first + 2, rig.Current().InstructionHud!.CreationRevision!.Value);
        rig.NoGameWrites();

        // The future Design refresh must remain reservable before publishing the death instance
        var boundary = new InstructionHudBoundaryFixture(ulong.MaxValue - 2);
        boundary.Open();
        boundary.Select("Menu Customization");
        boundary.Select("Menu Preferences");
        boundary.StageChoice(OverlayInput.NextChoice);
        boundary.Send(OverlayInput.Select);
        boundary.Clock.Advance(TimeSpan.FromMilliseconds(25));
        boundary.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Throws<OverflowException>(() => boundary.Current());
        TestAssert.Equal(ulong.MaxValue - 1, boundary.Controller.InstructionCreationCounter);
    }

    public static void TemporarilyUnavailableProofKeepsOnlyItsOriginalOwnersDelayedDesignRefresh() {
        foreach (bool permissionLost in new[] { false, true }) {
            var rig = new MenuChromeFixture();
            rig.SettleMain();
            rig.Preferences();
            rig.Highlight("Design");
            ulong first = rig.Current().InstructionHud!.CreationRevision!.Value;
            rig.StageChoice(OverlayInput.NextChoice);
            rig.Send(OverlayInput.Select);

            rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
            rig.Session.Change(permissionLost ? LocalPlayerLife.Alive : LocalPlayerLife.Unknown,
                hasMenu: !permissionLost);
            TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(5));
            rig.Session.Change(LocalPlayerLife.Alive);
            TestAssert.Equal(first + (permissionLost ? 1UL : 0UL), rig.Current().InstructionHud!.CreationRevision!.Value);
            rig.Wait(20);
            // Permission loss cancels the menu caller; Unknown life alone retains its delayed draw
            TestAssert.Equal(first + 1UL, rig.Current().InstructionHud!.CreationRevision!.Value);
            TestAssert.Equal(!permissionLost, rig.Current().IsOpen);
            rig.NoGameWrites();
        }

        // Replacement revokes the old actor's Design caller and its delayed footer cause together
        var replacement = new MenuChromeFixture();
        replacement.SettleMain();
        replacement.Preferences();
        replacement.Highlight("Design");
        ulong original = replacement.Current().InstructionHud!.CreationRevision!.Value;
        replacement.StageChoice(OverlayInput.NextChoice);
        replacement.Send(OverlayInput.Select);
        replacement.Clock.Advance(TimeSpan.FromMilliseconds(25));
        replacement.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        TestAssert.Equal(original + 1, replacement.Current().InstructionHud!.CreationRevision!.Value);
        replacement.Wait(25);
        TestAssert.Equal(original + 1, replacement.Current().InstructionHud!.CreationRevision!.Value);
    }

    private sealed class VaryingSession : IBackendSession {
        private readonly ScopedOpeningFixture.OpeningSession source = new();
        private readonly object owner = new();
        private ulong revision;
        public Dictionary<ulong, LocalPlayerObservation> Returned { get; } = new();
        public int FreshReads { get; private set; }
        public bool RequiresLocalMenuAdmission => true;
        public OverlayMenuState ReadMenuState() => source.State;
        public LocalPlayerObservation ReadLocalObservation() => source.ReadLocalObservation();
        public LocalMenuAdmissionSnapshot ReadLocalMenuAdmission() {
            ulong current = ++revision;
            var observation = source.ReadLocalObservation() with { Life = current % 2 == 0 ? LocalPlayerLife.Alive : LocalPlayerLife.Dead };
            Returned.Add(current, observation);
            return new(current, observation, owner);
        }
        public Task RefreshLocalObservationAsync(CancellationToken token = default) { FreshReads++; return Task.CompletedTask; }
        public Task ExecuteAsync(MenuAction action) => throw new InvalidOperationException("The frame must not dispatch a game action");
        public Task<syndishanx_iw_tool.SessionLifecycle.SessionShutdownResult> ShutdownAsync() => throw new InvalidOperationException("The frame must not start shutdown");
    }
}
