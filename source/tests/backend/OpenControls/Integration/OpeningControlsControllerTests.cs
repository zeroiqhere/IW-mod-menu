using System.Text.Json;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class OpeningControlsControllerTests {
    public static void LegacyControllersOmitTheDescriptorAndKeepOpenControlsUnavailable() {
        var rig = new OpeningControlsRig(configured: false);
        TestAssert.True(OpeningControlsRig.Revision(rig.Current()) is null, "Legacy callers must not gain a new policy");
        rig.Send(OverlayInput.Toggle);
        MenuOpenTestHarness.Settle(rig.Controller, rig.Clock);
        rig.Select("Menu Customization");
        var row = rig.Current().Rows.Single(item => item.Label == "Open Controls");
        TestAssert.True(!row.Enabled && row.Value == "UNAVAILABLE", "Unconfigured integration must remain unavailable");
        using var document = JsonDocument.Parse(rig.Current().Serialize());
        foreach (var item in document.RootElement.GetProperty("rows").EnumerateArray()) {
            TestAssert.True(!item.TryGetProperty("openingAction", out _) && !item.TryGetProperty("isSpacer", out _), "Legacy rows keep their original shape");
        }
        rig.NoGameWork();
    }

    public static void MainEditorHasSourceInventoryTypedRowsAndOnlyVerifiedActionsAvailable() {
        var packet = SavedOpeningActionCapabilitiesTests.Packet();
        foreach (string group in new[] { "moveUp", "moveDown", "attack", "frag", "secondaryOffhand" }) packet.Remove(group);
        // This legacy packet retains exact choice actions but omits the optional attack proof
        var proof = SavedControlCapabilities.Parse(JsonSerializer.SerializeToUtf8Bytes(packet));
        var partial = proof.OpeningActions;
        TestAssert.True(partial is not null && proof.Menu.ChoiceControlsAvailable, "One coherent saved packet must prove both capabilities");
        var rig = new OpeningControlsRig(partial);
        rig.Open();
        var frame = rig.Current();
        string[] expected = { "Menu", "Bind Slot", "", "Action Slot 1", "Action Slot 2", "Action Slot 3", "Action Slot 4", "Melee", "Aim", "Attack", "Sprint", "Use", "Frag", "Secondary Offhand", "Stance", "Jump" };
        TestAssert.True(frame.Rows.Select(row => row.Label).SequenceEqual(expected), "Slot1 keeps exact source action order and one blank spacer");
        TestAssert.True(frame.Rows[2].IsSpacer && !frame.Rows[2].Enabled, "The spacer has explicit non-action identity");
        TestAssert.True(OpeningControlsRig.Actions(frame).SequenceEqual(new[] { 5, 4 }), "Main default is verified Aim plus Melee");
        for (int id = 0; id < 13; id++) {
            var row = frame.Rows[id + 3];
            TestAssert.Equal((OpeningAction?)id, row.OpeningAction);
            bool available = id is 2 or 3 or 4 or 5 or 8;
            TestAssert.Equal(available, row.Enabled);
            TestAssert.Equal(available ? id == 5 ? "ON" : "OFF" : "UNAVAILABLE", row.Value);
        }
        rig.NoGameWork();
    }

    public static void MainTuplePublishesImmediatelyWhileOldBoolRowsRemainUntilOne180MillisecondRedraw() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Use");
        var before = rig.Current();
        rig.Send(OverlayInput.Select);
        var pending = rig.Current();
        TestAssert.Equal(OpeningControlsRig.Revision(before) + 1, OpeningControlsRig.Revision(pending));
        TestAssert.True(OpeningControlsRig.Actions(pending).SequenceEqual(new[] { 8, 4 }), "Input policy commits at admission");
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("ON", pending.Rows.Single(row => row.Label == "Aim").Value);
        TestAssert.Equal("pending", pending.MenuPreferenceRefresh?.Phase);
        TestAssert.Equal(before.MenuRevision, pending.MenuRevision);
        rig.Wait(179);
        TestAssert.Equal("OFF", rig.Current().Rows.Single(row => row.Label == "Use").Value);
        rig.Wait(1);
        var refreshed = rig.Current();
        TestAssert.Equal("ON", refreshed.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("OFF", refreshed.Rows.Single(row => row.Label == "Aim").Value);
        TestAssert.Equal(before.MenuRevision + 1, refreshed.MenuRevision);
        TestAssert.Equal("cooldown", refreshed.MenuPreferenceRefresh?.Phase);
        rig.Wait(249);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is not null, "Inputs stay locked through429");
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Inputs resume at430");
        TestAssert.Equal(refreshed.MenuRevision, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void ReselectRejectsAndEmptyNoneIsAcceptedWithoutPolicyRevisionButBothStillRefresh() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Aim");
        var original = rig.Current();
        TestAssert.True(original.Rows[original.SelectedIndex].Enabled, "An already assigned source action remains selectable");
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(OpeningControlsRig.Revision(original), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        TestAssert.True(rig.Current().Notice.Contains("already", StringComparison.OrdinalIgnoreCase), "Reselect reports a concrete duplicate");
        rig.Wait(430);
        TestAssert.Equal(original.MenuRevision + 1, rig.Current().MenuRevision);
        rig.SetSlot(3);
        TestAssert.Equal(17, rig.Current().Rows.Count);
        rig.Highlight("None");
        var unchanged = rig.Current();
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(OpeningControlsRig.Revision(unchanged), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(430);
        TestAssert.Equal(unchanged.MenuRevision + 1, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void SelectorCandidatesApplyOnlyOnUseWith250MillisecondsOfIndependentCooldown() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Bind Slot");
        var old = rig.Current();
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("2", rig.Current().Rows[1].Value);
        TestAssert.Equal(16, rig.Current().Rows.Count);
        TestAssert.Equal(old.MenuRevision, rig.Current().MenuRevision);
        rig.Send(OverlayInput.Select);
        var applied = rig.Current();
        TestAssert.Equal(17, applied.Rows.Count);
        TestAssert.Equal(old.MenuRevision + 1, applied.MenuRevision);
        TestAssert.Equal("cooldown", applied.MenuPreferenceRefresh?.Phase);
        TestAssert.Equal(OpeningControlsRig.Revision(old), OpeningControlsRig.Revision(applied));
        rig.Wait(249);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is not null, "Selector Use owns its source250ms wait");
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Selector cooldown has no extra180ms delay");
        rig.Highlight("Menu");
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("Quick Menu", rig.Current().Rows[0].Value);
        rig.Send(OverlayInput.Select);
        TestAssert.True(rig.Current().Notice.Contains("applied: Quick Menu", StringComparison.Ordinal), "Verified Quick configuration becomes the applied editor target");
        TestAssert.True(rig.Current().Notice.Contains("Quick opening is unavailable", StringComparison.Ordinal), "Configuration does not grant an opening path");
        TestAssert.True(OpeningControlsRig.Actions(rig.Current()).SequenceEqual(new[] { 5, 4 }), "Selecting an editor target cannot change Main policy");
        TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(250);
        rig.NoGameWork();
    }
}
