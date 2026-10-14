namespace syndishanx_iw_tool.Overlay;

// Source Disable labels show the inverse of the stored visibility and background fields
internal static class MenuInstructionRows {
    public static IReadOnlyList<MenuRow> Build(MenuAppearance appearance, bool positionControlsAvailable) => Array.AsReadOnly(new[] {
        Toggle("Disable", !appearance.InstructionsVisible, MenuPreference.InstructionsVisible,
            "Use toggles instruction visibility; menu options remain available"),
        Toggle("Alternate Style", appearance.AlternateInstructions, MenuPreference.AlternateInstructions,
            "Use toggles Alternate Style and clears Adaptive Position"),
        Toggle("Disable Background", !appearance.InstructionsBackground, MenuPreference.InstructionsBackground,
            "Use toggles the instruction background"),
        new MenuRow("Adaptive Position", appearance.AdaptiveInstructions ? "ON" : "OFF", true,
            new MenuAction(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.AdaptiveInstructions),
            appearance.AlternateInstructions ? "Use reports that Adaptive Position can't be used while Alternate Style is ON"
                : "Use toggles instructions following the menu position"),
        new MenuRow("Reposition", appearance.InstructionsVisible && positionControlsAvailable ? ">" : "UNAVAILABLE",
            appearance.InstructionsVisible && positionControlsAvailable,
            new MenuAction(MenuActionKind.OpenPage, MenuPage.InstructionPositionEditor),
            !appearance.InstructionsVisible ? "Enable Menu Instructions before repositioning them"
                : !positionControlsAvailable ? "Position controls unavailable. Save +actionslot 1, +actionslot 2, +actionslot 3 and +actionslot 4 bindings in IW controls, then restart the menu"
                : "Select to reposition instructions; saved action slots move them and melee returns here"),
        new MenuRow("Reset Position", "", true, new MenuAction(MenuActionKind.ResetInstructionPosition),
            "Use restores the default instruction position for the current style")
    });

    private static MenuRow Toggle(string label, bool enabled, MenuPreference preference, string detail) =>
        new(label, enabled ? "ON" : "OFF", true,
            new MenuAction(MenuActionKind.ChangePreference, Slot: (int)preference), detail);

}
