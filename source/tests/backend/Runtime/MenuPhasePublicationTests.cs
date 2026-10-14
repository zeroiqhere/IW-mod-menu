using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPhasePublicationTests {
    public static void OwnerPhaseTransitionsPublishAtExactBoundariesWithoutDuplicateFrames() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        var frames = new List<StateDocument>();
        var publication = new MenuPhasePublication(frames.Add);
        TestAssert.True(!publication.HasChanged(controller.ReadMenuUnlockPhase()), "No active pause cannot trigger an extra frame");
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.Equal("rebuild", controller.ReadMenuUnlockPhase());
        TestAssert.True(publication.HasChanged(controller.ReadMenuUnlockPhase()), "Finish must publish its first owner phase");
        publication.Publish(controller.Snapshot(), true);
        TestAssert.True(!publication.HasChanged(controller.ReadMenuUnlockPhase()), "An already committed phase cannot publish twice");
        clock.Advance(TimeSpan.FromMilliseconds(49));
        TestAssert.True(!publication.HasChanged(controller.ReadMenuUnlockPhase()), "Rebuild is unchanged at49ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.HasChanged(controller.ReadMenuUnlockPhase()), "Pause must publish at50ms even before a heartbeat");
        publication.Publish(controller.Snapshot(), true);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.True(!publication.HasChanged(controller.ReadMenuUnlockPhase()), "Pause remains unchanged at149ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(publication.HasChanged(controller.ReadMenuUnlockPhase()), "Unlock must publish at150ms rather than wait for the next heartbeat");
        publication.Publish(controller.Snapshot(), true);
        TestAssert.True(!publication.HasChanged(controller.ReadMenuUnlockPhase()), "The unlocked phase cannot trigger duplicate frames");
        TestAssert.True(frames.Select(frame => frame.MenuUnlock).SequenceEqual(new string?[] { "rebuild", "pause", null }),
            "Exactly three committed owner phases must be published");
    }

    public static void FocusHiddenPublicationsTrackOwnerProgressRatherThanTheirClearedNativeField() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        var frames = new List<StateDocument>();
        var publication = new MenuPhasePublication(frames.Add);
        publication.Publish(controller.Snapshot(), false);
        TestAssert.True(!publication.HasChanged("rebuild"), "A hidden display must still commit its actual owner phase");
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.True(publication.HasChanged(controller.ReadMenuUnlockPhase()), "A hidden owner still advances to pause");
        publication.Publish(controller.Snapshot(), false);
        TestAssert.True(!publication.HasChanged("pause"), "Hidden pause cannot trigger a frame every loop");
        clock.Advance(TimeSpan.FromMilliseconds(100));
        TestAssert.True(publication.HasChanged(controller.ReadMenuUnlockPhase()), "A hidden owner still publishes unlock");
        publication.Publish(controller.Snapshot(), false);
        TestAssert.True(!publication.HasChanged(null), "Unlock is committed independently of focus");
        TestAssert.Equal(3, frames.Count);
        TestAssert.True(frames.All(frame => !frame.IsOpen && frame.MenuUnlock is null && frame.WidthEditor is null),
            "Every focus-hidden packet must remain valid closed native state");
    }

    public static void PublicationTracksTheActualSnapshotWhenClockAdvancesAfterTheDueQuery() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        var frames = new List<StateDocument>();
        var publication = new MenuPhasePublication(frames.Add);
        publication.Publish(controller.Snapshot(), true);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        string? queried = controller.ReadMenuUnlockPhase();
        TestAssert.Equal("pause", queried);
        TestAssert.True(publication.HasChanged(queried), "The query must first detect a genuine transition");
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var actual = controller.Snapshot();
        TestAssert.True(actual.MenuUnlock is null, "Snapshot may legitimately cross a later phase boundary");
        publication.Publish(actual, true);
        TestAssert.True(!publication.HasChanged(null), "Tracking must use the packet actually committed rather than the earlier query");
        TestAssert.True(publication.HasChanged("pause"), "The earlier pause was never the committed packet");
        TestAssert.True(frames.Select(frame => frame.MenuUnlock).SequenceEqual(new string?[] { "rebuild", null }),
            "A crossed phase cannot be invented in publication history");
    }

    public static void FailedAtomicPublicationRetainsItsPhaseAndGenerationForASuccessfulRetry() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        string directory = Path.Combine(Path.GetTempPath(), "apparition-phase-publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            var publication = new MenuPhasePublication(files.WriteState);
            publication.Publish(controller.Snapshot(), true);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            var pause = controller.Snapshot();
            Directory.CreateDirectory(Path.Combine(directory, "state.json.tmp"));
            TestAssert.Throws<SessionFileException>(() => publication.Publish(pause, true));
            TestAssert.True(publication.HasChanged("pause") && !publication.HasChanged("rebuild"),
                "A failed atomic write must retain the last committed phase");
            using (var before = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "state.json")))) {
                TestAssert.Equal(1ul, before.RootElement.GetProperty("generation").GetUInt64());
                TestAssert.Equal("rebuild", before.RootElement.GetProperty("menuUnlock").GetString());
            }
            Directory.Delete(Path.Combine(directory, "state.json.tmp"));
            publication.Publish(pause, true);
            TestAssert.True(!publication.HasChanged("pause"), "A successful retry must commit the new phase");
            using var after = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "state.json")));
            TestAssert.Equal(2ul, after.RootElement.GetProperty("generation").GetUInt64());
            TestAssert.Equal("pause", after.RootElement.GetProperty("menuUnlock").GetString());
            TestAssert.Equal(controller.LastSequence, after.RootElement.GetProperty("lastSequence").GetUInt64());
        } finally { Directory.Delete(directory, true); }
    }
}
