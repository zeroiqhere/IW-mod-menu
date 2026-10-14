namespace Apparition.Backend.Ipc;

// These copied menu values describe paint, never current feature state or permission
public sealed record MenuBooleanPaint(ulong Revision, IReadOnlyList<MenuBooleanPaintRow> Rows) {
    public bool IsSupported() {
        if (Revision == 0 || Rows is null || Rows.Count is < 1 or > 128) return false;
        int previous = -1;
        foreach (var row in Rows) {
            if (row is null || row.Index <= previous || row.Index >= 128
                || row.Value is not ("ON" or "OFF" or "UNKNOWN")) return false;
            previous = row.Index;
        }
        return true;
    }
}

public sealed record MenuBooleanPaintRow(int Index, string Value);
