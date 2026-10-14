using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignProtocolTests
{
    public static void DesignTransitionAcceptsOnlyExactPhasesAndExclusiveOpenPopulatedChildFrames()
    {
        var menu = new MenuSnapshot("Menu Preferences", "PREVIEW", new[] { new MenuRow("Design", "Classic", true, new(MenuActionKind.ChangePreference), "Use applies") }, "");
        StateDocument Create(string phase = "rebuild", bool open = true, bool root = false, int selected = 0, MenuSnapshot? rows = null,
            LocalWidthEditorState? width = null, InstructionPositionEditor? instructions = null, LocalMenuPositionEditorState? position = null,
            MenuCredits? credits = null, string? unlock = null, MenuPreferenceRefresh? refresh = null) => StateDocument.Create(42, 0, open, rows ?? menu, selected,
                isRootMenu: root, widthEditor: width, instructionPositionEditor: instructions, menuPositionEditor: position,
                menuCredits: credits, menuUnlock: unlock, menuPreferenceRefresh: refresh, menuDesignTransition: new(phase));
        foreach (string phase in new[] { "rebuild", "cooldown" })
        {
            using var json = JsonDocument.Parse(Create(phase).Serialize()); var field = json.RootElement.GetProperty("menuDesignTransition");
            TestAssert.Equal(1, field.EnumerateObject().Count()); TestAssert.Equal(phase, field.GetProperty("phase").GetString());
            TestAssert.Equal("", Create(phase).Notice);
        }
        foreach (string phase in new[] { "", "Rebuild", "pending", "editing" }) TestAssert.Throws<InvalidOperationException>(() => Create(phase));
        TestAssert.Throws<InvalidOperationException>(() => Create(open: false)); TestAssert.Throws<InvalidOperationException>(() => Create(root: true));
        TestAssert.Throws<InvalidOperationException>(() => Create(selected: -1)); TestAssert.Throws<InvalidOperationException>(() => Create(selected: 1));
        TestAssert.Throws<InvalidOperationException>(() => Create(rows: menu with { Rows = Array.Empty<MenuRow>() }));
        TestAssert.Throws<InvalidOperationException>(() => Create(width: new())); TestAssert.Throws<InvalidOperationException>(() => Create(instructions: new("editing")));
        TestAssert.Throws<InvalidOperationException>(() => Create(position: new())); TestAssert.Throws<InvalidOperationException>(() => Create(credits: new(0)));
        TestAssert.Throws<InvalidOperationException>(() => Create(unlock: "pause")); TestAssert.Throws<InvalidOperationException>(() => Create(refresh: new("cooldown")));
        TestAssert.True(StateDocument.Create(42, 0, true, menu, 0).MenuDesignTransition is null, "Legacy frames remain outside this transition");
    }
}
