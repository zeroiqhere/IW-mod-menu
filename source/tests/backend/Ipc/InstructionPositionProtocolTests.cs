using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using System.Text;
using System.Text.Json;

namespace Backend.Tests;

internal static class InstructionPositionProtocolTests {
    public static void MovementCommandsUseExactNamesAndRequireOneUnsignedObservedNavigationRevision() {
        foreach (string input in new[] { "moveInstructionsUp", "moveInstructionsDown", "moveInstructionsLeft",
            "moveInstructionsRight", "finishInstructionPositionEditor" }) {
            CommandEnvelope? Parse(string suffix) => CommandEnvelope.Parse(Encoding.UTF8.GetBytes(
                "{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"" + input + "\"" + suffix + "}"));
            TestAssert.True(Parse(",\"navigationRevision\":18446744073709551615") is { Valid: true, NavigationRevision: ulong.MaxValue },
                "A complete exact editor command must accept its unsigned epoch");
            foreach (string suffix in new[] { "", ",\"navigationRevision\":-1", ",\"navigationRevision\":true",
                ",\"navigationRevision\":\"1\"", ",\"navigationRevision\":1.5", ",\"navigationRevision\":1,\"navigationRevision\":2" }) {
                TestAssert.True(Parse(suffix) is { Valid: false, Sequence: 1 }, "Malformed scoped commands must remain acknowledged but unusable");
            }
        }
    }
    public static void EditorStateRequiresVisibleOpenNonRootEmptyRowsAndExclusiveSupportedMetadata() {
        var visit = InstructionPositionEditorTests.Open(); visit.Enter(); var valid = visit.Controller.Snapshot();
        using var json = JsonDocument.Parse(valid.Serialize());
        TestAssert.Equal("entering", json.RootElement.GetProperty("instructionPositionEditor").GetProperty("phase").GetString());
        var empty = new MenuSnapshot("Menu Instructions", "PREVIEW", Array.Empty<MenuRow>(), "");
        void Reject(bool open = true, bool root = false, MenuAppearance? appearance = null,
            LocalWidthEditorState? width = null, string? unlock = null, string phase = "editing") =>
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, open, empty, 0,
                appearance: appearance ?? MenuAppearance.Default, isRootMenu: root, widthEditor: width,
                menuUnlock: unlock, instructionPositionEditor: new(phase)));
        Reject(open: false); Reject(root: true); Reject(appearance: MenuAppearance.Default with { InstructionsVisible = false });
        Reject(width: new()); Reject(unlock: "pause"); Reject(phase: "unknown");
        var ordinary = StateDocument.Create(42, 1, false, empty, 0);
        TestAssert.True(ordinary.InstructionPositionEditor is null, "Legacy Create callers must not gain editor metadata");
    }
    public static void FocusHiddenProjectionClearsEditorMetadataButPreservesCommittedCoordinatesAndOwnerState() {
        var visit = InstructionPositionEditorTests.Open(); visit.Editing(); visit.Send(OverlayInput.MoveInstructionsRight);
        var owner = visit.Controller.Snapshot(); var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(!hidden.IsOpen && hidden.InstructionPositionEditor is null && hidden.WidthEditor is null && hidden.MenuUnlock is null,
            "Hidden frames cannot carry active position editor metadata");
        TestAssert.Equal(owner.Appearance, hidden.Appearance); TestAssert.Equal(owner.NavigationRevision, hidden.NavigationRevision);
        TestAssert.True(owner.InstructionPositionEditor?.Phase == "editing" && owner.IsOpen,
            "Focus projection must leave the actual owner visit intact");
        using var json = JsonDocument.Parse(hidden.Serialize());
        var position = json.RootElement.GetProperty("appearance").GetProperty("instructionPosition");
        TestAssert.Equal(263, position.GetProperty("x").GetInt32()); TestAssert.Equal(472, position.GetProperty("y").GetInt32());
    }
}
