using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;

namespace Backend.Tests;

internal static class MenuOpeningPublicationTests {
    public static void OpeningEdgesAndIndependentCompletionPublishWithStableHeartbeatInVisibleAndHiddenFrames() {
        foreach (bool focused in new[] { true, false }) {
            var rig = MenuOpeningTests.Start(); var frames = new List<StateDocument>(); var publication = new MenuPhasePublication(frames.Add, rig.Clock); publication.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.True(!publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Stable rebuild has no extra owner-loop frames");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Actual redraw immediately publishes opening");
            publication.Publish(rig.Current(), focused); TestAssert.True(!publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "A committed event cannot publish twice");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); TestAssert.True(!publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Stable fade elapsed uses the existing100ms heartbeat");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); publication.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); publication.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Design input readiness is independent of continuing opening"); publication.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(699)); publication.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Final opening completion publishes even after a frame one millisecond earlier"); publication.Publish(rig.Current(), focused);
            TestAssert.True(!publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Completion starts one fresh heartbeat interval");
            TestAssert.Equal(7, frames.Count); TestAssert.True(frames.All(frame => frame.IsOpen == focused && (focused || frame.MenuOpening is null)), "Hidden publication tracks owner phases without invalid closed metadata"); rig.NoGameWork();
        }
    }

    public static void FailedOpeningPublicationRetainsItsCommittedPhaseFocusAndUsesTheActualSnapshotBoundary() {
        var rig = MenuOpeningTests.Start(); bool fail = false; int writes = 0;
        var publication = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned packet blocked"); writes++; }, rig.Clock); publication.Publish(rig.Current(), true);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.Equal("design:rebuild", rig.Controller.ReadMenuPublicationPhase());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var drawn = rig.Current(); fail = true; TestAssert.Throws<IOException>(() => publication.Publish(drawn, false));
        TestAssert.True(!publication.HasChanged("design:rebuild") && publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Failed write cannot advance phase or focus");
        fail = false; publication.Publish(drawn, false); TestAssert.True(!publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "The actual opening snapshot wins over the earlier rebuild query"); TestAssert.Equal(2, writes); rig.NoGameWork();
    }
}
