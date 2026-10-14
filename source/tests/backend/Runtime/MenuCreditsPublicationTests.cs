using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;

namespace Backend.Tests;

internal static class MenuCreditsPublicationTests
{
    public static void StableCreditsPhasePublishesElapsedHeartbeatWithoutDuplicateOwnerLoopFramesOrHiddenTimerLoss()
    {
        var rig = new MenuCreditsTests.Rig(); rig.Enter(); var frames = new List<StateDocument>();
        var publisher = new MenuPhasePublication(frames.Add, rig.Clock); publisher.Publish(rig.Current(), false);
        // Intermediate owner polls must not churn packets just because elapsed time advances
        for (int loop = 0; loop < 3; loop++)
        {
            rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Elapsed progress cannot create duplicate 25 ms frames");
        }
        rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
        TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "A stable credits phase still needs its 100 ms heartbeat");
        publisher.Publish(rig.Current(), false); TestAssert.Equal(100, rig.Current().MenuCredits?.ElapsedMilliseconds);
        TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Successful heartbeat commits exactly one cadence");
        // Hidden focus clears only the renderer projection, not the natural completion clock
        rig.Clock.Advance(TimeSpan.FromMilliseconds(39900));
        TestAssert.Equal("rebuild", rig.Controller.ReadMenuPublicationPhase());
        TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Natural completion publishes its actual parent phase");
        publisher.Publish(rig.Current(), false); TestAssert.True(!publisher.ShouldPublish("rebuild", false), "Completion cannot immediately publish twice");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); publisher.Publish(rig.Current(), false);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); publisher.Publish(rig.Current(), false);
        TestAssert.Equal(5, frames.Count);
        TestAssert.True(frames.All(frame => !frame.IsOpen && frame.MenuCredits is null && frame.MenuUnlock is null), "Every hidden packet must stay schema-valid");
        rig.NoGameWork();
    }

    public static void FailedWriteCannotCommitPhaseCadenceOrFocusAndSnapshotCompletionOverridesAnEarlierCreditsQuery()
    {
        var rig = new MenuCreditsTests.Rig(); rig.Enter(); bool fail = false; int writes = 0;
        var publisher = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned writer rejected"); writes++; }, rig.Clock);
        publisher.Publish(rig.Current(), true); rig.Clock.Advance(TimeSpan.FromMilliseconds(39999));
        TestAssert.Equal("credits", rig.Controller.ReadMenuPublicationPhase());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var completed = rig.Current(); fail = true;
        TestAssert.Throws<IOException>(() => publisher.Publish(completed, false));
        TestAssert.True(!publisher.HasChanged("credits") && publisher.HasChanged("rebuild"), "A failed completion packet cannot retire the committed credits phase");
        TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Failed phase, focus and heartbeat changes remain due");
        // Only a successful atomic write can retire the old committed credits phase
        fail = false; publisher.Publish(completed, false);
        TestAssert.True(!publisher.ShouldPublish("rebuild", false), "The actual snapshot wins over an earlier credits phase query");
        TestAssert.Equal(2, writes); rig.NoGameWork();
    }
}
