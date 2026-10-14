namespace syndishanx_iw_tool.Overlay.Themes;

// A plan describes one source callback over explicitly observed existing objects
public sealed class ThemeCallbackPlan {
    public IReadOnlyList<ThemeColorCommand> Commands { get; }

    private ThemeCallbackPlan(IEnumerable<ThemeColorCommand> commands) {
        Commands = Array.AsReadOnly(commands.ToArray());
    }

    public static ThemeCallbackPlan Fixed(ThemeCallbackContext context, ThemeRgb target,
        IEnumerable<ObservedThemeWidget> widgets) => Build(context, target, widgets, false);

    // This plans one already admitted Rainbow loop iteration, not its start or timer
    public static ThemeCallbackPlan RainbowSample(ThemeCallbackContext context, ThemeRgb target,
        IEnumerable<ObservedThemeWidget> widgets) => Build(context, target, widgets, true);

    private static ThemeCallbackPlan Build(ThemeCallbackContext context, ThemeRgb target,
        IEnumerable<ObservedThemeWidget> widgets, bool rainbow) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(widgets);
        // Validate the complete supplied membership before returning any command list
        var observed = widgets.ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var members = new HashSet<(ThemeWidgetRole Role, int? Row)>();
        var rowFacts = new Dictionary<int, (bool Boolean, bool On, bool Selected)>();
        foreach (var widget in observed) {
            if (widget is null || !keys.Add(widget.Identity.Key)
                || !members.Add((widget.Identity.Role, widget.RowIndex))) {
                throw new ArgumentException("Observed widgets must have distinct source identities and members", nameof(widgets));
            }
            if (widget.RowIndex is int row) {
                var facts = (widget.IsBooleanOption, widget.BooleanOn, widget.IsSelected);
                if (rowFacts.TryGetValue(row, out var prior) && prior != facts) {
                    throw new ArgumentException("Widgets in one source row must share its captured facts", nameof(widgets));
                }
                rowFacts[row] = facts;
            }
        }
        if (rowFacts.Values.Count(facts => facts.Selected) > 1) {
            throw new ArgumentException("Only one source row can own the cursor", nameof(widgets));
        }

        var commands = new List<ThemeColorCommand>();
        // Explicit source indexes recover traversal order without parsing opaque widget keys
        foreach (var widget in observed.OrderBy(widget => widget.RowIndex.HasValue ? 0 : 1)
            .ThenBy(widget => widget.RowIndex ?? 0).ThenBy(widget => SourceOrder(widget.Identity.Role))) {
            if (!TryTarget(context, target, widget, rainbow, out var color, out int duration)) continue;
            bool direct = rainbow || duration == 0;
            commands.Add(new(widget.Identity, direct ? ThemeColorOperation.DirectColor : ThemeColorOperation.FixedFadeColor,
                color, direct ? 0 : duration));
        }
        return new ThemeCallbackPlan(commands);
    }
    // Source ordering is BOOL interior, invalid marker, then the five row text families
    private static int SourceOrder(ThemeWidgetRole role) => role switch {
        ThemeWidgetRole.RowBooleanInterior => 0,
        ThemeWidgetRole.RowInvalidOption => 1,
        >= ThemeWidgetRole.RowText and <= ThemeWidgetRole.RowStringSlider => 2 + (int)role,
        _ => 20 + (int)role
    };

    private static bool TryTarget(ThemeCallbackContext context, ThemeRgb palette, ObservedThemeWidget widget,
        bool rainbow, out ThemeRgb color, out int duration) {
        color = palette;
        duration = 500;
        var role = widget.Identity.Role;
        if (widget.Identity.Lifetime == ThemeWidgetLifetime.Rows) {
            // Defined row HUD still requires a defined, nonempty cached source structure
            if (!context.HasMenuStructure) return false;
            if (role == ThemeWidgetRole.RowBooleanInterior) return widget.IsBooleanOption && widget.BooleanOn;
            if (role == ThemeWidgetRole.RowInvalidOption) return true;
            if (role is >= ThemeWidgetRole.RowText and <= ThemeWidgetRole.RowStringSlider) {
                duration = 1000;
                color = context.BooleanDisplay == "Text Color" && widget.IsBooleanOption && widget.BooleanOn
                    ? ThemeRgb.GreenOn : widget.IsSelected && context.ColoredCursor ? palette : ThemeRgb.White;
                return true;
            }
            // OFF interiors and gray Boolean backing have no callback assignment
            return false;
        }
        switch (role) {
            case ThemeWidgetRole.Scroller:
                return rainbow ? context.Design != "Apparition" || context.KeyboardStringExists
                    : context.Design is not ("Apparition" or "Basic");
            case ThemeWidgetRole.Banner:
                return context.Design is "Classic" or "Native";
            case ThemeWidgetRole.Title:
                return context.Design is "Apparition" or "Basic";
            case ThemeWidgetRole.Separator:
            case ThemeWidgetRole.BottomLine:
                return context.Design is not ("Classic" or "Native");
            case ThemeWidgetRole.InstructionsBackground:
            case ThemeWidgetRole.PlayerInfoBackground:
                return context.Design == "AIO";
            case ThemeWidgetRole.InstructionsOutline:
            case ThemeWidgetRole.PlayerInfoOutline:
                return context.Design != "AIO";
            case ThemeWidgetRole.ZombieCounter:
            case ThemeWidgetRole.EntityCount:
            case ThemeWidgetRole.CustomCrosshair:
                // The source sends LUI RGB directly even for a fixed theme request
                duration = 0;
                return widget.SourceFeatureEnabled;
            default:
                // Body, counters, backing, secondary titles, and footer text stay unchanged
                return false;
        }
    }
}
