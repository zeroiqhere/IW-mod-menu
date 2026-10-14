using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Ipc;

// A recoverable sequence can be acknowledged even when the payload is rejected
public sealed record CommandEnvelope(ulong Sequence, int Version, int ProcessId,
    OverlayInput? Input, bool Valid, ulong? NavigationRevision = null, ulong? OpeningControlsRevision = null,
    ulong? LocalAdmissionRevision = null, ulong? MenuRevision = null) {
    public const int MaximumBytes = 4096;

    public static CommandEnvelope? Parse(ReadOnlySpan<byte> bytes) {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes) return null;
        try {
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            // Duplicate fields cannot silently change a command's meaning
            var fields = root.EnumerateObject().ToArray();
            var sequences = fields.Where(p => p.Name == "sequence").ToArray();
            if (sequences.Length != 1 || !sequences[0].Value.TryGetUInt64(out ulong sequence)) return null;
            bool unique = fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length;
            int version = ReadInt(root, "version");
            int pid = ReadInt(root, "processId");
            OverlayInput? input = null;
            if (root.TryGetProperty("input", out var key) && key.ValueKind == JsonValueKind.String) {
                input = key.GetString() switch {
                    "toggle" => OverlayInput.Toggle, "up" => OverlayInput.Up,
                    "down" => OverlayInput.Down, "select" => OverlayInput.Select,
                    "back" => OverlayInput.Back, "previousChoice" => OverlayInput.PreviousChoice,
                    "nextChoice" => OverlayInput.NextChoice, "close" => OverlayInput.Close,
                    "increaseWidth" => OverlayInput.IncreaseWidth, "decreaseWidth" => OverlayInput.DecreaseWidth,
                    "increaseWidthStep" => OverlayInput.IncreaseWidthStep, "decreaseWidthStep" => OverlayInput.DecreaseWidthStep,
                    "finishWidthEditor" => OverlayInput.FinishWidthEditor,
                    "moveInstructionsUp" => OverlayInput.MoveInstructionsUp, "moveInstructionsDown" => OverlayInput.MoveInstructionsDown,
                    "moveInstructionsLeft" => OverlayInput.MoveInstructionsLeft, "moveInstructionsRight" => OverlayInput.MoveInstructionsRight,
                    "finishInstructionPositionEditor" => OverlayInput.FinishInstructionPositionEditor,
                    "moveMenuUp" => OverlayInput.MoveMenuUp, "moveMenuDown" => OverlayInput.MoveMenuDown,
                    "moveMenuLeft" => OverlayInput.MoveMenuLeft, "moveMenuRight" => OverlayInput.MoveMenuRight,
                    "increaseMenuPositionStep" => OverlayInput.IncreaseMenuPositionStep,
                    "decreaseMenuPositionStep" => OverlayInput.DecreaseMenuPositionStep,
                    "finishMenuPositionEditor" => OverlayInput.FinishMenuPositionEditor,
                    "finishMenuCredits" => OverlayInput.FinishMenuCredits,
                    "openMain" => OverlayInput.OpenMain, "openQuick" => OverlayInput.OpenQuick,
                    "jumpBack" => OverlayInput.JumpBack, _ => null
                };
            }
            // Editor commands and Close must describe the context observed by the worker
            ulong? revision = null;
            bool revisionValid = true;
            if (root.TryGetProperty("navigationRevision", out var epoch)) {
                if (epoch.ValueKind == JsonValueKind.Number && epoch.TryGetUInt64(out ulong value)) revision = value;
                else revisionValid = false;
            }
            ulong? openingRevision = null;
            bool openingRevisionValid = true;
            if (root.TryGetProperty("openingControlsRevision", out var policy)) {
                if (policy.ValueKind == JsonValueKind.Number && policy.TryGetUInt64(out ulong value) && value > 0) openingRevision = value;
                else openingRevisionValid = false;
                // A policy epoch scopes an observed opening, selection, or selector movement
                openingRevisionValid &= revision.HasValue
                    && input is OverlayInput.Toggle or OverlayInput.Select or OverlayInput.PreviousChoice or OverlayInput.NextChoice
                        or OverlayInput.OpenMain or OverlayInput.OpenQuick;
            }
            // A new mechanical ticket carries all three original visit epochs
            bool mechanical = input is OverlayInput.PreviousChoice or OverlayInput.NextChoice
                or OverlayInput.Back or OverlayInput.Close or OverlayInput.JumpBack;
            ulong? menuRevision = null;
            bool menuRevisionValid = true;
            if (root.TryGetProperty("menuRevision", out var menu)) {
                if (menu.ValueKind == JsonValueKind.Number && menu.TryGetUInt64(out ulong value)) menuRevision = value;
                else menuRevisionValid = false;
                menuRevisionValid &= mechanical && revision.HasValue;
            }
            ulong? localRevision = null;
            bool localRevisionValid = true;
            if (root.TryGetProperty("localAdmissionRevision", out var local)) {
                if (local.ValueKind == JsonValueKind.Number && local.TryGetUInt64(out ulong value) && value > 0) localRevision = value;
                else localRevisionValid = false;
                // Local ownership scopes closed opening and the captured mechanical visit
                localRevisionValid &= revision.HasValue
                    && (input is OverlayInput.OpenMain or OverlayInput.OpenQuick or OverlayInput.JumpBack
                        || mechanical && menuRevision.HasValue);
            }
            bool knownFields = fields.All(p => p.Name is "version" or "sequence" or "processId" or "input" or "navigationRevision" or "openingControlsRevision" or "localAdmissionRevision" or "menuRevision");
            bool scoped = input is OverlayInput.Close or OverlayInput.IncreaseWidth or OverlayInput.DecreaseWidth
                or OverlayInput.IncreaseWidthStep or OverlayInput.DecreaseWidthStep or OverlayInput.FinishWidthEditor or OverlayInput.MoveInstructionsUp or OverlayInput.MoveInstructionsDown
                or OverlayInput.MoveInstructionsLeft or OverlayInput.MoveInstructionsRight or OverlayInput.FinishInstructionPositionEditor
                or OverlayInput.MoveMenuUp or OverlayInput.MoveMenuDown or OverlayInput.MoveMenuLeft or OverlayInput.MoveMenuRight
                or OverlayInput.IncreaseMenuPositionStep or OverlayInput.DecreaseMenuPositionStep or OverlayInput.FinishMenuPositionEditor or OverlayInput.FinishMenuCredits;
            bool opening = input is OverlayInput.OpenMain or OverlayInput.OpenQuick;
            bool complete = (fields.Length is >= 4 and <= 8) && (!menuRevision.HasValue || localRevision.HasValue) && (!scoped || revision.HasValue)
                && (!opening || revision.HasValue && openingRevision.HasValue && localRevision.HasValue)
                && (input != OverlayInput.JumpBack || revision.HasValue && localRevision.HasValue && !openingRevision.HasValue);
            return new(sequence, version, pid, input,
                unique && knownFields && complete && revisionValid && openingRevisionValid && localRevisionValid
                    && menuRevisionValid && input is not null, revision, openingRevision, localRevision, menuRevision);
        } catch (JsonException) {
            return null;
        } catch (InvalidOperationException) {
            return null;
        }
    }

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int result) ? result : 0;
}
