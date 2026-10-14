using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPreferenceRefreshPublicationTests
{
    public static void VisibleAndHiddenOwnerPhasesPublishAt180And430WithStableHeartbeatCadenceAndNoDuplicateFrames()
    {
        foreach (bool focused in new[] { true, false })
        {
            var rig = new MenuPreferenceRefreshTests.Rig(); rig.Open(); rig.Select("Spotlight Cursor"); var frames = new List<StateDocument>();
            var publisher = new MenuPhasePublication(frames.Add, rig.Clock); publisher.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Stable pending phase cannot churn owner-loop frames");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "The100ms heartbeat remains independent");
            publisher.Publish(rig.Current(), focused); rig.Clock.Advance(TimeSpan.FromMilliseconds(79));
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "No extra refresh occurs at179ms");
            // The180ms phase edge must publish even80ms after the last committed heartbeat
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Cooldown transition publishes before the next heartbeat");
            publisher.Publish(rig.Current(), focused); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "A committed phase cannot publish twice");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); publisher.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Unlock transition must publish at430ms");
            publisher.Publish(rig.Current(), focused); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Successful unlock begins one new cadence");
            TestAssert.Equal(5, frames.Count);
            TestAssert.True(frames.All(frame => frame.IsOpen == focused && (focused || frame.MenuPreferenceRefresh is null)), "Hidden frames remain valid without ending the owner's timer"); rig.NoGameWork();
        }
    }

    public static void FailedPublicationRetainsCommittedPhaseFocusAndCadenceAndUsesTheActualSnapshotAfterAClockBoundary()
    {
        var rig = new MenuPreferenceRefreshTests.Rig(); rig.Open(); rig.Select("Large Cursor"); bool fail = false; int writes = 0;
        var publisher = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned publication blocked"); writes++; }, rig.Clock);
        publisher.Publish(rig.Current(), true); rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        TestAssert.Equal("preference:pending", rig.Controller.ReadMenuPublicationPhase()); rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var applied = rig.Current(); fail = true; TestAssert.Throws<IOException>(() => publisher.Publish(applied, false));
        TestAssert.True(!publisher.HasChanged("preference:pending") && publisher.HasChanged("preference:cooldown"), "A rejected packet cannot commit its new phase");
        TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Failed focus and phase publication remains due");
        // A successful retry records the actual frame rather than its earlier pending query
        fail = false; publisher.Publish(applied, false); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "The actual snapshot phase wins over its earlier query");
        TestAssert.Equal(2, writes); rig.NoGameWork();
    }
}
