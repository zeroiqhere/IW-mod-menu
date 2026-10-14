using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StealthAppearanceTests {
    public static void StealthAppendsItsStableIdAndForcesOnlyItsIncompatibleSettings() {
        TestAssert.Equal(12, (int)MenuPreference.Design);
        TestAssert.Equal(13, (int)MenuPreference.StealthUI);
        TestAssert.True(!MenuAppearance.Default.StealthUI, "Stealth starts off");
        foreach (string display in new[] { "Boxes", "Text", "Text Color" }) {
            var original = MenuAppearance.Default with { BooleanDisplay = display, OptionCounter = true, LargeCursor = true };
            TestAssert.True(original.TryChange(13, out var enabled), "A valid appearance can enable Stealth");
            TestAssert.Equal(original with { StealthUI = true, OptionCounter = false,
                BooleanDisplay = display == "Boxes" ? "Text" : display }, enabled);
            TestAssert.True(enabled.TryChange(13, out var disabled), "Stealth can be disabled");
            TestAssert.Equal(enabled with { StealthUI = false }, disabled);
            TestAssert.True(!enabled.TryChange((int)MenuPreference.OptionCounter, out var rejected), "A direct Counter action cannot bypass Stealth");
            TestAssert.Equal(enabled, rejected);
        }
        foreach (var invalid in new[] { MenuAppearance.Default with { StealthUI = true },
            MenuAppearance.Default with { StealthUI = true, BooleanDisplay = "Text", OptionCounter = true } }) {
            TestAssert.True(!invalid.IsSupported(), "Incompatible Stealth frames must reject");
        }
    }

    public static void StealthCapacityIsAlwaysFiveBeforeOtherFlagsAndKeepsTheAbsoluteCursor() {
        foreach (string design in MenuDesignTests.Names)
        foreach (string display in new[] { "Text", "Text Color" })
        foreach (bool visible in new[] { true, false }) {
            var appearance = MenuAppearance.Default with { Design = design, BooleanDisplay = display,
                InstructionsVisible = visible, ColoredCursor = MenuDesignDraft.AllowsColoredCursor(design), StealthUI = true };
            TestAssert.Equal(5, MenuOptionCapacity.For(appearance));
            TestAssert.Equal(400, MenuOptionCapacity.OpeningDurationMilliseconds(12, appearance));
        }
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Select("Stealth UI");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        var state = rig.Current();
        TestAssert.Equal(10, state.SelectedIndex);
        TestAssert.Equal("Stealth UI", state.Rows[state.SelectedIndex].Label);
        TestAssert.Equal(12, state.Rows.Count);
        rig.NoGameWork();
    }

    public static void ForcedTextRetainsTheBoxesDraftRejectsItsCommitAndAcceptsBothTextModes() {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Select("Stealth UI");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        rig.Highlight("Bool Display");
        var before = rig.Current();
        TestAssert.Equal("Boxes", before.Rows[1].Value);
        TestAssert.Equal("Text", before.Appearance!.BooleanDisplay);
        rig.Send(OverlayInput.Select);
        var rejected = rig.Current();
        TestAssert.Equal(before.Appearance, rejected.Appearance);
        TestAssert.Equal(before.MenuRevision, rejected.MenuRevision);
        TestAssert.True(rejected.Notice.Contains("Boxes cannot be applied", StringComparison.Ordinal), "Rejected Use must explain the current restriction");
        foreach (string display in new[] { "Text", "Text Color" }) {
            rig.Send(OverlayInput.NextChoice);
            rig.Send(OverlayInput.Select);
            TestAssert.Equal(display, rig.Current().Appearance!.BooleanDisplay);
            TestAssert.True(!rig.Current().Notice.Contains("Boxes cannot be applied", StringComparison.Ordinal), "A confirmed Text commit cannot retain the earlier rejection feedback");
        }
        rig.Select("Option Counter");
        TestAssert.True(!rig.Current().Appearance!.OptionCounter, "A disabled Counter selection cannot change state");
        rig.NoGameWork();
    }
}
