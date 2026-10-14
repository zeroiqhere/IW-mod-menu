using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuInitialOpenPublicationTests
{
    public static void ReadinessRedrawAndIndependentCompletionPublishOnceWithoutSessionWorkOrHiddenOwnerLoss()
    {
        foreach (bool focused in new[] { true, false })
        {
            var clock = new ManualTimeProvider();
            var session = new SessionBoundary();
            var controller = new BackendController(42, session, clock: clock);
            var frames = new List<StateDocument>();
            var publication = new MenuPhasePublication(frames.Add, clock);
            controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
            publication.Publish(controller.Snapshot(), focused);
            clock.Advance(TimeSpan.FromMilliseconds(49));
            TestAssert.True(!publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Rebuild has no duplicate owner-loop frames");
            clock.Advance(TimeSpan.FromMilliseconds(1));
            int reads = session.MenuStateReads;
            session.RejectMenuStateReads = true;
            TestAssert.True(publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Readiness requests a fresh actual-row snapshot");
            TestAssert.Equal(reads, session.MenuStateReads);
            session.RejectMenuStateReads = false;
            publication.Publish(controller.Snapshot(), focused);
            TestAssert.True(!publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Committed cooldown replaces the earlier readiness key");
            clock.Advance(TimeSpan.FromMilliseconds(549));
            publication.Publish(controller.Snapshot(), focused);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Input readiness publishes while the independent fade continues");
            publication.Publish(controller.Snapshot(), focused);
            clock.Advance(TimeSpan.FromMilliseconds(149));
            publication.Publish(controller.Snapshot(), focused);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Fade completion is independent from the monitor wait");
            publication.Publish(controller.Snapshot(), focused);
            TestAssert.True(!publication.ShouldPublish(controller.ReadMenuPublicationPhase(), focused), "Final completion starts one fresh heartbeat interval");
            TestAssert.True(frames.All(frame => frame.IsOpen == focused && (focused || frame.MenuOpenTransition is null && frame.MenuOpening is null)),
                "Hidden frames preserve owner progress without invalid closed metadata");
            TestAssert.Equal(6, frames.Count);
            TestAssert.Equal(0, session.Executed.Count);
        }
    }

    public static void FailedPublicationRetainsCommittedPhaseWhileActualRedrawOwnsItsOriginalCooldownStart()
    {
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, new SessionBoundary(), clock: clock);
        bool fail = false;
        var publication = new MenuPhasePublication(_ => { if (fail) throw new IOException("Owned write rejected"); }, clock);
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        publication.Publish(controller.Snapshot(), true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("open:ready", controller.ReadMenuPublicationPhase());
        var drawn = controller.Snapshot();
        fail = true;
        TestAssert.Throws<IOException>(() => publication.Publish(drawn, false));
        TestAssert.True(!publication.HasChanged("open:rebuild"), "Failed write cannot commit a new phase or focus");
        clock.Advance(TimeSpan.FromMilliseconds(100));
        TestAssert.True(publication.ShouldPublish(controller.ReadMenuPublicationPhase(), false), "The same actual redraw remains due after failure");
        fail = false;
        var retry = controller.Snapshot();
        TestAssert.Equal(100, retry.MenuOpening!.ElapsedMilliseconds);
        publication.Publish(retry, false);
        TestAssert.True(!publication.ShouldPublish(controller.ReadMenuPublicationPhase(), false), "A successful retry commits its actual owner key");
        clock.Advance(TimeSpan.FromMilliseconds(450));
        TestAssert.True(controller.Snapshot().MenuOpenTransition is null, "Publication retry does not restart the source monitor wait");
    }
}
