using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOpeningProtocolTests {
    public static void PositiveRevisionAndActualDurationRequireOrdinaryOpenFramesAndCompatibleTransitions() {
        var menu = new MenuSnapshot("Apparition", "PREVIEW", Enumerable.Range(0, 20).Select(index => new MenuRow("Row " + index, "", true, new(MenuActionKind.None), "")).ToArray(), "");
        StateDocument Create(MenuOpening? opening = null, bool open = true, bool root = true, MenuAppearance? appearance = null,
            int selected = 0, MenuSnapshot? rows = null, MenuDesignTransition? design = null,
            LocalWidthEditorState? width = null, InstructionPositionEditor? instructions = null, LocalMenuPositionEditorState? position = null,
            MenuCredits? credits = null, string? unlock = null, MenuPreferenceRefresh? refresh = null) => StateDocument.Create(42, 0, open, rows ?? menu, selected,
                appearance: appearance, isRootMenu: root, widthEditor: width, instructionPositionEditor: instructions, menuPositionEditor: position,
                menuCredits: credits, menuUnlock: unlock, menuPreferenceRefresh: refresh, menuDesignTransition: design, menuOpening: opening ?? new(1, 0));
        foreach (bool root in new[] { true, false }) {
            // Missing appearance has the same default interpretation as the native legacy parser
            var state = Create(new(ulong.MaxValue, 899), root: root);
            using var json = JsonDocument.Parse(state.Serialize()); var opening = json.RootElement.GetProperty("menuOpening");
            TestAssert.Equal(2, opening.EnumerateObject().Count()); TestAssert.Equal(ulong.MaxValue, opening.GetProperty("revision").GetUInt64());
            TestAssert.Equal(899, opening.GetProperty("elapsedMilliseconds").GetInt32());
        }
        var widest = MenuAppearance.Default with { BooleanDisplay = "Text", InstructionsVisible = false };
        Create(new(1, 1299), appearance: widest);
        foreach (var invalid in new[] { new MenuOpening(0, 0), new MenuOpening(1, -1), new MenuOpening(1, 900), new MenuOpening(1, int.MaxValue) }) TestAssert.Throws<InvalidOperationException>(() => Create(invalid));
        TestAssert.Throws<InvalidOperationException>(() => Create(new(1, 1300), appearance: widest));
        TestAssert.Throws<InvalidOperationException>(() => Create(open: false)); TestAssert.Throws<InvalidOperationException>(() => Create(selected: -1)); TestAssert.Throws<InvalidOperationException>(() => Create(selected: 20));
        foreach (int count in new[] { 0, 1 }) TestAssert.Throws<InvalidOperationException>(() => Create(rows: menu with { Rows = menu.Rows.Take(count).ToArray() }));
        Create(root: false, design: new("cooldown")); TestAssert.Throws<InvalidOperationException>(() => Create(root: false, design: new("rebuild")));
        TestAssert.Throws<InvalidOperationException>(() => Create(width: new())); TestAssert.Throws<InvalidOperationException>(() => Create(instructions: new("editing")));
        TestAssert.Throws<InvalidOperationException>(() => Create(position: new())); TestAssert.Throws<InvalidOperationException>(() => Create(credits: new(0))); TestAssert.Throws<InvalidOperationException>(() => Create(unlock: "pause"));
        foreach (string phase in new[] { "pending", "cooldown" }) TestAssert.Throws<InvalidOperationException>(() => Create(refresh: new(phase)));
        var owner = Create(appearance: MenuAppearance.Default); var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(hidden.MenuOpening is null && !hidden.IsOpen && owner.MenuOpening is not null, "Projection changes only the display frame"); TestAssert.Equal(owner.Appearance, hidden.Appearance);
    }
}
