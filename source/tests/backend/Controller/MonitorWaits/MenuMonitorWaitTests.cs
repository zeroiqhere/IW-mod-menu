using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Source monitor waits are observed through actual controller commands and copied wire frames
internal static class MenuMonitorWaitTests {
    private static (ulong Revision, string Kind) Wait(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        TestAssert.True(json.RootElement.TryGetProperty("menuMonitorWait", out var wait), "An admitted source branch must publish its mechanical wait");
        return (wait.GetProperty("revision").GetUInt64(), wait.GetProperty("kind").GetString()!);
    }
    private static void NoWait(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        TestAssert.True(!json.RootElement.TryGetProperty("menuMonitorWait", out _), "No mechanical wait may remain after the source boundary");
    }
    private static MenuChromeFixture Preferences() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        return rig;
    }
    public static void ChoiceWaitConsumesRepeatedCommandsAndResumesExactlyAt180Milliseconds() {
        var rig = Preferences();
        rig.Send(OverlayInput.NextChoice);
        var first = rig.Current();
        var wait = Wait(first);
        TestAssert.Equal("choice", wait.Kind);
        TestAssert.Equal<ulong>(1, wait.Revision);
        TestAssert.Equal<bool?>(false, first.LocalMenuEligibility!.ControlsDisabled);
        TestAssert.True(first.MenuPreferenceRefresh is null && first.MenuOptionCallback is null, "Choice movement does not invent callback ownership");
        string draft = first.Rows[first.SelectedIndex].Value;
        rig.Send(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Down);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(draft, rig.Current().Rows[first.SelectedIndex].Value);
        rig.Wait(179);
        TestAssert.Equal(wait, Wait(rig.Current()));
        rig.Send(OverlayInput.PreviousChoice);
        TestAssert.Equal(draft, rig.Current().Rows[first.SelectedIndex].Value);
        rig.Wait(1);
        NoWait(rig.Current());
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal(wait.Revision + 1, Wait(rig.Current()).Revision);
        TestAssert.True(draft != rig.Current().Rows[first.SelectedIndex].Value, "The held direction resumes as a new admitted source branch");
        rig.NoGameWrites();
    }
    public static void NumericChoiceWrapsWhileNonsliderAndBooleanRowsKeepTheirSourceSchedules() {
        var rig = Preferences();
        rig.Highlight("Scroll Animation Time (ms)");
        // Numeric source sliders retain their existing bound and wrap rules
        for (int i = 0; i < 13; i++) { rig.Send(OverlayInput.NextChoice); rig.Wait(180); }
        string bounded = rig.Current().Rows[rig.Current().SelectedIndex].Value;
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("choice", Wait(rig.Current()).Kind);
        TestAssert.Equal("250 ms", bounded);
        TestAssert.Equal("100 ms", rig.Current().Rows[rig.Current().SelectedIndex].Value);
        rig.Wait(180);
        rig.Highlight("Quick Exit");
        string unchanged = rig.Current().Rows[rig.Current().SelectedIndex].Value;
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("choiceNoop", Wait(rig.Current()).Kind);
        TestAssert.Equal(unchanged, rig.Current().Rows[rig.Current().SelectedIndex].Value);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "An unchanged choice branch is not a bool callback");
        rig.Wait(50);
        NoWait(rig.Current());
        rig.Send(OverlayInput.Select);
        NoWait(rig.Current());
        var started = rig.Current();
        TestAssert.True(started.MenuPreferenceRefresh is null, "Typed preference Select has one callback clock");
        TestAssert.Equal("boolean", started.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", started.MenuOptionCallback.Phase);
        var receipt = started.MenuUseContinuation;
        TestAssert.True(receipt is not null, "The Boolean caller owns the actual Select invocation");
        rig.Wait(179);
        TestAssert.Equal(unchanged, rig.Current().Rows[rig.Current().SelectedIndex].Value);
        rig.Wait(1);
        TestAssert.Equal("cooldown", rig.Current().MenuOptionCallback!.Phase);
        rig.Wait(249);
        TestAssert.Equal("cooldown", rig.Current().MenuOptionCallback!.Phase);
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuOptionCallback is null, "The Boolean caller retains its exact430ms schedule");
        TestAssert.Equal(receipt, rig.Current().MenuUseContinuation);
        rig.NoGameWrites();
    }
    public static void BackWaitSurvivesItsOwnParentAndRootCloseAndBlocksNewOpening() {
        var rig = Preferences();
        rig.Send(OverlayInput.Back);
        var parent = rig.Current();
        TestAssert.Equal("back", Wait(parent).Kind);
        rig.Send(OverlayInput.Back);
        TestAssert.Equal(parent.NavigationRevision, rig.Current().NavigationRevision);
        rig.Wait(250);
        rig.Send(OverlayInput.Back);
        rig.Wait(250);
        rig.Send(OverlayInput.Back);
        var closed = rig.Current();
        TestAssert.True(!closed.IsOpen, "Root Back closes the source body");
        TestAssert.Equal("back", Wait(closed).Kind);
        var publication = new Apparition.Backend.Runtime.MenuPhasePublication(_ => { }, rig.Clock);
        publication.Publish(closed, true);
        TestAssert.True(!publication.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Closed-root wait has the same published owner identity");
        rig.Open();
        TestAssert.True(!rig.Current().IsOpen, "Closed-root wait also rejects the scoped opening branch");
        rig.Wait(249);
        TestAssert.Equal("back", Wait(rig.Current()).Kind);
        rig.Wait(1);
        NoWait(rig.Current());
        TestAssert.True(publication.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Final Back expiry publishes without waiting for another input");
        rig.Open();
        TestAssert.True(rig.Current().IsOpen, "A fresh opening is admitted after the final monitor wait");
        rig.NoGameWrites();
    }
    public static void DeadQuickJumpAndQuickExitUseTheSame250MillisecondBackWait() {
        var rig = new MenuChromeFixture();
        rig.Session.Change(LocalPlayerLife.Dead);
        rig.Open(OverlayInput.OpenQuick);
        rig.Wait(550);
        var frame = rig.Current();
        rig.Controller.Consume(new(100, 1, 42, OverlayInput.JumpBack, true, frame.NavigationRevision,
            null, rig.Session.Admission.Revision), () => true);
        TestAssert.True(!rig.Current().IsOpen, "Dead Jump exits the actual Quick root");
        TestAssert.Equal("back", Wait(rig.Current()).Kind);
        rig.Wait(250);
        NoWait(rig.Current());
        var main = Preferences();
        main.Highlight("Quick Exit");
        main.Send(OverlayInput.Select);
        main.Wait(430);
        main.Send(OverlayInput.Close);
        TestAssert.True(!main.Current().IsOpen, "The admitted hold-close remains local navigation");
        TestAssert.Equal("back", Wait(main.Current()).Kind);
        main.NoGameWrites();
    }
    public static void WaitOwnerReplacementShutdownAndClockRollbackCannotReplayAnOldBranch() {
        var rig = Preferences();
        rig.Send(OverlayInput.NextChoice);
        var original = Wait(rig.Current());
        rig.Wait(100);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(90));
        TestAssert.Equal(original, Wait(rig.Current()));
        rig.Wait(179);
        TestAssert.Equal(original, Wait(rig.Current()));
        rig.Wait(1);
        NoWait(rig.Current());
        rig.Send(OverlayInput.NextChoice);
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        NoWait(rig.Current());
        var shutdown = Preferences();
        shutdown.Send(OverlayInput.NextChoice);
        Wait(shutdown.Current());
        shutdown.Controller.RequestShutdown();
        NoWait(shutdown.Current());
        shutdown.NoGameWrites();
    }
    public static void ActorlessLegacyChoiceAndBackKeepTheirExistingImmediateBehavior() {
        var legacy = new MenuChromeFixture();
        legacy.Session.AdmissionUnavailable = true;
        legacy.Send(OverlayInput.Toggle);
        legacy.Wait(50);
        legacy.Wait(700);
        legacy.Preferences();
        legacy.Highlight("Design");
        legacy.Send(OverlayInput.NextChoice);
        NoWait(legacy.Current());
        legacy.Send(OverlayInput.Back);
        NoWait(legacy.Current());
        legacy.NoGameWrites();
    }
    public static void FailedClockAndCounterPreflightsLeaveDraftAndParentUnchanged() {
        var clock = new MonitorWaitFaultClock();
        var rig = new MenuChromeFixture(clock: clock);
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        var before = rig.Current();
        clock.FailTimestamp = true;
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.NextChoice));
        TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Back));
        clock.FailTimestamp = false;
        var after = rig.Current();
        TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
        TestAssert.Equal(before.Context, after.Context);
        TestAssert.Equal(before.Rows[before.SelectedIndex].Value, after.Rows[after.SelectedIndex].Value);
        NoWait(after);
        // Counter exhaustion is tested before either a draft or a navigation path can change
        var counter = typeof(Apparition.Backend.Controller.BackendController).GetField("monitorWaitRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        counter.SetValue(rig.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.NextChoice));
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Back));
        TestAssert.Equal(before.NavigationRevision, rig.Current().NavigationRevision);
        TestAssert.Equal(before.Rows[before.SelectedIndex].Value, rig.Current().Rows[before.SelectedIndex].Value);
        NoWait(rig.Current());
        rig.NoGameWrites();
        // Saturated elapsed conversion restarts only the finite mechanical wait
        var overflow = new MonitorWaitOverflowClock();
        var sleeping = new Apparition.Backend.Controller.LocalMenuMonitorWaitState(new(9, "back"), new(), 1, 0, 0);
        var recovered = sleeping.Read(overflow)!;
        TestAssert.Equal<ulong>(9, recovered.Frame.Revision);
        TestAssert.Equal(long.MaxValue, recovered.StartedTimestamp);
        TestAssert.Equal(recovered, recovered.Read(overflow));
    }

    public static void WireCopiesRejectMalformedConflictsAndFocusProjectionWithdrawsOnlyThePublishedWait() {
        var rig = Preferences();
        rig.Send(OverlayInput.NextChoice);
        var frame = rig.Current();
        foreach (var invalid in new[] { new MenuMonitorWait(0, "choice"), new MenuMonitorWait(1, "Choice"), new MenuMonitorWait(1, "plain") }) {
            TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuMonitorWait = invalid }).Serialize());
        }
        TestAssert.Throws<InvalidOperationException>(() => (frame with { IsOpen = false }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { Rows = Array.Empty<StateRow>(), SelectedIndex = 0 }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { Rows = new[] { new StateRow("", "", false, true) }, SelectedIndex = 0 }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { SelectedIndex = frame.Rows.Count }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { LocalMenuEligibility = null }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { LocalMenuEligibility = frame.LocalMenuEligibility! with { Life = "unknown" } }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { LocalMenuEligibility = frame.LocalMenuEligibility! with { HasMenu = false } }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuOptionCallback = new(1, "plain", "cooldown") }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuUnlock = "pause" }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuPreferenceRefresh = new("pending") }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuDesignTransition = new("cooldown") }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { WidthEditor = new() }).Serialize());
        (frame with { MenuMonitorWait = new(ulong.MaxValue, "choice") }).Serialize();
        NoWait(frame with { MenuMonitorWait = null });
        var projected = StateFocusProjection.Apply(frame, false);
        NoWait(projected);
        projected.Serialize();
        TestAssert.Equal(Wait(frame), Wait(rig.Current()));
        rig.Wait(180);
        NoWait(rig.Current());
    }

    public static void OpeningSelectorsWaitAndIndependentNavigationRetiresTheOldInvocation() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Open Controls");
        var frame = rig.Current();
        rig.Controller.Consume(new(100, 1, 42, OverlayInput.NextChoice, true, frame.NavigationRevision,
            frame.OpeningPolicies!.Revision), () => true);
        TestAssert.Equal("choice", Wait(rig.Current()).Kind);
        var held = rig.Current();
        rig.Controller.Consume(new(101, 1, 42, OverlayInput.Select, true, held.NavigationRevision,
            held.OpeningPolicies!.Revision), () => true);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Use cannot apply the staged selector during the mechanical wait");
        // Direct owner navigation models a separate admitted lifecycle event, not a sleeping branch replay
        var navigation = (OverlayNavigation)typeof(Apparition.Backend.Controller.BackendController).GetField("navigation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(rig.Controller)!;
        navigation.OpenPage(MenuPage.Preferences);
        NoWait(rig.Current());
        rig.NoGameWrites();
    }
}
