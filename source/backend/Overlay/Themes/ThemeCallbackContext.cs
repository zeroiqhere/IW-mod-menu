namespace syndishanx_iw_tool.Overlay.Themes;

// The caller supplies observed handler facts without creating widgets or authorizing actions
public sealed record ThemeCallbackContext {
    public string Design { get; }
    public string BooleanDisplay { get; }
    public bool ColoredCursor { get; }
    // This fact means the source structure is both defined and nonempty
    public bool HasMenuStructure { get; }
    public bool KeyboardStringExists { get; }

    public ThemeCallbackContext(string design, string booleanDisplay = "Boxes", bool coloredCursor = true,
        bool hasMenuStructure = true, bool keyboardStringExists = false) {
        if (design is not ("Apparition" or "Classic" or "Native" or "AIO" or "Basic")) throw new ArgumentException("Unknown source design", nameof(design));
        if (booleanDisplay is not ("Boxes" or "Text" or "Text Color")) throw new ArgumentException("Unknown source boolean display", nameof(booleanDisplay));
        Design = design;
        BooleanDisplay = booleanDisplay;
        ColoredCursor = coloredCursor;
        HasMenuStructure = hasMenuStructure;
        KeyboardStringExists = keyboardStringExists;
    }
}
