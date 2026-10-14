using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOpeningCapacityTests {
    public static void SupportedAppearanceCapacityAndVisibleOrdinalDurationMatchEverySourceCombination() {
        foreach (string display in new[] { "Boxes", "Text", "Text Color" })
        foreach (bool instructions in new[] { true, false })
        foreach (bool counter in new[] { true, false }) {
            var appearance = MenuAppearance.Default with { BooleanDisplay = display, InstructionsVisible = instructions, OptionCounter = counter };
            int expected = display == "Boxes" ? instructions ? 10 : 11 : instructions ? 12 : 14;
            if (counter) expected -= 2;
            TestAssert.Equal(expected, MenuOptionCapacity.For(appearance));
            foreach (int rows in new[] { 0, 1, 2, 8, 10, 14, 128 }) {
                int shown = Math.Min(rows, expected), duration = Math.Max(0, shown - 1) * 100;
                TestAssert.Equal(duration, MenuOptionCapacity.OpeningDurationMilliseconds(rows, appearance));
                TestAssert.Equal(duration > 0, new MenuOpening(ulong.MaxValue, 0).IsSupported(rows, appearance));
                if (duration > 0) TestAssert.True(new MenuOpening(1, duration - 1).IsSupported(rows, appearance), "The last millisecond remains active");
                TestAssert.True(!new MenuOpening(1, duration).IsSupported(rows, appearance), "Completion cannot remain advertised as an active fade");
            }
        }
        foreach (string design in new[] { "AIO", "Basic" }) TestAssert.Throws<ArgumentException>(() => MenuOptionCapacity.For(MenuAppearance.Default with { Design = design, OptionCounter = true }));
        foreach (int rows in new[] { -1, 129, int.MaxValue }) TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuOptionCapacity.OpeningDurationMilliseconds(rows, MenuAppearance.Default));
    }
}
