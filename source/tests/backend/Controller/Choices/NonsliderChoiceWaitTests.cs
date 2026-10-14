using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The outer source choice branch waits even when its inner slider adjustment does nothing
internal static class NonsliderChoiceWaitTests {
    private static void Waiting(StateDocument frame) {
        TestAssert.True(frame.MenuMonitorWait is { Kind: "choiceNoop" }, "A verified ordinary nonslider choice must own its 50ms caller wait");
        TestAssert.True(frame.MenuUseContinuation is null && frame.MenuOptionCallback is null,
            "A new choice invocation cannot borrow an older ready Select receipt");
    }
    public static void NonsliderPaletteChoiceAfterBooleanExpiryWaitsWithoutChangingItsSourceRow() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors); rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuUseContinuation is not null, "The actual BOOL predecessor must expire normally");
        rig.Send(OverlayInput.NextChoice); var waiting = rig.Controller.Snapshot(); Waiting(waiting);
        TestAssert.True(ready.Rows.SequenceEqual(waiting.Rows), "A nonslider branch cannot change row values, actions or capability");
        TestAssert.Equal(ready.Accent, waiting.Accent); TestAssert.Equal(ready.NavigationRevision, waiting.NavigationRevision);
        TestAssert.Equal(ready.MenuRevision, waiting.MenuRevision); TestAssert.Equal<bool?>(false, waiting.LocalMenuEligibility!.ControlsDisabled);
        rig.Send(OverlayInput.Down); rig.Send(OverlayInput.Select); TestAssert.Equal(ready.NavigationRevision, rig.Controller.Snapshot().NavigationRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); Waiting(rig.Controller.Snapshot());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(rig.Controller.Snapshot().MenuMonitorWait is null,
            "The no-op caller resumes exactly 50ms after admission");
        rig.Send(OverlayInput.PreviousChoice); Waiting(rig.Controller.Snapshot()); TestAssert.Equal(0, rig.Session.Executed.Count);
        var active = rig.Controller.Snapshot();
        // The50ms branch shares strict source ownership and wire guards, not slider timing
        foreach (var malformed in new[] { active with { IsOpen = false },
            active with { Rows = Array.Empty<StateRow>(), SelectedIndex = 0 },
            active with { Rows = new[] { new StateRow("", "", false, true) }, SelectedIndex = 0 },
            active with { MenuMonitorWait = new(0, "choiceNoop") },
            active with { MenuMonitorWait = new(1, "ChoiceNoop") },
            active with { MenuOptionCallback = new(1, "plain", "cooldown") } })
            TestAssert.Throws<InvalidOperationException>(() => malformed.Serialize());
        (active with { MenuMonitorWait = new(ulong.MaxValue, "choiceNoop") }).Serialize();
        TestAssert.True(StateFocusProjection.Apply(active, false).MenuMonitorWait is null,
            "Unfocused publication does not expose an admitted source timer");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        TestAssert.True(rig.Controller.Snapshot().MenuMonitorWait is null, "A replacement owner cannot inherit the original no-op branch");
    }
    public static void RootAndQuickExitChoicesKeepTheirValuesAndOwnTheOuterWait() {
        foreach (MenuPage page in new[] { MenuPage.Main, MenuPage.Preferences }) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(page);
            LocalOptionCallbackControllerTests.SelectIndex(rig, page == MenuPage.Preferences ? 4 : 1);
            var before = rig.Controller.Snapshot(); rig.Send(OverlayInput.PreviousChoice); var waiting = rig.Controller.Snapshot(); Waiting(waiting);
            TestAssert.True(before.Rows.SequenceEqual(waiting.Rows), "Root and source BOOL rows have no supported slider adjustment");
            TestAssert.Equal(before.Appearance, waiting.Appearance); TestAssert.Equal(before.NavigationRevision, waiting.NavigationRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); TestAssert.True(rig.Controller.Snapshot().MenuMonitorWait is null,
                "An unchanged branch still ends after its finite source pause");
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }
    public static void InvalidNonsliderVisitsAndFailedClockOrCounterCannotStartAWait() {
        foreach (string condition in new[] { "preview", "unknown", "focus", "busy", "stale-admission", "stale-navigation", "closed", "editor" }) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors);
            if (condition == "preview") rig.Session.State = rig.Session.State with { Preview = true };
            if (condition == "unknown") rig.Session.Change(LocalPlayerLife.Unknown);
            if (condition == "focus") rig.Focus = false;
            if (condition == "busy") rig.Session.State = rig.Session.State with { Busy = true };
            if (condition == "closed") rig.Navigation.Close();
            if (condition == "editor") { rig.Navigation.OpenPage(MenuPage.WidthEditor); rig.Controller.Snapshot(); }
            var before = rig.Controller.Snapshot();
            rig.Controller.Consume(new(100, 1, 42, OverlayInput.NextChoice, true,
                condition == "stale-navigation" ? before.NavigationRevision + 1 : null,
                LocalAdmissionRevision: condition == "stale-admission" ? rig.Session.Admission.Revision + 1 : null), () => rig.Focus);
            var after = rig.Controller.Snapshot(); TestAssert.True(after.MenuMonitorWait is null, "Rejected source context cannot own a pause: " + condition);
            TestAssert.Equal(before.Accent, after.Accent); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
        var clock = new MonitorWaitFaultClock(); var fault = new MenuChromeFixture(clock: clock); fault.SettleMain(); fault.Highlight("Basic Scripts"); var unchanged = fault.Current();
        clock.FailTimestamp = true; TestAssert.Throws<IOException>(() => fault.Send(OverlayInput.NextChoice)); clock.FailTimestamp = false;
        TestAssert.Equal(unchanged.NavigationRevision, fault.Current().NavigationRevision); TestAssert.True(fault.Current().MenuMonitorWait is null,
            "Failed clock preflight cannot publish a no-op invocation");
        var counter = new MenuChromeFixture(); counter.SettleMain(); counter.Highlight("Basic Scripts"); var original = counter.Current();
        typeof(Apparition.Backend.Controller.BackendController).GetField("monitorWaitRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(counter.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => counter.Send(OverlayInput.NextChoice));
        TestAssert.Equal(original.NavigationRevision, counter.Current().NavigationRevision); TestAssert.True(counter.Current().MenuMonitorWait is null,
            "Counter exhaustion cannot change navigation or publish a wait");
        var rollback = new MenuChromeFixture(); rollback.SettleMain(); rollback.Highlight("Basic Scripts"); rollback.Send(OverlayInput.NextChoice);
        var first = rollback.Current().MenuMonitorWait; rollback.Wait(20);
        rollback.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.Equal(first, rollback.Current().MenuMonitorWait);
        rollback.Wait(49); TestAssert.Equal(first, rollback.Current().MenuMonitorWait);
        rollback.Wait(1); TestAssert.True(rollback.Current().MenuMonitorWait is null, "Rollback restarts only a finite50ms wait");
        rollback.Send(OverlayInput.NextChoice); rollback.Controller.RequestShutdown();
        TestAssert.True(rollback.Current().MenuMonitorWait is null, "Shutdown retires the no-op branch without replaying input");
    }
}
