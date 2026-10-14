using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPublicationCadenceTests {
    public static void InitialAndHundredMillisecondHeartbeatUseMonotonicCommitTimeWithoutDuplicateFrames() {
        var clock = new ManualTimeProvider();
        int writes = 0;
        var publication = new MenuPhasePublication(_ => writes++, clock);
        var frame = NormalFrame();
        TestAssert.True(publication.ShouldPublish(null, true), "Initial state must always be ready to publish");
        publication.Publish(frame, true);
        TestAssert.True(!publication.ShouldPublish(null, true), "The same committed instant cannot trigger another heartbeat");
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.ShouldPublish(null, true), "The heartbeat is not due at99ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(null, true), "Heartbeat must publish exactly at100ms");
        publication.Publish(frame, true);
        TestAssert.True(!publication.ShouldPublish(null, true), "A committed heartbeat cannot repeat at the same timestamp");
        TestAssert.Equal(2, writes);
    }

    public static void UtcJumpsDoNotChangeCadenceAndTheTimestampIsSampledAfterTheWrite() {
        var clock = new ManualTimeProvider();
        var publication = new MenuPhasePublication(_ => clock.Advance(TimeSpan.FromMilliseconds(40)), clock);
        publication.Publish(NormalFrame(), true);
        clock.MoveWallClock(TimeSpan.FromDays(1000));
        TestAssert.True(!publication.ShouldPublish(null, true), "A forward UTC jump cannot fabricate a heartbeat");
        clock.MoveWallClock(TimeSpan.FromDays(-2000));
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.ShouldPublish(null, true), "Cadence must start after the40ms write, independently of UTC");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(null, true), "A backwards UTC jump cannot freeze the100ms heartbeat");
    }

    public static void TimestampRollbackForcesOnePublicationAndThenRecoversItsBoundedCadence() {
        var clock = new ManualTimeProvider();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var publication = new MenuPhasePublication(_ => { }, clock);
        publication.Publish(NormalFrame(), true);
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(75));
        TestAssert.True(publication.ShouldPublish(null, true), "A counter rollback must force a fresh committed frame");
        publication.Publish(NormalFrame(), true);
        TestAssert.True(!publication.ShouldPublish(null, true), "A successful rollback recovery cannot spin");
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.ShouldPublish(null, true), "The recovered cadence still waits100ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(null, true), "The recovered clock continues publishing normally");
    }

    public static void SignedElapsedOverflowForcesAFreshFrameAndRestartsANormalHeartbeatInterval() {
        var clock = new ManualTimeProvider();
        clock.RollBackTimestamp(TimeSpan.MaxValue);
        clock.RollBackTimestamp(TimeSpan.FromTicks(1));
        var publication = new MenuPhasePublication(_ => { }, clock);
        publication.Publish(NormalFrame(), true);
        clock.Advance(TimeSpan.MaxValue);
        clock.Advance(TimeSpan.FromTicks(1));
        TestAssert.Equal(TimeSpan.MinValue, clock.GetElapsedTime(long.MinValue, 0));
        TestAssert.True(publication.ShouldPublish(null, true), "A negative elapsed delta cannot freeze all future heartbeats");
        publication.Publish(NormalFrame(), true);
        TestAssert.True(!publication.ShouldPublish(null, true), "The recovery frame starts one fresh interval");
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.ShouldPublish(null, true), "The recovered heartbeat is not due at99ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(null, true), "Normal cadence resumes100ms after recovery");
    }

    public static void FailedWriteCannotAdvanceHeartbeatFocusOrOwnerPhaseAndRollbackStaysDueUntilCommit() {
        var clock = new ManualTimeProvider();
        bool fail = false;
        var publication = new MenuPhasePublication(_ => {
            if (fail) {
                clock.Advance(TimeSpan.FromMilliseconds(30));
                throw new IOException("Controlled publication failure");
            }
        }, clock);
        var frame = NormalFrame() with { MenuUnlock = "rebuild" };
        publication.Publish(frame, true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.True(publication.ShouldPublish("pause", false), "Phase and focus changes trigger a frame before the heartbeat");
        fail = true;
        TestAssert.Throws<IOException>(() => publication.Publish(frame with { MenuUnlock = "pause" }, false));
        TestAssert.True(!publication.ShouldPublish("rebuild", true), "Failure cannot change the last committed focus or phase");
        TestAssert.True(publication.ShouldPublish("pause", false), "The failed phase and focus remain due");
        clock.Advance(TimeSpan.FromMilliseconds(20));
        TestAssert.True(publication.ShouldPublish("rebuild", true), "A failure cannot move the original100ms heartbeat deadline");
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(75));
        TestAssert.True(publication.ShouldPublish("rebuild", true), "Rollback must force publication even before the prior deadline");
        TestAssert.Throws<IOException>(() => publication.Publish(frame, true));
        TestAssert.True(publication.ShouldPublish("rebuild", true), "Rollback remains due until a frame actually commits");
        fail = false;
        publication.Publish(frame, true);
        TestAssert.True(!publication.ShouldPublish("rebuild", true), "Successful recovery commits all scheduling state together");
    }

    public static void RealOwnerPhasesAndFocusChangesTriggerTheSharedCadenceBeforeAHeartbeatIsDue() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        var frames = new List<StateDocument>();
        var publication = new MenuPhasePublication(frames.Add, clock);
        publication.Publish(controller.Snapshot(), true);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.True(publication.ShouldPublish(controller.ReadMenuUnlockPhase(), true), "Finish's new phase is due immediately");
        publication.Publish(controller.Snapshot(), true);
        clock.Advance(TimeSpan.FromMilliseconds(49));
        TestAssert.True(!publication.ShouldPublish(controller.ReadMenuUnlockPhase(), true), "No condition is due at49ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(controller.ReadMenuUnlockPhase(), true), "The owner pause publishes at50ms");
        publication.Publish(controller.Snapshot(), true);
        TestAssert.True(publication.ShouldPublish(controller.ReadMenuUnlockPhase(), false), "Focus loss publishes independently of timing");
        publication.Publish(controller.Snapshot(), false);
        TestAssert.True(!publication.ShouldPublish(controller.ReadMenuUnlockPhase(), false), "Focus-hidden owner progress cannot spin");
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.ShouldPublish(controller.ReadMenuUnlockPhase(), false), "Neither the owner phase nor heartbeat has changed at149ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.ShouldPublish(controller.ReadMenuUnlockPhase(), false), "Unlock and heartbeat combine into one due frame at150ms");
        publication.Publish(controller.Snapshot(), false);
        TestAssert.True(!publication.ShouldPublish(controller.ReadMenuUnlockPhase(), false), "Combined triggers cannot duplicate a frame");
        TestAssert.Equal(5, frames.Count);
        TestAssert.True(frames[^1].MenuUnlock is null && !frames[^1].IsOpen, "A hidden unlock must remain valid native state");
    }

    private static StateDocument NormalFrame() => StateDocument.Create(42, 1, true,
        new("Menu Customization", "PREVIEW", Array.Empty<MenuRow>(), ""), 0, isRootMenu: false);
}
