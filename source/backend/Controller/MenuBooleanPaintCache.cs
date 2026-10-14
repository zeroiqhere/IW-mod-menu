using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Cached indicators describe the last menu redraw, never feature authority
internal sealed class MenuBooleanPaintCache {
    private const int MaximumRows = 128;
    private readonly RowTopology[] topology;

    private MenuBooleanPaintCache(RowTopology[] topology, MenuBooleanPaint frame) {
        this.topology = topology;
        Frame = frame;
    }

    public MenuBooleanPaint Frame { get; }

    public static MenuBooleanPaintCache? Capture(ulong revision, IReadOnlyList<MenuRow> rows, bool includePalette = false, bool includeInstructions = false) {
        ArgumentOutOfRangeException.ThrowIfZero(revision);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaximumRows) {
            throw new ArgumentException("Menu paint supports at most 128 rows", nameof(rows));
        }

        // Every row participates in topology, including rows without a cached indicator
        var copiedTopology = new RowTopology[rows.Count];
        var paint = new List<MenuBooleanPaintRow>();
        for (int index = 0; index < copiedTopology.Length; index++) {
            var row = rows[index];
            if (!HasSupportedShape(row)) {
                throw new ArgumentException("Menu paint requires supported row identities", nameof(rows));
            }
            copiedTopology[index] = RowTopology.From(row);
            if (!IsEligible(row, includePalette, includeInstructions)) continue;

            // Only the caller's palette page may opt these local source booleans into paint
            if (IsPaletteAction(row.Action.Kind) && !HasSupportedPaletteAction(row.Action)) {
                throw new ArgumentException("Palette paint requires a supported color action", nameof(rows));
            }

            // Unknown remains explicit rather than being painted as a confirmed OFF
            if (row.Value is not ("ON" or "OFF" or "UNKNOWN")) {
                throw new ArgumentException("Boolean paint requires ON, OFF, or UNKNOWN", nameof(rows));
            }
            paint.Add(new MenuBooleanPaintRow(index, row.Value));
        }

        if (paint.Count == 0) return null;

        // Neither the public frame nor the private topology retains a mutable caller list
        var frame = new MenuBooleanPaint(revision, Array.AsReadOnly(paint.ToArray()));
        return new MenuBooleanPaintCache(copiedTopology, frame);
    }

    public bool MatchesTopology(IReadOnlyList<MenuRow> rows) {
        if (rows is null || rows.Count != topology.Length) return false;
        for (int index = 0; index < topology.Length; index++) {
            var row = rows[index];
            if (!HasSupportedShape(row) || topology[index] != RowTopology.From(row)) return false;
        }
        return true;
    }

    private static bool IsEligible(MenuRow row, bool includePalette, bool includeInstructions) => !row.IsSpacer
        && (row.Action.Kind is MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
            or MenuActionKind.ToggleTeammateGodmode or MenuActionKind.ToggleAllPlayersGodmode || includePalette && IsPaletteAction(row.Action.Kind)
            || includeInstructions && IsInstructionAction(row.Action));

    // Copy the caller's raw Disable inversions without reading appearance or granting handlers
    private static bool IsInstructionAction(MenuAction action) => action.Kind == MenuActionKind.ChangePreference
        && action.Slot is (int)MenuPreference.InstructionsVisible or (int)MenuPreference.AlternateInstructions
            or (int)MenuPreference.InstructionsBackground or (int)MenuPreference.AdaptiveInstructions;

    private static bool IsPaletteAction(MenuActionKind kind) => kind is
        MenuActionKind.ChangeAccent or MenuActionKind.EnableSmoothRainbowTheme;

    // This bounds the copied action shape without applying a color or authorizing a callback
    private static bool HasSupportedPaletteAction(MenuAction action) => action.Kind == MenuActionKind.ChangeAccent
        ? MenuThemes.Find(action.Slot) is not null : action.Slot == -1;

    // This checks model shape only; target authentication remains with the action owner
    private static bool HasSupportedShape(MenuRow? row) => row is not null
        && row.Label is not null && row.Value is not null && row.Action is not null
        && Enum.IsDefined(row.Action.Kind) && Enum.IsDefined(row.Action.Page)
        && (!row.OpeningAction.HasValue || Enum.IsDefined(row.OpeningAction.Value));

    // Live values, busy eligibility, and help text do not change the source row identity
    private readonly record struct RowTopology(string Label, MenuAction Action, bool IsSpacer,
        OpeningAction? OpeningAction, MenuSourceChoiceRole SourceChoiceRole) {
        public static RowTopology From(MenuRow row) =>
            new(row.Label, row.Action, row.IsSpacer, row.OpeningAction, row.SourceChoiceRole);
    }
}
