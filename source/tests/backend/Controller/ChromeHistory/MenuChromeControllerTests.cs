using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuChromeControllerTests {
    public static void MainCreationWaitsForActualRowsAndLateFirstDrawCommitsOnlyOneChrome() {
        foreach (int firstDraw in new[] { 50, 1000 }) {
            var rig = new MenuChromeFixture();
            rig.Open();
            TestAssert.True(rig.Current().MenuChromeRevision is null, "The hidden initial panel has no created chrome");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(firstDraw));
            TestAssert.Equal("open:ready", rig.Controller.ReadMenuPublicationPhase());
            var frame = rig.Current();
            TestAssert.Equal<ulong?>(1, frame.MenuChromeRevision);
            TestAssert.Equal(frame.MenuChromeRevision, rig.Current().MenuChromeRevision);
            rig.Wait(550);
            TestAssert.Equal(frame.MenuChromeRevision, rig.Current().MenuChromeRevision);
            rig.NoGameWrites();
        }
    }

    public static void QuickCreatesImmediatelyAndMissedCloseCannotReuseMainOrQuickChrome() {
        var rig = new MenuChromeFixture();
        rig.Open(OverlayInput.OpenQuick);
        TestAssert.Equal<ulong?>(1, rig.Current().MenuChromeRevision);
        rig.Wait(550);
        rig.Send(OverlayInput.Back);
        var closed = rig.Current();
        TestAssert.True(!closed.IsOpen, "The Quick root must close before switching families");
        TestAssert.Equal<ulong?>(1, closed.MenuChromeRevision);
        rig.Wait(250);

        rig.Open();
        rig.Wait(50);
        TestAssert.Equal<ulong?>(2, rig.Current().MenuChromeRevision);
        rig.Wait(550);
        rig.Send(OverlayInput.Toggle);
        rig.Open(OverlayInput.OpenQuick);
        TestAssert.Equal<ulong?>(3, rig.Current().MenuChromeRevision);
        rig.NoGameWrites();
    }

    public static void FixedAndRainbowCallbacksRedrawRowsWithoutRecreatingChrome() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        ulong? chrome = rig.Current().MenuChromeRevision;
        TestAssert.True(chrome > 0, "An actual source visit must publish chrome identity");
        foreach (string label in new[] { "Dodger Blue", "Smooth Rainbow", "Smooth Rainbow", "Red" }) {
            rig.Select(label);
            var before = rig.Current();
            rig.Wait(180);
            var redraw = rig.Current();
            TestAssert.Equal(before.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
            TestAssert.Equal(chrome, redraw.MenuChromeRevision);
            rig.Wait(250);
        }
        rig.Send(OverlayInput.Back);
        TestAssert.Equal(chrome, rig.Current().MenuChromeRevision);
        rig.NoGameWrites();
    }

    public static void ChangedDesignRecreatesAtActualRedrawButSameDesignKeepsChrome() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        ulong? first = rig.Current().MenuChromeRevision;
        rig.Select("Design");
        rig.Wait(250);
        TestAssert.Equal(first, rig.Current().MenuChromeRevision);
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(first, rig.Current().MenuChromeRevision);
        rig.Wait(49);
        TestAssert.Equal(first, rig.Current().MenuChromeRevision);
        rig.Wait(1);
        TestAssert.Equal(first!.Value + 1, rig.Current().MenuChromeRevision!.Value);
        rig.Wait(200);
        TestAssert.Equal(first.Value + 1, rig.Current().MenuChromeRevision!.Value);
        rig.NoGameWrites();
    }

    public static void CounterAndConditionalStealthRebuildOnlyAtTheirActualCreation() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        ulong first = rig.Current().MenuChromeRevision ?? throw new InvalidOperationException("Missing source chrome");
        rig.Select("Option Counter");
        rig.Wait(50);
        TestAssert.Equal(first + 1, rig.Current().MenuChromeRevision!.Value);
        rig.Wait(200);
        rig.Select("Stealth UI");
        rig.Wait(50);
        TestAssert.Equal(first + 2, rig.Current().MenuChromeRevision!.Value);
        rig.Wait(200);
        rig.Select("Stealth UI");
        rig.Wait(430);
        TestAssert.Equal(first + 2, rig.Current().MenuChromeRevision!.Value);
        rig.NoGameWrites();
    }

    public static void SoftLockRetainsChromeAndLateSoftUnlockCreatesOnce() {
        foreach (int redraw in new[] { 50, 150, 1000 }) {
            var rig = new MenuChromeFixture();
            rig.SettleMain();
            rig.Select("Menu Customization");
            ulong first = rig.Current().MenuChromeRevision ?? throw new InvalidOperationException("Missing source chrome");
            rig.Select("Width Editor");
            TestAssert.Equal(first, rig.Current().MenuChromeRevision!.Value);
            rig.Send(OverlayInput.FinishWidthEditor);
            TestAssert.Equal(first, rig.Current().MenuChromeRevision!.Value);
            rig.Wait(redraw);
            var recreated = rig.Current();
            TestAssert.Equal(first + 1, recreated.MenuChromeRevision!.Value);
            if (redraw >= 150) TestAssert.Equal<bool?>(false, recreated.LocalMenuEligibility!.ControlsDisabled);
            rig.Wait(150);
            TestAssert.Equal(first + 1, rig.Current().MenuChromeRevision!.Value);
            rig.NoGameWrites();
        }
        // A legacy preview has no actor-owned ID but still completes its actual parent return
        var legacy = new MenuCreditsTests.Rig();
        legacy.Enter();
        legacy.Send(OverlayInput.FinishMenuCredits);
        legacy.SetupClock.Advance(TimeSpan.FromMilliseconds(150));
        TestAssert.True(legacy.Controller.ReadMenuUnlockPhase() is null, "An expired legacy wait is input-ready");
        var unlocked = legacy.Current();
        TestAssert.True(unlocked.MenuUnlock is null && unlocked.MenuChromeRevision is null,
            "The actual legacy redraw terminates without waiting for an unavailable owner ID");
        legacy.Send(OverlayInput.Select);
        TestAssert.True(legacy.Current().MenuCredits is not null, "A fresh legacy input works after the return");
        legacy.NoGameWork();
    }

    public static void UnlockRollbackCannotRecreateCommittedChromeOrConsumeUnobservedReadiness() {
        foreach (int elapsed in new[] { 50, 150, 1000 }) {
            foreach (bool drawn in new[] { false, true }) {
                var rig = new MenuChromeFixture();
                rig.SettleMain();
                rig.Select("Menu Customization");
                rig.Select("Width Editor");
                rig.Send(OverlayInput.FinishWidthEditor);
                ulong first = rig.Controller.CommittedChromeRevision;
                rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
                if (drawn) rig.Current();
                else TestAssert.Equal(elapsed < 150 ? "pause" : null, rig.Controller.ReadMenuUnlockPhase());
                rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(elapsed - 1));
                TestAssert.Equal(drawn && elapsed >= 150 ? null : "rebuild", rig.Current().MenuUnlock);
                TestAssert.Equal(first + (drawn ? 1UL : 0UL), rig.Controller.CommittedChromeRevision);
                rig.Wait(50);
                TestAssert.Equal(first + 1, rig.Controller.CommittedChromeRevision);
                rig.Wait(150);
                TestAssert.Equal(first + 1, rig.Controller.CommittedChromeRevision);
            }
        }
    }

    public static void ExhaustedChromeCountersRejectOpenDesignAndEditorFinishBeforeMutation() {
        var cannotOpen = new MenuChromeFixture(ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => cannotOpen.Open());
        TestAssert.True(!cannotOpen.Current().IsOpen, "A failed opening reservation cannot change the logical family");

        var cannotDesign = new MenuChromeFixture(ulong.MaxValue - 1);
        cannotDesign.SettleMain();
        cannotDesign.Preferences();
        cannotDesign.Highlight("Design");
        cannotDesign.StageChoice(OverlayInput.NextChoice);
        var before = cannotDesign.Current();
        TestAssert.Throws<OverflowException>(() => cannotDesign.Send(OverlayInput.Select));
        TestAssert.Equal(before.Appearance, cannotDesign.Current().Appearance);
        TestAssert.Equal(before.NavigationRevision, cannotDesign.Current().NavigationRevision);
        TestAssert.True(cannotDesign.Current().MenuDesignTransition is null, "Failed chrome admission cannot arm a design rebuild");

        var cannotFinish = new MenuChromeFixture(ulong.MaxValue - 1);
        cannotFinish.SettleMain();
        cannotFinish.Select("Menu Customization");
        cannotFinish.Select("Menu Credits");
        var credits = cannotFinish.Current();
        TestAssert.Throws<OverflowException>(() => cannotFinish.Send(OverlayInput.FinishMenuCredits));
        TestAssert.Equal(credits.NavigationRevision, cannotFinish.Current().NavigationRevision);
        TestAssert.True(cannotFinish.Current().MenuCredits is not null && cannotFinish.Current().MenuUnlock is null,
            "Failed finish reservation must keep the credits visit and no latent unlock");
    }

    public static void FailedFreshRowCommitCannotPublishOrConsumeTheReservedChromeInstance() {
        var navigation = new OverlayNavigation(0, ulong.MaxValue - 3);
        navigation.OpenPage(MenuPage.Players);
        var rig = new MenuChromeFixture(navigation: navigation);
        rig.Open();
        rig.Session.State = rig.Session.State with { Players = new[] { new OverlayPlayer(1, "Renamed friend") } };
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Throws<OverflowException>(() => rig.Current());
        TestAssert.Equal(0UL, rig.Controller.CommittedChromeRevision);
        TestAssert.Equal("open:ready", rig.Controller.ReadMenuPublicationPhase());
    }

    public static void ActorAndPermissionReplacementCannotAdoptPreviousSourceChrome() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        ulong first = rig.Controller.CommittedChromeRevision;
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive, replaceOwner: true);
        TestAssert.True(!rig.Current().IsOpen, "Replacing the source actor must retire the old visit");
        rig.Open();
        rig.Wait(50);
        TestAssert.Equal(first + 1, rig.Controller.CommittedChromeRevision);
        rig.Wait(550);
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive, hasMenu: false);
        TestAssert.True(!rig.Current().IsOpen, "Permission loss must retire the source visit");
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive);
        rig.Open(OverlayInput.OpenQuick);
        rig.Current();
        TestAssert.Equal(first + 2, rig.Controller.CommittedChromeRevision);
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Dead);
        TestAssert.True(rig.Current().IsOpen, "A source Quick visit remains open across life changes");
        TestAssert.Equal(first + 2, rig.Controller.CommittedChromeRevision);
        rig.NoGameWrites();
    }

    public static void FailedUnlockClockCannotLeaveTheEditorOrReserveFutureChrome() {
        var clock = new FailingClock();
        var rig = new MenuChromeFixture(clock: clock);
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Width Editor");
        var before = rig.Current();

        // No active timeline samples time before the editor finish admission
        clock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.FinishWidthEditor));
        clock.Fail = false;
        var after = rig.Current();
        TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
        TestAssert.Equal(before.MenuChromeRevision, after.MenuChromeRevision);
        TestAssert.True(after.WidthEditor is not null && after.MenuUnlock is null,
            "A failed start sample must retain the editor without a pending recreation");
        rig.NoGameWrites();
    }

    private sealed class FailingClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new();
        public bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail
            ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }

    public static void FocusProjectionClosedStateAndLegacyPreviewRetainTheirSeparateContracts() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        var frame = rig.Current();
        TestAssert.True(frame.MenuChromeRevision > 0, "The actual source panel must publish its identity");
        TestAssert.Equal(frame.MenuChromeRevision, StateFocusProjection.Apply(frame, false).MenuChromeRevision);
        TestAssert.Equal(frame.MenuChromeRevision, rig.Controller.ClosedState().MenuChromeRevision);
        var preview = new Apparition.Backend.Controller.BackendController(42, new SessionBoundary());
        using var json = JsonDocument.Parse(preview.Snapshot().Serialize());
        TestAssert.True(!json.RootElement.TryGetProperty("menuChromeRevision", out _), "Legacy inert preview omits creation identity");
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuChromeRevision = 0 }).Serialize());
        using var maximum = JsonDocument.Parse((frame with { MenuChromeRevision = ulong.MaxValue, ChromeTheme = null }).Serialize());
        TestAssert.Equal(ulong.MaxValue, maximum.RootElement.GetProperty("menuChromeRevision").GetUInt64());
        using var legacy = JsonDocument.Parse((frame with { MenuChromeRevision = null, ChromeTheme = null }).Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("menuChromeRevision", out _), "A null producer property is omitted for legacy readers");
        rig.NoGameWrites();
    }
}
