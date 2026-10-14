using System.Text.Json;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class SavedOpeningActionCapabilitiesTests {
    public static void LegacyExactGroupsProveOnlyTheirTenActionsWithoutBorrowingCombinedScrollKeys() {
        var packet = Packet();
        var parsed = Parse(packet);
        TestAssert.True(parsed.OpeningActions is not null, "A valid packet must publish explicit action availability");
        TestAssert.True(parsed.OpeningActions!.Actions.SequenceEqual(new[] {
            OpeningAction.ActionSlot1, OpeningAction.ActionSlot2, OpeningAction.ActionSlot3, OpeningAction.ActionSlot4,
            OpeningAction.Melee, OpeningAction.Aim, OpeningAction.Attack, OpeningAction.Use, OpeningAction.Frag, OpeningAction.SecondaryOffhand
        }), "Legacy groups prove exactly ten canonical actions");
        packet.Remove("attack");
        packet.Remove("moveUp");
        packet.Remove("moveDown");
        var partial = Parse(packet);
        TestAssert.True(partial.OpeningActions is not null && !partial.OpeningActions.Contains(OpeningAction.Attack)
            && !partial.OpeningActions.Contains(OpeningAction.ActionSlot1) && !partial.OpeningActions.Contains(OpeningAction.ActionSlot2),
            "Combined up/down cannot prove exact action groups");
        TestAssert.True(partial.Menu.ChoiceControlsAvailable && !partial.Menu.WidthEditorControlsAvailable,
            "Individual proof preserves existing legacy capability semantics");
    }

    public static void OptionalExactSprintStanceAndJumpGroupsEnableOnlyExplicitlySuppliedActions() {
        var packet = Packet();
        packet["sprint"] = new[] { 67 };
        packet["stance"] = new[] { 68 };
        packet["jump"] = new[] { 74 };
        var complete = Parse(packet);
        TestAssert.Equal(13, complete.OpeningActions!.Actions.Count);
        TestAssert.True(complete.Menu.MenuPositionEditorControlsAvailable, "Existing complete controls remain available");
        packet["jump"] = Array.Empty<int>();
        TestAssert.True(!Parse(packet).OpeningActions!.Contains(OpeningAction.Jump), "An empty optional group stays unavailable");
        packet.Remove("sprint");
        TestAssert.True(!Parse(packet).OpeningActions!.Contains(OpeningAction.Sprint), "An omitted optional group has no fallback");
    }

    public static void MalformedOptionalGroupsRejectTheWholePacketAndCannotEnableAFeatureByAlias() {
        foreach (string name in new[] { "sprint", "stance", "jump" }) {
            foreach (object invalid in new object[] { true, "67", new[] { 0 }, new[] { 255 }, new[] { 67, 67 }, new[] { 2 }, Enumerable.Range(100, 17).ToArray() }) {
                var packet = Packet();
                packet[name] = invalid;
                var parsed = Parse(packet);
                TestAssert.Equal(SavedBindingCapabilities.Unavailable, parsed.Menu);
                TestAssert.True(parsed.OpeningActions is null, "Malformed input cannot publish partial opening proof");
            }
        }
        var supplied = Packet();
        supplied["sprint"] = new[] { 67 };
        string json = JsonSerializer.Serialize(supplied);
        var duplicate = SavedControlCapabilities.Parse(System.Text.Encoding.UTF8.GetBytes(json[..^1] + ",\"sprint\":[68]}"));
        TestAssert.True(duplicate.OpeningActions is null, "Repeated decoded fields cannot change the action set");
    }

    internal static Dictionary<string, object> Packet() => new() {
        ["version"] = 1, ["aim"] = new[] { 2 }, ["melee"] = new[] { 86 }, ["up"] = new[] { 2, 38 },
        ["down"] = new[] { 1, 40 }, ["select"] = new[] { 70 }, ["previousChoice"] = new[] { 51 },
        ["nextChoice"] = new[] { 52 }, ["attack"] = new[] { 1 }, ["moveUp"] = new[] { 38 },
        ["moveDown"] = new[] { 40 }, ["frag"] = new[] { 71 }, ["secondaryOffhand"] = new[] { 72 }
    };
    private static SavedControlCapabilities Parse(Dictionary<string, object> packet) =>
        SavedControlCapabilities.Parse(JsonSerializer.SerializeToUtf8Bytes(packet));
}
