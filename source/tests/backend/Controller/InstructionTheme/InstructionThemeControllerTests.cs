using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Actual fixed callbacks expose invocation facts, without borrowing scalar accent changes as requests
internal static class InstructionThemeControllerTests {
    private static InstructionThemeState Theme(StateDocument frame) {
        TestAssert.True(frame.InstructionTheme is not null, "A paired owned footer must declare its authoritative theme window");
        return frame.InstructionTheme!;
    }
    private static MenuChromeFixture Palette() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        return rig;
    }

    public static void AllFixedSelectionsIssueANewCapturedCurrentDesignRequestIncludingSameColorRainbowStop() {
        var rig = Palette();
        TestAssert.Equal(0, Theme(rig.Current()).Events.Count);
        rig.Select("Red");
        var first = Theme(rig.Current()).Events.Single();
        TestAssert.Equal(1UL, first.Revision);
        TestAssert.Equal("outline", first.Target);
        TestAssert.Equal("#FF0000", first.Accent);
        TestAssert.True(!first.StoppedRainbow, "The first fixed request must capture its actual prior mode");
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        var second = Theme(rig.Current()).Events.Last();
        TestAssert.Equal(2UL, second.Revision);
        TestAssert.Equal<int?>(430, second.PredecessorGapMilliseconds);
        rig.Wait(430);
        rig.Select("Smooth Rainbow");
        rig.Wait(430);
        rig.Select("Red");
        var stop = Theme(rig.Current()).Events.Last();
        TestAssert.Equal(3UL, stop.Revision);
        TestAssert.True(stop.StoppedRainbow, "Same-color selection must still emit the accepted Rainbow stop request");
        TestAssert.True(!rig.Current().SmoothRainbowTheme, "The fixed action must stop actual mode after recording its prior state");
        rig.NoGameWrites();
    }

    public static void FooterRequestsSurviveRowPageCloseAndFocusButRetireWithCreationOrOwner() {
        var rig = Palette();
        rig.Select("Red");
        ulong parent = Theme(rig.Current()).CreationRevision;
        string rejectedDirectory = Path.Combine(Path.GetTempPath(), "apparition-theme-publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rejectedDirectory, "state.json"));
        try {
            // Failed filesystem publication must leave the accepted callback available for the next report
            TestAssert.Throws<SessionFileException>(() => AtomicFilePublisher.Write(rejectedDirectory, "state.json", rig.Current().Serialize()));
            TestAssert.Equal(1UL, Theme(rig.Current()).Events.Single().Revision);
        } finally {
            Directory.Delete(rejectedDirectory, recursive: true);
        }
        rig.Wait(180);
        TestAssert.Equal(180UL, Theme(rig.Current()).Events.Single().ElapsedMilliseconds);
        rig.Wait(250);
        rig.Send(OverlayInput.Back);
        rig.Send(OverlayInput.Toggle);
        TestAssert.Equal(parent, Theme(rig.Current()).CreationRevision);
        TestAssert.Equal(1UL, Theme(rig.Current()).Events.Single().Revision);
        TestAssert.True(StateFocusProjection.Apply(rig.Current(), false).InstructionTheme is null,
            "Focus withdrawal cannot carry active descriptor authority");
        rig.Open();
        rig.Wait(50);
        rig.Wait(550);
        TestAssert.Equal(parent, Theme(rig.Current()).CreationRevision);
        rig.Select("Menu Instructions");
        rig.Select("Alternate Style");
        TestAssert.Equal(0, Theme(rig.Current()).Events.Count);
        TestAssert.True(Theme(rig.Current()).CreationRevision > parent, "True source recreation retires previous requests");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        TestAssert.Equal(0, Theme(rig.Current()).Events.Count);
        rig.NoGameWrites();
    }

    public static void MissingOwnerAndBackgroundRectanglesKeepTheAuthoritativeWindowEmptyOrOmitted() {
        var rig = Palette();
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Disable Background");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Red");
        TestAssert.Equal(0, Theme(rig.Current()).Events.Count);
        TestAssert.Equal(0UL, rig.Controller.InstructionThemeCounter);
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        TestAssert.True(rig.Current().InstructionTheme is null, "A withdrawn owner cannot publish theme authority");
        var preview = new Apparition.Backend.Controller.BackendController(42, new SessionBoundary());
        TestAssert.True(preview.Snapshot().InstructionTheme is null, "Legacy preview retains omission");
        rig.NoGameWrites();
    }

    public static void RepeatedOverlapsRetainUnclampedAgesAndBoundedIncompleteHistory() {
        var rig = Palette();
        for (int request = 0; request < 9; request++) {
            rig.Select("Red");
            if (request < 8) rig.Wait(430);
        }
        var window = Theme(rig.Current());
        TestAssert.Equal(8, window.Events.Count);
        TestAssert.True(!window.PrefixComplete, "Bounded pruning must disclose the missing invocation prefix");
        TestAssert.Equal(2UL, window.Events[0].Revision);
        TestAssert.Equal(3010UL, window.Events[0].ElapsedMilliseconds);
        TestAssert.Equal(9UL, window.Events[^1].Revision);
        foreach (var request in window.Events) TestAssert.Equal<int?>(430, request.PredecessorGapMilliseconds);
        rig.Wait(1000);
        TestAssert.True(!Theme(rig.Current()).PrefixComplete, "A quiet endpoint cannot invent the lost prefix");
        TestAssert.Equal(4010UL, Theme(rig.Current()).Events[0].ElapsedMilliseconds);
        rig.NoGameWrites();
    }

    public static void ActionTimeTargetCannotFollowALaterCommittedDesignDuringItsRebuildGap() {
        var rig = Palette();
        rig.Select("Red");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Design");
        for (int choice = 0; choice < 3; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal("AIO", rig.Current().MenuDesignTransition!.FooterDesign);
        TestAssert.Equal("outline", Theme(rig.Current()).Events.Single().Target);
        rig.Wait(49);
        TestAssert.Equal("outline", Theme(rig.Current()).Events.Single().Target);
        rig.Wait(1);
        TestAssert.Equal(0, Theme(rig.Current()).Events.Count);
        rig.Wait(200);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Green");
        TestAssert.Equal("background", Theme(rig.Current()).Events.Single().Target);
        rig.NoGameWrites();
    }

    public static void InvocationClockAndCounterFailuresCannotMutateActualPaletteModeOrWait() {
        var failureClock = new FailingClock();
        var rig = new MenuChromeFixture(clock: failureClock);
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Highlight("Red");
        var before = rig.Current();
        failureClock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.Select));
        failureClock.Fail = false;
        TestAssert.Equal(before.Accent, rig.Current().Accent);
        TestAssert.Equal(0UL, rig.Controller.InstructionThemeCounter);

        var exhausted = new InstructionHudBoundaryFixture(0, themeCounter: ulong.MaxValue);
        exhausted.Open();
        exhausted.Select("Menu Customization");
        exhausted.Select("Main Design Color");
        var initial = exhausted.Current();
        TestAssert.Throws<OverflowException>(() => exhausted.Send(OverlayInput.Select));
        var after = exhausted.Current();
        TestAssert.Equal(initial.Accent, after.Accent);
        TestAssert.Equal(initial.SmoothRainbowTheme, after.SmoothRainbowTheme);
        TestAssert.True(after.MenuOptionCallback is null, "Failed invocation admission must not arm the BOOL wait");
        TestAssert.Equal(ulong.MaxValue, exhausted.Controller.InstructionThemeCounter);
    }

    private sealed class FailingClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new();
        public bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock failure") : Time.GetTimestamp();
    }
}
