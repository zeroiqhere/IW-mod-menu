namespace syndishanx_iw_tool.Overlay.Themes;

// A command owns normalized RGB only, without opacity or a prior presented color
public readonly record struct ThemeRgb {
    public double Red { get; }
    public double Green { get; }
    public double Blue { get; }

    public ThemeRgb(double red, double green, double blue) {
        // Nonfinite values cannot enter a later renderer or native color command
        if (!Valid(red) || !Valid(green) || !Valid(blue)) throw new ArgumentOutOfRangeException(nameof(red), "RGB channels must be finite and normalized");
        Red = red;
        Green = green;
        Blue = blue;
    }

    private static bool Valid(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
    public static ThemeRgb White => new(1, 1, 1);
    public static ThemeRgb GreenOn => new(0, 1, 0);
}
