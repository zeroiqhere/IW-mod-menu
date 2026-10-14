using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Captured control epochs protect every route, including the parent of the editor
internal static class OpeningControlsScopeTests {
    public static void AnEditorCommandDeliveredAfterBackCannotSelectTheParentsCurrentRow() {
        var rig = new OpeningControlsRig();
        rig.Open();
        var captured = rig.Current();
        rig.Send(OverlayInput.Back);
        var parent = rig.Current();
        TestAssert.Equal("Menu Customization", parent.Title);
        foreach (var input in new[] { OverlayInput.Select, OverlayInput.NextChoice, OverlayInput.PreviousChoice }) {
            ulong sequence = rig.Sequence++;
            rig.Controller.Consume(new(sequence, 1, 42, input, true,
                captured.NavigationRevision, OpeningControlsRig.Revision(captured)), () => true);
            var rejected = rig.Current();
            TestAssert.Equal(parent.Title, rejected.Title);
            TestAssert.Equal(parent.NavigationRevision, rejected.NavigationRevision);
            TestAssert.Equal(sequence, rejected.LastSequence);
            TestAssert.True(rejected.MenuPreferenceRefresh is null, "Rejected captured inputs cannot own a later callback");
        }
        // The current parent frame remains actionable with its matching global epochs
        rig.Send(OverlayInput.Select);
        TestAssert.Equal("Open Controls", rig.Current().Title);
        rig.NoGameWork();
    }

    public static void CurrentGlobalCapturesAndLegacyOmissionsRemainActionableWhileOldPolicyChoicesReject() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Select("Use");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Bool Display");
        var before = rig.Current();
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.NextChoice, true,
            before.NavigationRevision, 1), () => true);
        TestAssert.Equal(before.Rows[before.SelectedIndex].Value, rig.Current().Rows[before.SelectedIndex].Value);
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("Text", rig.Current().Rows[before.SelectedIndex].Value);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal("Text", rig.Current().Appearance!.BooleanDisplay);
        TestAssert.Equal(2UL, OpeningControlsRig.Revision(rig.Current()));
        rig.NoGameWork();

        var legacy = new OpeningControlsRig(configured: false);
        legacy.Send(OverlayInput.Toggle);
        MenuOpenTestHarness.Settle(legacy.Controller, legacy.Clock);
        legacy.Select("Menu Customization");
        legacy.Select("Menu Preferences");
        legacy.Highlight("Bool Display");
        legacy.Send(OverlayInput.NextChoice);
        legacy.Send(OverlayInput.Select);
        TestAssert.Equal("Text", legacy.Current().Appearance!.BooleanDisplay);
        TestAssert.True(OpeningControlsRig.Revision(legacy.Current()) is null, "Legacy selection needs no invented policy epoch");
        legacy.NoGameWork();
    }
}
