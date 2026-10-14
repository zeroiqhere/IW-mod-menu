using System.Text.Json;
using syndishanx_iw_tool.Overlay.Themes;

namespace Backend.Tests.Overlay.Themes;

internal static class ThemeCallbackPlanTests {
    private static readonly ThemeRgb Palette = new(0.2, 0.4, 0.8);

    public static void FixedCommandsMatchHandAuditedSourceMasksAndDurations() => CheckOracle("Fixed");
    public static void RainbowSamplesMatchHandAuditedDirectColorMasks() => CheckOracle("RainbowSample");

    private static void CheckOracle(string mode) {
        using var oracle = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "theme-callback-source-oracle.json")));
        TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", oracle.RootElement.GetProperty("pin").GetString());
        var samples = oracle.RootElement.GetProperty("cases").EnumerateArray()
            .Where(sample => sample.GetProperty("mode").GetString() == mode).ToArray();
        var expectedDesigns = new HashSet<string>(new[] { "Apparition", "Classic", "Native", "AIO", "Basic" }, StringComparer.Ordinal);
        var observedDesigns = new HashSet<string>(StringComparer.Ordinal);
        TestAssert.Equal(expectedDesigns.Count, samples.Length);

        foreach (var sample in samples) {
            string design = sample.GetProperty("design").GetString()!;
            TestAssert.True(expectedDesigns.Contains(design) && observedDesigns.Add(design),
                "Each source mode must cover every expected design exactly once");
            var context = new ThemeCallbackContext(sample.GetProperty("design").GetString()!);
            var widgets = Enum.GetValues<ThemeWidgetRole>().Select(role => Widget(role, true, true, true, true)).Reverse().ToArray();
            var plan = mode == "Fixed" ? ThemeCallbackPlan.Fixed(context, Palette, widgets)
                : ThemeCallbackPlan.RainbowSample(context, Palette, widgets);
            var expected = sample.GetProperty("commands").EnumerateArray().ToArray();

            TestAssert.Equal(expected.Length, plan.Commands.Count);
            for (int index = 0; index < expected.Length; index++) {
                var command = plan.Commands[index];
                // The oracle's provenance and target annotation are part of its trusted contract
                TestAssert.Equal("palette", expected[index].GetProperty("target").GetString());
                TestAssert.True(!string.IsNullOrWhiteSpace(expected[index].GetProperty("source").GetString()),
                    "Each expected command must retain a source anchor");
                TestAssert.Equal(expected[index].GetProperty("role").GetString(), command.Widget.Role.ToString());
                TestAssert.Equal(expected[index].GetProperty("lifetime").GetString(), command.Widget.Lifetime.ToString());
                TestAssert.Equal(expected[index].GetProperty("operation").GetString(), command.Operation.ToString());
                TestAssert.Equal(expected[index].GetProperty("durationMilliseconds").GetInt32(), command.DurationMilliseconds);
                TestAssert.Equal(Palette, command.Target);
            }
        }
    }

    public static void CapturedRowBooleansAndSelectionChooseExactSourceTargets() {
        var widgets = new[] {
            Widget(ThemeWidgetRole.RowText, true, true, false),
            Widget(ThemeWidgetRole.RowBooleanText, true, false, true, row: 1),
            Widget(ThemeWidgetRole.RowSubmenu, false, true, false, row: 2),
            Widget(ThemeWidgetRole.RowBooleanInterior, true, false, row: 3)
        };
        var plan = ThemeCallbackPlan.Fixed(new("Native", "Text Color"), Palette, widgets);

        TestAssert.Equal(3, plan.Commands.Count);
        TestAssert.Equal(ThemeRgb.GreenOn, plan.Commands[0].Target);
        TestAssert.Equal(Palette, plan.Commands[1].Target);
        TestAssert.Equal(ThemeRgb.White, plan.Commands[2].Target);
        var uncolored = ThemeCallbackPlan.Fixed(new("Apparition", coloredCursor: false), Palette,
            new[] { Widget(ThemeWidgetRole.RowText, false, false, true) });
        TestAssert.Equal(ThemeRgb.White, uncolored.Commands.Single().Target);
    }

    public static void MissingStructureAndDisabledLuiLeaveObservedObjectsUntouched() {
        var widgets = new[] { Widget(ThemeWidgetRole.RowText), Widget(ThemeWidgetRole.RowInvalidOption),
            Widget(ThemeWidgetRole.ZombieCounter), Widget(ThemeWidgetRole.EntityCount), Widget(ThemeWidgetRole.CustomCrosshair) };
        var context = new ThemeCallbackContext("Apparition", hasMenuStructure: false);

        TestAssert.Equal(0, ThemeCallbackPlan.Fixed(context, Palette, widgets).Commands.Count);
        TestAssert.Equal(0, ThemeCallbackPlan.RainbowSample(context, Palette, widgets).Commands.Count);
        TestAssert.Equal(0, ThemeCallbackPlan.Fixed(new("AIO"), Palette, Array.Empty<ObservedThemeWidget>()).Commands.Count);
    }

    public static void InputOrderingCannotChangeSourceRowAndChromeCommandOrder() {
        var row2 = Widget(ThemeWidgetRole.RowText, selected: true, row: 2);
        var row1 = Widget(ThemeWidgetRole.RowText, true, true, selected: false, row: 1);
        var check1 = Widget(ThemeWidgetRole.RowBooleanInterior, true, true, row: 1);
        var widgets = new[] { Widget(ThemeWidgetRole.Title), row2, row1, check1, Widget(ThemeWidgetRole.InstructionsOutline) };
        var plan = ThemeCallbackPlan.Fixed(new("Apparition"), Palette, widgets);

        TestAssert.Equal(check1.Identity, plan.Commands[0].Widget);
        TestAssert.Equal(row1.Identity, plan.Commands[1].Widget);
        TestAssert.Equal(row2.Identity, plan.Commands[2].Widget);
        TestAssert.Equal(ThemeWidgetRole.Title, plan.Commands[3].Widget.Role);
        TestAssert.Equal(ThemeWidgetRole.InstructionsOutline, plan.Commands[4].Widget.Role);
    }

    public static void PlansOwnReadOnlyCopiesAndEqualFixedSelectionsRemainFullRequests() {
        var widgets = new List<ObservedThemeWidget> { Widget(ThemeWidgetRole.Title), Widget(ThemeWidgetRole.InstructionsOutline) };
        var first = ThemeCallbackPlan.Fixed(new("Apparition"), Palette, widgets);
        var repeated = ThemeCallbackPlan.Fixed(new("Apparition"), Palette, widgets);
        widgets.Clear();

        TestAssert.Equal(2, first.Commands.Count);
        TestAssert.True(first.Commands.SequenceEqual(repeated.Commands), "Equal source selections must keep all callback requests");
        TestAssert.Throws<NotSupportedException>(() => ((IList<ThemeColorCommand>)first.Commands).Clear());
    }

    public static void MalformedColorsContextsAndObservedAliasesRejectWithoutPartialPlans() {
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.1, 1.1 }) {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ThemeRgb(invalid, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ThemeRgb(0, invalid, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ThemeRgb(0, 0, invalid));
        }
        // Both closed interval endpoints remain valid in every channel
        TestAssert.Equal(default(ThemeRgb), new ThemeRgb(0, 0, 0));
        TestAssert.Equal(ThemeRgb.White, new ThemeRgb(1, 1, 1));
        TestAssert.Equal(new ThemeRgb(0, 1, 0), ThemeRgb.GreenOn);
        TestAssert.Throws<ArgumentException>(() => new ThemeCallbackContext("Aio"));
        TestAssert.Throws<ArgumentException>(() => new ThemeCallbackContext("Basic", "Unknown"));
        TestAssert.Throws<ArgumentException>(() => new ThemeWidgetIdentity("", ThemeWidgetRole.Title, ThemeWidgetLifetime.MenuChrome));
        TestAssert.Throws<ArgumentException>(() => new ThemeWidgetIdentity("x", ThemeWidgetRole.Title, ThemeWidgetLifetime.Rows));
        TestAssert.Throws<ArgumentException>(() => new ObservedThemeWidget(Widget(ThemeWidgetRole.RowText).Identity));
        var widget = Widget(ThemeWidgetRole.Title);
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, new[] { widget, widget }));
        var alias = new ObservedThemeWidget(new(widget.Identity.Key, ThemeWidgetRole.Body, ThemeWidgetLifetime.MenuChrome));
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, new[] { widget, alias }));
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, new ObservedThemeWidget[] { widget, null! }));
        TestAssert.Throws<ArgumentNullException>(() => ThemeCallbackPlan.Fixed(null!, Palette, Array.Empty<ObservedThemeWidget>()));
        TestAssert.Throws<ArgumentNullException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, null!));
        TestAssert.Throws<ArgumentNullException>(() => ThemeCallbackPlan.RainbowSample(new("Apparition"), Palette, null!));
        TestAssert.Throws<ArgumentException>(() => new ThemeWidgetIdentity("unknown", (ThemeWidgetRole)999, ThemeWidgetLifetime.Rows));
        TestAssert.Throws<ArgumentException>(() => new ObservedThemeWidget(Widget(ThemeWidgetRole.Title).Identity, rowIndex: 0));
        TestAssert.Throws<ArgumentException>(() => new ObservedThemeWidget(Widget(ThemeWidgetRole.RowText).Identity, rowIndex: -1));
        var duplicateMember = new ObservedThemeWidget(new("other-title", ThemeWidgetRole.Title, ThemeWidgetLifetime.MenuChrome));
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, new[] { widget, duplicateMember }));
        var conflictingRow = new[] { Widget(ThemeWidgetRole.RowText, true, true), Widget(ThemeWidgetRole.RowBooleanText, true, false) };
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, conflictingRow));
        var twoCursors = new[] { Widget(ThemeWidgetRole.RowText, selected: true), Widget(ThemeWidgetRole.RowText, selected: true, row: 1) };
        TestAssert.Throws<ArgumentException>(() => ThemeCallbackPlan.RainbowSample(new("Apparition"), Palette, twoCursors));
        // Failure after an earlier valid widget cannot return a partial operation plan
        TestAssert.Throws<InvalidOperationException>(() => ThemeCallbackPlan.Fixed(new("Apparition"), Palette, ThrowAfter(widget)));
    }

    public static void IndependentLifetimesAndBasicKeyboardExceptionsPreserveUntargetedRoles() {
        var widgets = new[] { Widget(ThemeWidgetRole.Scroller), Widget(ThemeWidgetRole.InstructionsBackground),
            Widget(ThemeWidgetRole.InstructionsOutline), Widget(ThemeWidgetRole.RowBooleanBackground), Widget(ThemeWidgetRole.RowInvalidOption) };
        var fixedBasic = ThemeCallbackPlan.Fixed(new("Basic"), Palette, widgets);
        var rainbowBasic = ThemeCallbackPlan.RainbowSample(new("Basic"), Palette, widgets);
        var keyboard = ThemeCallbackPlan.RainbowSample(new("Apparition", keyboardStringExists: true), Palette, widgets);
        var aio = ThemeCallbackPlan.Fixed(new("AIO"), Palette, widgets);

        TestAssert.True(!fixedBasic.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.Scroller), "Fixed Basic must leave its observed scroller color unchanged");
        TestAssert.True(rainbowBasic.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.Scroller), "Rainbow updates the existing Basic scroller");
        TestAssert.True(keyboard.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.Scroller), "The source keyboard exception updates Apparition scroller");
        TestAssert.True(aio.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.InstructionsBackground), "AIO recolors the existing background");
        TestAssert.True(!aio.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.InstructionsOutline), "AIO outline keeps its existing creation color");
        TestAssert.True(aio.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.RowInvalidOption), "Source invalid-option decoration has its own callback");
        TestAssert.True(!aio.Commands.Any(command => command.Widget.Role == ThemeWidgetRole.RowBooleanBackground), "Boolean backing has no color callback");
    }

    private static IEnumerable<ObservedThemeWidget> ThrowAfter(ObservedThemeWidget widget) {
        yield return widget;
        throw new InvalidOperationException("Owned observation enumeration failed");
    }

    private static ObservedThemeWidget Widget(ThemeWidgetRole role, bool boolean = false, bool on = false,
        bool selected = false, bool enabled = false, int row = 0) {
        ThemeWidgetLifetime lifetime = role <= ThemeWidgetRole.RowInvalidOption ? ThemeWidgetLifetime.Rows
            : role <= ThemeWidgetRole.Counter ? ThemeWidgetLifetime.MenuChrome
            : role <= ThemeWidgetRole.InstructionsText ? ThemeWidgetLifetime.Instructions
            : role <= ThemeWidgetRole.PlayerInfoText ? ThemeWidgetLifetime.PlayerInfo : ThemeWidgetLifetime.ExternalLui;
        return new(new($"{role}:{row}", role, lifetime), boolean, on, selected, enabled,
            lifetime == ThemeWidgetLifetime.Rows ? row : null);
    }
}
