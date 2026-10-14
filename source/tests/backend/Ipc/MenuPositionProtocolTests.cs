using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPositionProtocolTests
{
    public static void SevenExactCommandsRequireStrictUnsignedEpochsAndRejectMalformedOrUnknownPackets()
    {
        string[] names = { "moveMenuUp", "moveMenuDown", "moveMenuLeft", "moveMenuRight", "increaseMenuPositionStep", "decreaseMenuPositionStep", "finishMenuPositionEditor" };
        for (int index = 0; index < names.Length; index++)
        {
            CommandEnvelope? Parse(string suffix) => CommandEnvelope.Parse(Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"" + names[index] + "\"" + suffix + "}"));
            TestAssert.Equal(MenuPositionEditorTests.Commands()[index], Parse(",\"navigationRevision\":0")!.Input);
            TestAssert.True(Parse(",\"navigationRevision\":18446744073709551615") is { Valid: true, NavigationRevision: ulong.MaxValue }, "Exact commands accept both unsigned boundaries");
            foreach (string suffix in new[] { "", ",\"navigationRevision\":true", ",\"navigationRevision\":-1", ",\"navigationRevision\":1.5", ",\"navigationRevision\":\"0\"", ",\"navigationRevision\":0,\"navigationRevision\":1", ",\"navigationRevision\":0,\"unknown\":1" })
                TestAssert.True(Parse(suffix) is { Valid: false, Sequence: 1 }, "Malformed commands remain acknowledged but cannot dispatch");
        }
    }

    public static void MetadataRequiresAnOpenEmptyNonRootExclusiveEditorAndFocusProjectionPreservesPosition()
    {
        var rig = new MenuPositionEditorTests.Rig(); rig.Enter(); rig.Send(OverlayInput.MoveMenuRight);
        var owner = rig.Current(); var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(hidden.MenuPositionEditor is null && !hidden.IsOpen && owner.MenuPositionEditor is not null, "Hidden presentation clears metadata without changing its owner");
        TestAssert.Equal(owner.Appearance, hidden.Appearance); TestAssert.Equal(owner.MenuRevision, hidden.MenuRevision);
        var empty = new MenuSnapshot("Menu Customization", "PREVIEW", Array.Empty<MenuRow>(), "");
        void Reject(bool open = true, bool root = false, int index = 0, MenuSnapshot? snapshot = null, LocalWidthEditorState? width = null,
            InstructionPositionEditor? instructions = null, string? unlock = null, int step = 1) => TestAssert.Throws<InvalidOperationException>(() =>
                StateDocument.Create(42, 1, open, snapshot ?? empty, index, isRootMenu: root, widthEditor: width, instructionPositionEditor: instructions,
                    menuUnlock: unlock, menuPositionEditor: new(step)));
        Reject(open: false); Reject(root: true); Reject(index: 1); Reject(width: new()); Reject(instructions: new("editing"));
        Reject(unlock: "pause"); Reject(step: 0); Reject(step: 11);
        Reject(snapshot: empty with { Rows = new[] { new MenuRow("Row", "", true, new(MenuActionKind.None)) } });
        using var json = JsonDocument.Parse(owner.Serialize());
        TestAssert.Equal(1, json.RootElement.GetProperty("menuPositionEditor").GetProperty("step").GetInt32());
        TestAssert.Equal(1, json.RootElement.GetProperty("appearance").GetProperty("menuPosition").GetProperty("x").GetInt32());
        TestAssert.True(StateDocument.Create(42, 0, false, empty, 0).MenuPositionEditor is null, "Legacy state remains outside the editor");
    }

    public static void OptionalVerifiedCapabilityFlagPreservesTheOldCliAndRejectsAmbiguousArguments()
    {
        string[] ordinary = { "--game-pid", "42", "--session-directory", Path.GetTempPath() };
        TestAssert.True(!BackendOptions.Parse(ordinary).MenuPositionControlsAvailable, "Old launch arguments cannot enable the editor");
        TestAssert.True(BackendOptions.Parse(ordinary.Concat(new[] { "--menu-position-controls-available" }).ToArray()).MenuPositionControlsAvailable,
            "The private launcher can explicitly publish its verified editor capability");
        foreach (string[] invalid in new[]{ordinary.Concat(new[]{"--unknown"}).ToArray(),ordinary.Concat(new[]{"--menu-position-controls-available","true"}).ToArray(),
            ordinary.Concat(new[]{"--menu-position-controls-available","--menu-position-controls-available"}).ToArray(),Array.Empty<string>()})
            TestAssert.Throws<ArgumentException>(() => BackendOptions.Parse(invalid));
    }
}
