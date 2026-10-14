using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class InstructionPositionPublicationTests {
    public static void EachOwnerPhaseCommitsOnceWithoutTwentyFiveMillisecondSpinOrDuplicateHiddenFrames() {
        var visit = InstructionPositionEditorTests.Open(); visit.Enter();
        var frames = new List<StateDocument>(); var publication = new MenuPhasePublication(frames.Add, visit.Clock);
        publication.Publish(visit.Controller.Snapshot(), false);
        for (int step = 1; step <= 3; step++) {
            visit.Clock.Advance(TimeSpan.FromMilliseconds(25));
            TestAssert.True(!publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false),
                "An unchanged position phase cannot publish each owner loop");
        }
        visit.Clock.Advance(TimeSpan.FromMilliseconds(25));
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Editing must publish at100ms");
        publication.Publish(visit.Controller.Snapshot(), false);
        TestAssert.True(!publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Editing was already committed");
        visit.Send(OverlayInput.FinishInstructionPositionEditor);
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Exiting must publish immediately");
        publication.Publish(visit.Controller.Snapshot(), false);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100));
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Parent rebuild must publish when exit completes");
        publication.Publish(visit.Controller.Snapshot(), false);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Pause transition must publish before heartbeat");
        publication.Publish(visit.Controller.Snapshot(), false);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100));
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Final unlocked parent must publish");
        publication.Publish(visit.Controller.Snapshot(), false);
        TestAssert.Equal(6, frames.Count);
        TestAssert.True(frames.All(frame => !frame.IsOpen && frame.InstructionPositionEditor is null && frame.MenuUnlock is null),
            "All hidden publications must remain valid closed renderer frames");
    }
    public static void FailedWritesRetainTheActualCommittedOwnerPhaseAndHeartbeatUntilRetry() {
        var visit = InstructionPositionEditorTests.Open(); visit.Enter(); bool fail = false; int writes = 0;
        var publication = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned publication blocked"); writes++; }, visit.Clock);
        publication.Publish(visit.Controller.Snapshot(), true);
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); var editing = visit.Controller.Snapshot(); fail = true;
        TestAssert.Throws<IOException>(() => publication.Publish(editing, false));
        TestAssert.True(!publication.HasChanged("position:entering") && publication.HasChanged("position:editing"),
            "A rejected write cannot commit the new editor phase");
        TestAssert.True(publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Failed focus or phase publication must remain due");
        fail = false; publication.Publish(editing, false);
        TestAssert.True(!publication.ShouldPublish(visit.Controller.ReadMenuPublicationPhase(), false), "Successful retry must start one new cadence");
        TestAssert.Equal(2, writes);
    }
    public static void PublicationUsesTheActualSnapshotAfterTheClockCrossesTheQueriedTransition() {
        var visit = InstructionPositionEditorTests.Open(); visit.Enter();
        var publication = new MenuPhasePublication(_ => { }, visit.Clock);
        TestAssert.Equal("position:entering", visit.Controller.ReadMenuPublicationPhase());
        visit.Clock.Advance(TimeSpan.FromMilliseconds(100)); publication.Publish(visit.Controller.Snapshot(), true);
        TestAssert.True(!publication.HasChanged("position:editing") && publication.HasChanged("position:entering"),
            "Publication must record the snapshot actually written rather than its earlier cheap query");
    }
}
