using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Real controller frames distinguish missing current proof from destruction of owned source widgets
internal static class FooterWithdrawalControllerTests {
    internal static MenuChromeFixture Palette() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Select("Red");
        rig.Wait(180);
        return rig;
    }

    internal static void Withdrawn(StateDocument frame) {
        TestAssert.Equal(InstructionHudState.Absent, frame.InstructionHud);
        TestAssert.True(frame.InstructionTheme is null, "Unknown life must not publish current footer color authority");
    }

    public static void SameOwnerUnknownLifeRetainsCreationAndThemeHistoryWhileOpenOrClosed() {
        foreach (bool close in new[] { false, true }) {
            var rig = Palette();
            if (close) {
                rig.Wait(250);
                rig.Send(OverlayInput.Toggle);
            }
            var before = rig.Current();
            rig.Session.Change(LocalPlayerLife.Unknown);
            Withdrawn(rig.Current());
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            Withdrawn(rig.Current());
            rig.Session.Change(LocalPlayerLife.Alive);
            var recovered = rig.Current();
            TestAssert.Equal(before.InstructionHud!.CreationRevision, recovered.InstructionHud!.CreationRevision);
            TestAssert.Equal(before.InstructionHud.CreationAccent, recovered.InstructionHud.CreationAccent);
            TestAssert.Equal(before.InstructionHud.CreationDesign, recovered.InstructionHud.CreationDesign);
            TestAssert.Equal(before.InstructionHud.CreationRainbow, recovered.InstructionHud.CreationRainbow);
            TestAssert.Equal(before.InstructionTheme!.Events.Single().Revision, recovered.InstructionTheme!.Events.Single().Revision);
            TestAssert.Equal(before.InstructionTheme.Events.Single().ElapsedMilliseconds + 250,
                recovered.InstructionTheme.Events.Single().ElapsedMilliseconds);
            TestAssert.Equal(!close, recovered.IsOpen);
            rig.NoGameWrites();
        }
        var clock = new WithdrawalClock();
        var closed = new MenuChromeFixture(clock: clock);
        closed.SettleMain();
        closed.Select("Menu Customization");
        closed.Select("Main Design Color");
        closed.Select("Red");
        closed.Wait(430);
        closed.Send(OverlayInput.Toggle);
        closed.Session.Change(LocalPlayerLife.Unknown);
        clock.Fail = true;
        Withdrawn(closed.Current());
        clock.Fail = false;
        closed.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(1, closed.Current().InstructionTheme!.Events.Count);
        closed.NoGameWrites();
    }

    private sealed class WithdrawalClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new();
        public bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Withdrawal must not sample the footer clock") : Time.GetTimestamp();
    }

    public static void KnownDeathAcrossUnknownRecreatesOnceButRevivalRetainsTheDeadCreation() {
        var rig = Palette();
        ulong initial = rig.Current().InstructionHud!.CreationRevision!.Value;
        rig.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(rig.Current());
        rig.Session.Change(LocalPlayerLife.Dead);
        var death = rig.Current();
        TestAssert.Equal(initial + 1, death.InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", death.InstructionHud.CreationAccent);
        TestAssert.Equal(0, death.InstructionTheme!.Events.Count);
        rig.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(rig.Current());
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Equal(death.InstructionHud.CreationRevision, rig.Current().InstructionHud!.CreationRevision);
        rig.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(rig.Current());
        rig.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(death.InstructionHud.CreationRevision, rig.Current().InstructionHud!.CreationRevision);
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Equal(initial + 2, rig.Current().InstructionHud!.CreationRevision!.Value);
        rig.NoGameWrites();
    }

    public static void DelayedAndReadyDesignRefreshSurviveUnknownWithoutSpendingEarlierCreation() {
        foreach (bool drawWhileUnknown in new[] { false, true }) {
            var rig = Palette();
            rig.Wait(250);
            rig.Send(OverlayInput.Back);
            rig.Select("Menu Preferences");
            rig.Highlight("Design");
            var initial = rig.Current();
            rig.StageChoice(OverlayInput.NextChoice);
            rig.Send(OverlayInput.Select);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
            rig.Session.Change(LocalPlayerLife.Unknown);
            Withdrawn(rig.Current());
            rig.Clock.Advance(TimeSpan.FromMilliseconds(drawWhileUnknown ? 25 : 5));
            if (drawWhileUnknown) Withdrawn(rig.Current());
            rig.Session.Change(LocalPlayerLife.Alive);
            var recovered = rig.Current();
            TestAssert.Equal(initial.InstructionHud!.CreationRevision!.Value + (drawWhileUnknown ? 1UL : 0UL),
                recovered.InstructionHud!.CreationRevision!.Value);
            TestAssert.Equal(drawWhileUnknown ? "Classic" : "Apparition", recovered.InstructionHud.CreationDesign);
            if (!drawWhileUnknown) {
                TestAssert.Equal(1, recovered.InstructionTheme!.Events.Count);
                rig.Wait(20);
            }
            TestAssert.Equal(initial.InstructionHud.CreationRevision.Value + 1, rig.Current().InstructionHud!.CreationRevision!.Value);
            TestAssert.Equal(0, rig.Current().InstructionTheme!.Events.Count);
            rig.NoGameWrites();
        }
    }

    public static void RevocationOwnerReplacementDisableAndStoppedStateCannotResurrectRetainedHistory() {
        foreach (string cause in new[] { "permission", "permission-null", "process", "replacement", "missing", "preview", "shutdown" }) {
            var rig = Palette();
            ulong initial = rig.Current().InstructionHud!.CreationRevision!.Value;
            rig.Session.Change(LocalPlayerLife.Unknown);
            Withdrawn(rig.Current());
            if (cause == "permission") rig.Session.Change(LocalPlayerLife.Unknown, hasMenu: false);
            if (cause == "permission-null") rig.Session.Change(LocalPlayerLife.Unknown, hasMenu: null);
            // Only the inert provider receipt is changed; production still performs its normal process check
            var observationField = typeof(ScopedOpeningFixture.OpeningSession).GetField("observation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            if (cause == "process") observationField.SetValue(rig.Session, rig.Session.Admission.Observation with { ProcessId = 43 });
            if (cause == "replacement") rig.Session.Change(LocalPlayerLife.Unknown, replaceOwner: true);
            if (cause == "missing") rig.Session.AdmissionUnavailable = true;
            if (cause == "preview") rig.Session.State = rig.Session.State with { Preview = true };
            if (cause == "shutdown") rig.Controller.RequestShutdown();
            var revoked = rig.Current();
            TestAssert.True(revoked.InstructionTheme is null, "Retired proof must not publish an older journal");
            if (cause == "process") observationField.SetValue(rig.Session, rig.Session.Admission.Observation with { ProcessId = 42 });
            rig.Session.AdmissionUnavailable = false;
            rig.Session.State = rig.Session.State with { Preview = false };
            rig.Session.Change(LocalPlayerLife.Alive);
            var recovered = rig.Current();
            if (cause == "shutdown") Withdrawn(recovered);
            else {
                TestAssert.Equal(initial + 1, recovered.InstructionHud!.CreationRevision!.Value);
                TestAssert.Equal(0, recovered.InstructionTheme!.Events.Count);
            }
            rig.NoGameWrites();
        }
        // Disable is an actual source destruction even if a same-owner withdrawal preceded it
        var disabled = Palette();
        disabled.Wait(250);
        disabled.Send(OverlayInput.Back);
        disabled.Select("Menu Instructions");
        ulong first = disabled.Current().InstructionHud!.CreationRevision!.Value;
        disabled.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(disabled.Current());
        disabled.Session.Change(LocalPlayerLife.Alive);
        disabled.Select("Disable");
        Withdrawn(disabled.Current());
        disabled.Wait(430);
        disabled.Send(OverlayInput.Select);
        TestAssert.Equal(first + 1, disabled.Current().InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal(0, disabled.Current().InstructionTheme!.Events.Count);
        disabled.NoGameWrites();
    }

    public static void PrunedPrefixAndClockRecoveryKeepTheirTimingIdentityAcrossWithdrawal() {
        var rig = Palette();
        rig.Wait(250);
        for (int invocation = 1; invocation < 9; invocation++) {
            rig.Select("Red");
            rig.Wait(430);
        }
        var before = rig.Current();
        TestAssert.True(!before.InstructionTheme!.PrefixComplete, "Nine callbacks must expose the bounded missing prefix");
        rig.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(rig.Current());
        rig.Wait(100);
        rig.Session.Change(LocalPlayerLife.Alive);
        var recovered = rig.Current();
        TestAssert.Equal(before.InstructionHud!.CreationRevision, recovered.InstructionHud!.CreationRevision);
        TestAssert.Equal(8, recovered.InstructionTheme!.Events.Count);
        TestAssert.True(!recovered.InstructionTheme.PrefixComplete, "Withdrawal cannot restore a pruned prefix");
        TestAssert.Equal(before.InstructionTheme.Events[0].ElapsedMilliseconds + 100, recovered.InstructionTheme.Events[0].ElapsedMilliseconds);
        rig.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(rig.Current());
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(150));
        Withdrawn(rig.Current());
        rig.Session.Change(LocalPlayerLife.Alive);
        var rollback = rig.Current();
        TestAssert.Equal(before.InstructionHud.CreationRevision, rollback.InstructionHud!.CreationRevision);
        TestAssert.True(!rollback.InstructionTheme!.PrefixComplete, "Clock recovery cannot fabricate a fresh complete creation");
        TestAssert.Equal(0, rollback.InstructionTheme.Events.Count);
        TestAssert.Equal(9UL, rig.Controller.InstructionThemeCounter);
        rig.NoGameWrites();
    }

    public static void SchemaFailureAndPendingCounterBoundaryCannotConsumeRetainedState() {
        var rig = Palette();
        rig.Wait(250);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Design");
        var before = rig.Current();
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Session.Change(LocalPlayerLife.Unknown);
        rig.Wait(50);
        rig.Session.Change(LocalPlayerLife.Alive);
        var accentField = typeof(BackendController).GetField("accent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        // A ready source refresh captures its color, so invalid creation data reaches the actual factory boundary
        accentField.SetValue(rig.Controller, "#GG0000");
        TestAssert.Throws<InvalidOperationException>(() => rig.Current());
        TestAssert.Equal(before.InstructionHud!.CreationRevision!.Value, rig.Controller.InstructionCreationCounter);
        accentField.SetValue(rig.Controller, "#FF0000");
        var retried = rig.Current();
        TestAssert.Equal(before.InstructionHud.CreationRevision.Value + 1, retried.InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("Classic", retried.InstructionHud.CreationDesign);
        TestAssert.Equal(0, retried.InstructionTheme!.Events.Count);

        var boundary = new InstructionHudBoundaryFixture(ulong.MaxValue - 2);
        boundary.Open();
        boundary.Select("Menu Customization");
        boundary.Select("Menu Preferences");
        boundary.StageChoice(OverlayInput.NextChoice);
        boundary.Send(OverlayInput.Select);
        boundary.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(boundary.Current());
        boundary.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(ulong.MaxValue - 1, boundary.Current().InstructionHud!.CreationRevision!.Value);
        boundary.Clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal(ulong.MaxValue, boundary.Current().InstructionHud!.CreationRevision!.Value);
        boundary.Session.Change(LocalPlayerLife.Unknown);
        Withdrawn(boundary.Current());
        boundary.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(ulong.MaxValue, boundary.Current().InstructionHud!.CreationRevision!.Value);
        boundary.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Throws<OverflowException>(() => boundary.Current());
        TestAssert.Equal(ulong.MaxValue, boundary.Controller.InstructionCreationCounter);
        rig.NoGameWrites();
    }
}
