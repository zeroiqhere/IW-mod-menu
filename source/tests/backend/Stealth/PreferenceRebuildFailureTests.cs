using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class PreferenceRebuildFailureTests {
    public static void ClockAndEitherCounterExhaustionRejectBeforeChangingPreferencesOrStartingTimers() {
        var clock = new ThrowingClock();
        var rig = new MenuDesignTests.Rig(clock: clock);
        rig.Open();
        rig.Highlight("Option Counter");
        var before = rig.Current();
        clock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.Select));
        clock.Fail = false;
        TestAssert.Equal(before.Appearance, rig.Current().Appearance);
        TestAssert.True(rig.Current().MenuPreferenceRebuild is null, "A failed clock cannot admit a timer");
        rig.NoGameWork();
        foreach (bool openingBoundary in new[] { false, true }) {
            var session = new SessionBoundary();
            var navigation = new OverlayNavigation(0, openingBoundary ? 0 : ulong.MaxValue - 3);
            navigation.Open();
            navigation.OpenPage(MenuPage.Preferences);
            navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.Preferences, session.State).Rows);
            navigation.SelectIndex(9);
            var controller = new BackendController(42, session, navigation, false,
                openingRevision: openingBoundary ? ulong.MaxValue : 0);
            var original = controller.Snapshot();
            TestAssert.Throws<OverflowException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Select, true), () => true));
            var retained = controller.Snapshot();
            TestAssert.Equal(original.Appearance, retained.Appearance);
            TestAssert.Equal(original.MenuRevision, retained.MenuRevision);
            TestAssert.True(retained.MenuPreferenceRebuild is null && retained.MenuOpening is null, "Counter exhaustion cannot leave a partial admission");
            controller.StartQueuedAction();
            TestAssert.Equal(0, session.Executed.Count);
        }
    }

    public static void ForeignInvalidStaleAndBothFocusFailuresCannotCommitStealthOrReplayARebuild() {
        foreach (string label in new[] { "Option Counter", "Stealth UI" }) {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Highlight(label);
            var before = rig.Current();
            rig.Send(OverlayInput.Select, false);
            ulong consumed = rig.Controller.LastSequence;
            rig.Controller.Consume(new(consumed, 1, 42, OverlayInput.Select, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 99, OverlayInput.Select, true), () => true);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, false), () => true);
            int checks = 0;
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true), () => ++checks == 1);
            var rejected = rig.Current();
            TestAssert.Equal(before.Appearance, rejected.Appearance);
            TestAssert.Equal(before.MenuRevision, rejected.MenuRevision);
            TestAssert.True(rejected.MenuPreferenceRebuild is null && rejected.MenuPreferenceRefresh is null, "Rejected commands cannot retain a latent timer");
            rig.Clock.Advance(TimeSpan.FromSeconds(1));
            TestAssert.Equal(before.Appearance, rig.Current().Appearance);
            rig.NoGameWork();
        }
    }

    private sealed class ThrowingClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new();
        internal bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }
}
