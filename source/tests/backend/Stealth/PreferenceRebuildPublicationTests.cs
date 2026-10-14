using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class PreferenceRebuildPublicationTests {
    public static void VisibleAndHiddenRebuildEdgesCommitOnceWhileFailedPublicationRemainsDue() {
        foreach (bool focused in new[] { true, false }) {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Select("Option Counter");
            var frames = new List<StateDocument>();
            bool fail = false;
            var publisher = new MenuPhasePublication(frame => {
                if (fail) throw new IOException("Owned write unavailable");
                frames.Add(frame);
            }, rig.Clock);
            publisher.Publish(rig.Current(), focused);
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "A committed rebuild cannot publish every owner poll");
            int reads = rig.Session.MenuStateReads;
            rig.Session.RejectMenuStateReads = true;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(49));
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "The rebuild edge remains unchanged through49ms");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "The observed redraw must publish before its heartbeat");
            TestAssert.Equal(reads, rig.Session.MenuStateReads);
            rig.Session.RejectMenuStateReads = false;
            var applied = rig.Current();
            fail = true;
            TestAssert.Throws<IOException>(() => publisher.Publish(applied, focused));
            TestAssert.True(publisher.HasChanged(rig.Controller.ReadMenuPublicationPhase()), "Failed publication cannot commit the new phase");
            fail = false;
            publisher.Publish(applied, focused);
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Successful retry commits the actual phase once");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(199));
            publisher.Publish(rig.Current(), focused);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            TestAssert.True(publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "The independent input-ready edge must publish");
            publisher.Publish(rig.Current(), focused);
            TestAssert.True(!publisher.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), focused), "Ready opacity has a stable committed phase");
            TestAssert.True(frames.All(frame => frame.IsOpen == focused && (focused || frame.MenuPreferenceRebuild is null && frame.MenuOpening is null)), "Hidden frames omit active metadata without ending owner progress");
            rig.NoGameWork();
        }
    }
}
