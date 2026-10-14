using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Every profile drives the real owner with a bounded private saved-binding packet
internal static class OpeningControlsFixtureExporter {
    internal const int TotalFixtureCount = SpacerScrollFixtureExporter.TotalFixtureCount;
    internal static void Add(IDictionary<string, StateDocument> fixtures) {
        var defaults = Create();
        defaults.Open();
        Capture("default", defaults);
        defaults.Highlight("Bind Slot");
        defaults.Send(OverlayInput.NextChoice);
        Capture("slot-draft", defaults);
        defaults.Send(OverlayInput.Select);
        defaults.Wait(250);
        Capture("slot2-applied", defaults);
        defaults.SetSlot(3);
        Capture("slot3-applied", defaults);
        defaults.Highlight("Menu");
        defaults.Send(OverlayInput.NextChoice);
        Capture("quick-candidate", defaults);
        defaults.Send(OverlayInput.Select);
        Capture("quick-applied", defaults);
        defaults.NoGameWork();

        var change = Create();
        change.Open();
        change.Select("Use");
        Capture("change-pending", change);
        change.Wait(179);
        Capture("change-pre-refresh", change);
        change.Wait(1);
        Capture("change-cooldown", change);
        change.Wait(249);
        Capture("change-pre-unlock", change);
        change.Wait(1);
        Capture("change-unlocked", change);
        Capture("hidden", change, hidden: true);
        change.Send(OverlayInput.Toggle);
        Capture("closed", change);
        change.NoGameWork();

        var reselect = Create();
        reselect.Open();
        reselect.Select("Aim");
        Capture("reselect-pending", reselect);
        reselect.Wait(430);
        reselect.SetSlot(3);
        reselect.Select("None");
        Capture("none-no-op-pending", reselect);
        reselect.NoGameWork();

        var third = Create();
        third.Open();
        third.SetSlot(3);
        third.Select("Frag");
        Capture("third-action-pending", third);
        third.Wait(430);
        third.SetSlot(2);
        third.Select("Secondary Offhand");
        Capture("offhand-conflict-pending", third);
        third.Wait(430);
        third.Select("None");
        Capture("none-truncate-pending", third);
        third.Wait(430);
        Capture("none-truncate-unlocked", third);
        third.NoGameWork();

        var unavailable = Create(partial: true);
        unavailable.Open();
        Capture("unavailable-actions", unavailable);
        unavailable.NoGameWork();

        var selectors = Create();
        selectors.Open();
        selectors.Select("Menu");
        Capture("selector-same-menu", selectors);
        selectors.Wait(250);
        selectors.Select("Bind Slot");
        Capture("selector-same-slot", selectors);
        selectors.NoGameWork();
        SpacerScrollFixtureExporter.Add(fixtures);

        void Capture(string name, OpeningControlsRig rig, bool hidden = false) {
            var state = rig.Current();
            fixtures.Add("open-controls-" + name + ".json", hidden ? StateFocusProjection.Apply(state, false) : state);
        }
    }

    internal static OpeningControlsRig Create(bool partial = false) {
        var packet = SavedOpeningActionCapabilitiesTests.Packet();
        if (partial) {
            // A real minimal producer still proves exact Attack and both choice actions
            foreach (string group in new[] { "moveUp", "moveDown", "frag", "secondaryOffhand" }) packet.Remove(group);
        } else {
            packet["sprint"] = new[] { 67 };
            packet["stance"] = new[] { 68 };
            packet["jump"] = new[] { 74 };
        }
        var proof = SavedControlCapabilities.Parse(JsonSerializer.SerializeToUtf8Bytes(packet));
        TestAssert.True(proof.Menu.ChoiceControlsAvailable && proof.OpeningActions is not null,
            "One validated saved packet must prove both selectors and action availability");
        return new(proof.OpeningActions);
    }
}
