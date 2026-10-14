namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private void ValidateBooleanPaint() {
        if (MenuBooleanPaint is not { } paint) return;
        // Display samples require the same visible ordinary context as native rendering
        if (!paint.IsSupported() || !IsOpen || MenuRevision is null || Rows is null || Rows.Count is < 1 or > 128
            || SelectedIndex < 0 || SelectedIndex >= Rows.Count || Rows[SelectedIndex].IsSpacer
            || WidthEditor is not null || InstructionPositionEditor is not null || MenuPositionEditor is not null
            || MenuCredits is not null || MenuUnlock is not null || MenuPreferenceRefresh is not null
            || MenuDesignTransition is not null || MenuPreferenceRebuild is not null
            || MenuOpenTransition is { Phase: "rebuild" }) {
            throw new InvalidOperationException("Boolean paint requires an open populated ordinary menu context");
        }
        foreach (var row in paint.Rows) {
            // Cached paint cannot turn an unavailable option into a boolean or an action
            if (row.Index >= Rows.Count || Rows[row.Index].IsSpacer
                || Rows[row.Index].Value is not ("ON" or "OFF" or "UNKNOWN")) {
                throw new InvalidOperationException("Boolean paint requires existing nonspacer boolean targets");
            }
        }
    }

    private string BooleanPaintNotice() {
        if (MenuBooleanPaint is null) return "";
        var current = Rows[SelectedIndex];
        // Native rendering always keeps a missing current observation UNKNOWN
        if (current.Value == "UNKNOWN") return "";
        foreach (var row in MenuBooleanPaint.Rows) {
            if (row.Index == SelectedIndex && row.Value != current.Value) {
                return $"Current {current.Label}: {current.Value}. Menu value is from the last refresh";
            }
        }
        return "";
    }
}
