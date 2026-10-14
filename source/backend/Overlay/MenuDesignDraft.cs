namespace syndishanx_iw_tool.Overlay;

// Design candidates retain their source index independently of the applied design
public sealed record MenuDesignDraft(int Index = 0)
{
    private static readonly string[] Names = { "Apparition", "Classic", "Native", "AIO", "Basic" };
    public static bool IsSupportedDesign(string design) => System.Array.IndexOf(Names, design) >= 0;
    public static bool AllowsColoredCursor(string design) => design is not ("Classic" or "Native");
    public static bool AllowsOptionCounter(string design) => design is not ("AIO" or "Basic");
    public static MenuDesignDraft FromAppearance(MenuAppearance appearance) {
        System.ArgumentNullException.ThrowIfNull(appearance);
        int index = System.Array.IndexOf(Names, appearance.Design);
        if (!appearance.IsSupported() || index < 0) throw new System.ArgumentException("Unsupported menu appearance", nameof(appearance));
        // A fresh editor starts at the committed source design, not an older candidate
        return new(index);
    }
    public bool IsSupported() => Index is >= 0 and < 5;
    public string Candidate => IsSupported() ? Names[Index] : throw new System.InvalidOperationException("Unsupported design candidate");

    public bool TryMove(int direction, out MenuDesignDraft updated)
    {
        updated = this;
        if (!IsSupported() || direction is not (-1 or 1)) return false;
        // Only the draft wraps; renderer geometry stays committed until Use
        updated = new((Index + direction + Names.Length) % Names.Length);
        return true;
    }

    public bool TryCommit(MenuAppearance appearance, out MenuAppearance updated)
    {
        updated = appearance;
        if (!IsSupported() || !appearance.IsSupported()) return false;
        // Reselecting the applied source design cannot force or save any flags
        if (Candidate == appearance.Design) return true;
        updated = appearance with
        {
            Design = Candidate,
            ColoredCursor = appearance.ColoredCursor && AllowsColoredCursor(Candidate),
            OptionCounter = appearance.OptionCounter && AllowsOptionCounter(Candidate)
        };
        return true;
    }
}
