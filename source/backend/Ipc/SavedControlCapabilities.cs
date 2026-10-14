using System.Text.Json;
using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Ipc;

// One strict private packet supplies both old menu flags and individual action proof
internal sealed record SavedControlCapabilities(SavedBindingCapabilities Menu, VerifiedOpeningActions? OpeningActions) {
    public static SavedControlCapabilities Unavailable { get; } = new(SavedBindingCapabilities.Unavailable, null);
    public static SavedControlCapabilities Parse(ReadOnlySpan<byte> bytes) {
        if (bytes.Length is <= 0 or > SavedBindingCapabilities.MaximumBytes) return Unavailable;
        try {
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unavailable;
            var fields = root.EnumerateObject().ToArray();
            if (fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length
                || fields.Any(field => field.Name is not ("version" or "aim" or "melee" or "up" or "down" or "select"
                    or "previousChoice" or "nextChoice" or "attack" or "moveUp" or "moveDown"
                    or "frag" or "secondaryOffhand" or "sprint" or "stance" or "jump"))
                || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int number) || number != 1) return Unavailable;
            string[] groups = {
                "aim", "melee", "up", "down", "select", "previousChoice", "nextChoice", "attack",
                "moveUp", "moveDown", "frag", "secondaryOffhand", "sprint", "stance", "jump"
            };
            var membership = new int[255];
            var availableGroups = new bool[groups.Length];
            bool choicesAvailable = true;
            bool attackAvailable = false;
            bool moveUpAvailable = false;
            bool moveDownAvailable = false;
            bool fragAvailable = false;
            bool secondaryOffhandAvailable = false;
            // The first five source groups are required even for older choice-only packets
            for (int group = 0; group < groups.Length; group++) {
                if (!root.TryGetProperty(groups[group], out var values)) {
                    if (group < 5) return Unavailable;
                    if (group < 7) choicesAvailable = false;
                    continue;
                }
                if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 16) return Unavailable;
                if (values.GetArrayLength() == 0) {
                    if (group < 5) return Unavailable;
                    if (group < 7) choicesAvailable = false;
                }
                availableGroups[group] = values.GetArrayLength() != 0;
                if (group == 7) attackAvailable = values.GetArrayLength() != 0;
                if (group == 8) moveUpAvailable = values.GetArrayLength() != 0;
                if (group == 9) moveDownAvailable = values.GetArrayLength() != 0;
                if (group == 10) fragAvailable = values.GetArrayLength() != 0;
                if (group == 11) secondaryOffhandAvailable = values.GetArrayLength() != 0;
                foreach (var value in values.EnumerateArray()) {
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int key) || key is < 1 or > 254
                        || (membership[key] & (1 << group)) != 0) return Unavailable;
                    membership[key] |= 1 << group;
                    // Exact action groups share only their own combined scrolling group
                    // Grenade keys remain separate from every movement and menu group
                    if ((membership[key] & (membership[key] - 1)) != 0
                        && membership[key] != ((1 << 0) | (1 << 2))
                        && membership[key] != ((1 << 3) | (1 << 7))
                        && membership[key] != ((1 << 2) | (1 << 8))
                        && membership[key] != ((1 << 3) | (1 << 9))) return Unavailable;
                    // Attack must be explicitly imported, never inferred from a Down key
                    if (group == 7 && (membership[key] & (1 << 3)) == 0) return Unavailable;
                    // Position controls must be imported as slots, not borrowed from aim or fire
                    if (group == 8 && (membership[key] & (1 << 2)) == 0) return Unavailable;
                    if (group == 9 && (membership[key] & (1 << 3)) == 0) return Unavailable;
                }
            }
            // Width also needs both offset directions; no Down key becomes a guessed attack
            var menu = new SavedBindingCapabilities(choicesAvailable, choicesAvailable && attackAvailable,
                choicesAvailable && moveUpAvailable && moveDownAvailable,
                choicesAvailable && moveUpAvailable && moveDownAvailable && fragAvailable && secondaryOffhandAvailable);
            // IDs follow source order while each maps to one exact validated private group
            int[] actionGroups = { 8, 9, 5, 6, 1, 0, 7, 12, 4, 10, 11, 13, 14 };
            var actions = new List<OpeningAction>(13);
            for (int id = 0; id < actionGroups.Length; id++) {
                if (availableGroups[actionGroups[id]]) actions.Add((OpeningAction)id);
            }
            if (!VerifiedOpeningActions.TryCreate(actions, out var opening, out _)) return Unavailable;
            return new(menu, opening);
        } catch (JsonException) { return Unavailable; }
    }
}
