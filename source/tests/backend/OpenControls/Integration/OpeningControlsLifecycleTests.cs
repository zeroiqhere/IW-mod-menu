using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Local control edits use the real controller and a clock that never waits on a process
internal static class OpeningControlsLifecycleTests {
    public static void RejectedQuickTargetFeedbackRemainsTrueAfterAnotherCandidateIsStaged() {
        // Missing exact SecondaryOffhand proof preserves the honest Main-only rejection path
        var rig = OpeningControlsFixtureExporter.Create(partial: true);
        rig.Open();
        rig.Highlight("Menu");
        rig.Send(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(250);
        rig.Send(OverlayInput.NextChoice);
        var frame = rig.Current();
        TestAssert.Equal("Apparition", frame.Rows[0].Value);
        TestAssert.True(!frame.Notice.Contains("Candidate: Quick Menu", StringComparison.Ordinal),
            "Historical rejection feedback cannot describe a candidate that has already changed");
        TestAssert.True(frame.Notice.Contains("Candidate: Apparition", StringComparison.Ordinal),
            "The current row remains the authority for the staged target");
        rig.NoGameWork();
    }

    public static void AppliedSummaryChangesImmediatelyAndNeverUsesStagedSelectorsOrCapturedCheckboxes() {
        var rig = new OpeningControlsRig();
        rig.Open();
        TestAssert.True(rig.Current().Notice.Contains("Applied main chord: Aim + Melee", StringComparison.Ordinal),
            "The complete applied chord must be visible without visiting every slot");
        rig.Highlight("Bind Slot");
        rig.Send(OverlayInput.NextChoice);
        TestAssert.True(rig.Current().Notice.Contains("Applied main chord: Aim + Melee", StringComparison.Ordinal),
            "A staged selector is not an applied policy");
        rig.Highlight("Use");
        rig.Send(OverlayInput.Select);
        var pending = rig.Current();
        TestAssert.True(pending.Notice.Contains("Applied main chord: Use + Melee", StringComparison.Ordinal),
            "The summary uses the actual tuple even while checkboxes retain their previous state");
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "Use").Value);
        rig.NoGameWork();
    }

    public static void AssignedElsewhereStaysOffAndSelectableButRejectsWithoutChangingThePolicy() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Melee");
        var before = rig.Current();
        TestAssert.Equal("OFF", before.Rows[before.SelectedIndex].Value);
        TestAssert.True(before.Rows[before.SelectedIndex].Enabled, "Assignment elsewhere cannot disable a source bool row");
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(OpeningControlsRig.Revision(before), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.True(OpeningControlsRig.Actions(rig.Current()).SequenceEqual(new[] { 5, 4 }), "A duplicate cannot move an action between slots");
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(430);
        TestAssert.Equal(before.MenuRevision + 1, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void SameMenuSkipsRedrawWhileSameBindSlotRedrawsOnceAndBothWait250Milliseconds() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Menu");
        ulong? original = rig.Current().MenuRevision;
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(original, rig.Current().MenuRevision);
        rig.Wait(249);
        TestAssert.Equal("selector", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(1);
        rig.Highlight("Bind Slot");
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(original + 1, rig.Current().MenuRevision);
        TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(250);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "A same-slot redraw cannot add a bool refresh delay");
        rig.NoGameWork();
    }

    public static void ThirdSlotOffhandConflictAndNoneTruncationKeepConsecutiveSourceTuples() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.SetSlot(3);
        rig.Select("Frag");
        TestAssert.True(OpeningControlsRig.Actions(rig.Current()).SequenceEqual(new[] { 5, 4, 9 }), "A third action extends only a consecutive tuple");
        rig.Wait(430);
        rig.SetSlot(2);
        rig.Select("Secondary Offhand");
        TestAssert.True(OpeningControlsRig.Actions(rig.Current()).SequenceEqual(new[] { 5, 4, 9 }), "Frag and secondary offhand cannot coexist across slots");
        TestAssert.True(rig.Current().Notice.Contains("offhand", StringComparison.OrdinalIgnoreCase), "Conflict feedback identifies the rejected pair");
        rig.Wait(430);
        rig.Select("None");
        var pending = rig.Current();
        TestAssert.True(OpeningControlsRig.Actions(pending).SequenceEqual(new[] { 5 }), "Clearing slot2 also clears slot3 immediately");
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "None").Value);
        rig.Wait(430);
        TestAssert.Equal("ON", rig.Current().Rows.Single(row => row.Label == "None").Value);
        // An absent second slot cannot be bypassed by applying a third action
        rig.SetSlot(3);
        var beforeGap = rig.Current();
        rig.Select("Frag");
        TestAssert.Equal(OpeningControlsRig.Revision(beforeGap), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.True(OpeningControlsRig.Actions(rig.Current()).SequenceEqual(new[] { 5 }), "A rejected gap cannot grow the tuple");
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.NoGameWork();
    }

    public static void ClockRollbackRestartsOnlyTheRemainingStageAndLatePollingRedrawsOnce() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Select("Use");
        ulong? before = rig.Current().MenuRevision;
        rig.Wait(100);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(179);
        TestAssert.Equal(before, rig.Current().MenuRevision);
        rig.Wait(1);
        TestAssert.Equal(before + 1, rig.Current().MenuRevision);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(249);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is not null, "Rollback cannot shorten the remaining cooldown");
        rig.Wait(1);
        TestAssert.Equal(before + 1, rig.Current().MenuRevision);
        rig.Highlight("Aim");
        rig.Send(OverlayInput.Select);
        ulong? late = rig.Current().MenuRevision;
        rig.Wait(1000);
        TestAssert.Equal(late + 1, rig.Current().MenuRevision);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "One late poll performs the refresh before releasing the wait");
        rig.NoGameWork();
    }

    public static void ClosedAndHiddenSnapshotsRetainOnlyTheActualImmutableOpeningDescriptor() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Select("Use");
        var actual = OpeningControlsRig.Actions(rig.Current());
        var hidden = StateFocusProjection.Apply(rig.Current(), false);
        TestAssert.True(!hidden.IsOpen, "Lost foreground hides the view");
        TestAssert.True(OpeningControlsRig.Actions(hidden).SequenceEqual(actual), "Hidden policy cannot revert to cached row state");
        rig.Wait(430);
        rig.Send(OverlayInput.Toggle);
        var closed = rig.Current();
        TestAssert.True(!closed.IsOpen, "Toggle closes the ordinary view");
        TestAssert.True(OpeningControlsRig.Actions(closed).SequenceEqual(actual), "Closing a view cannot erase the applied startup policy");
        rig.NoGameWork();
    }
}
