using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StealthHelpTests {
    public static void SelectedStealthExplainsItsFiveRowsForcedSettingsAndNonRestoringDisable() {
        const string expected = "Show five rows; replace Boxes with Text and clear the counter. Turning this off keeps those settings";
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Highlight("Stealth UI");
        TestAssert.True(rig.Current().Notice.Contains(expected, StringComparison.Ordinal), "Help must explain the actual setting changes before selection");
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        var enabled = rig.Current();
        TestAssert.True(enabled.Notice.Contains(expected, StringComparison.Ordinal), "Help must also explain that disabling does not restore settings");
        TestAssert.True(enabled.Appearance!.StealthUI && enabled.Appearance.BooleanDisplay == "Text" && !enabled.Appearance.OptionCounter,
            "The help must match the committed source behavior");
        rig.NoGameWork();
    }
}
