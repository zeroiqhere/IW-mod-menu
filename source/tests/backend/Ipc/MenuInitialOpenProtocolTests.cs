using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuInitialOpenProtocolTests
{
    public static void OpeningTransitionSupportsOrdinaryRootAndChildEmptyMenusAndRejectsIncompatibleMetadata()
    {
        var rows = new[] { new MenuRow("One", "", true, new(MenuActionKind.None), "Selected action") };
        var menu = new MenuSnapshot("Apparition", "PREVIEW", rows, "");
        StateDocument Create(string phase = "rebuild", bool open = true, bool root = true, int selected = 0,
            MenuSnapshot? snapshot = null, MenuOpening? opening = null, string? unlock = null,
            LocalWidthEditorState? width = null, InstructionPositionEditor? instructions = null,
            LocalMenuPositionEditorState? position = null, MenuCredits? credits = null,
            MenuPreferenceRefresh? refresh = null, MenuDesignTransition? design = null) =>
            StateDocument.Create(42, 0, open, snapshot ?? menu, selected, isRootMenu: root, menuOpening: opening,
                menuUnlock: unlock, widthEditor: width, instructionPositionEditor: instructions,
                menuPositionEditor: position, menuCredits: credits, menuPreferenceRefresh: refresh,
                menuDesignTransition: design, menuOpenTransition: new(phase));
        foreach (bool root in new[] { true, false })
        {
            foreach (string phase in new[] { "rebuild", "cooldown" })
            {
                Create(phase, root: root);
                Create(phase, root: root, snapshot: menu with
                {
                    Rows = Array.Empty<MenuRow>()
                });
            }
        }
        foreach (string phase in new[] { "", "ready", "Rebuild", "pause" })
            TestAssert.Throws<InvalidOperationException>(() => Create(phase));
        TestAssert.Throws<InvalidOperationException>(() => Create(open: false));
        TestAssert.Throws<InvalidOperationException>(() => Create(selected: -1));
        TestAssert.Throws<InvalidOperationException>(() => Create(selected: 1));
        TestAssert.Throws<InvalidOperationException>(() => Create(selected: 1, snapshot: menu with { Rows = Array.Empty<MenuRow>() }));
        var pair = menu with
        {
            Rows = new[] { rows[0], rows[0] }
        };
        Create("cooldown", snapshot: pair, opening: new(1, 0));
        TestAssert.Throws<InvalidOperationException>(() => Create(snapshot: pair, opening: new(1, 0)));
        TestAssert.Throws<InvalidOperationException>(() => Create(unlock: "pause"));
        TestAssert.Throws<InvalidOperationException>(() => Create(width: new()));
        TestAssert.Throws<InvalidOperationException>(() => Create(instructions: new("editing")));
        TestAssert.Throws<InvalidOperationException>(() => Create(position: new()));
        TestAssert.Throws<InvalidOperationException>(() => Create(credits: new(0)));
        TestAssert.Throws<InvalidOperationException>(() => Create(refresh: new("pending")));
        TestAssert.Throws<InvalidOperationException>(() => Create(design: new("cooldown")));
        var owner = Create();
        TestAssert.True(!owner.Notice.Contains("Selected action", StringComparison.Ordinal), "Paused input cannot advertise a selected-row action");
        using var json = JsonDocument.Parse(owner.Serialize());
        TestAssert.Equal(1, json.RootElement.GetProperty("menuOpenTransition").EnumerateObject().Count());
        var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(!hidden.IsOpen && hidden.MenuOpenTransition is null && owner.MenuOpenTransition is not null,
            "Focus projection hides only display metadata while retaining owner state");
    }
}
