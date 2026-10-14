using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;

namespace Backend.Tests;

internal static class MenuDesignPublicationTests
{
    public static void VisibleAndHiddenDesignEdgesPublishOnceAtFiftyAnd250WithoutSessionReadsOrHeartbeatLoss()
    {
        foreach (bool focused in new[] { true, false })
        {
            var rig = new MenuDesignTests.Rig(); rig.Open(); rig.Stage("Classic"); rig.Send(syndishanx_iw_tool.Overlay.OverlayInput.Select);
            var frames = new List<StateDocument>(); var publisher = new MenuPhasePublication(frames.Add, rig.Clock); publisher.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Rebuild cannot churn 25ms owner frames");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "The redraw edge precedes the heartbeat");
            publisher.Publish(rig.Current(), focused); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "A committed edge publishes once");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Stable cooldown still publishes its heartbeat");
            publisher.Publish(rig.Current(), focused); rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); publisher.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Unlock publishes even just after the last heartbeat");
            publisher.Publish(rig.Current(), focused); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Unlock has no duplicate frame");
            TestAssert.Equal(5, frames.Count); TestAssert.True(frames.All(frame => frame.IsOpen == focused && (focused || frame.MenuDesignTransition is null)), "Projection hides metadata without ending its owner"); rig.NoGameWork();
        }
    }

    public static void FailedPublicationKeepsPhaseFocusAndCadenceAndCommitsTheActualFrameBoundary()
    {
        var rig = new MenuDesignTests.Rig(); rig.Open(); rig.Stage("Basic"); rig.Send(syndishanx_iw_tool.Overlay.OverlayInput.Select);
        bool fail = false; int writes = 0; var publisher = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned write blocked"); writes++; }, rig.Clock);
        publisher.Publish(rig.Current(), true); rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.Equal("design:rebuild", rig.Controller.ReadMenuPublicationPhase());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var actual = rig.Current(); fail = true; TestAssert.Throws<IOException>(() => publisher.Publish(actual, false));
        TestAssert.True(!publisher.HasChanged("design:rebuild") && publisher.HasChanged("design:cooldown"), "Failed writes cannot retire the last committed phase");
        TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Failed focus and phase changes remain due");
        fail = false; publisher.Publish(actual, false); TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "Only successful actual state commits cadence"); TestAssert.Equal(2, writes); rig.NoGameWork();
    }
}
