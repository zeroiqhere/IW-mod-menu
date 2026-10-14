using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPreferenceRefreshProtocolTests
{
    public static void MetadataAcceptsOnlyTwoExactPhasesInAnOpenPopulatedExclusiveChildMenu()
    {
        var row = new MenuRow("Spotlight Cursor", "ON", true, new(MenuActionKind.ChangePreference), "Use changes spotlight");
        var populated = new MenuSnapshot("Menu Preferences", "PREVIEW", new[] { row }, "");
        StateDocument Create(string phase = "pending", bool open = true, bool root = false, int index = 0,
            MenuSnapshot? snapshot = null, LocalWidthEditorState? width = null, InstructionPositionEditor? instructions = null,
            LocalMenuPositionEditorState? position = null, MenuCredits? credits = null, string? unlock = null) => StateDocument.Create(42, 0, open,
                snapshot ?? populated, index, isRootMenu: root, widthEditor: width, instructionPositionEditor: instructions,
                menuPositionEditor: position, menuCredits: credits, menuUnlock: unlock, menuPreferenceRefresh: new(phase));
        foreach (string phase in new[] { "pending", "cooldown" })
        {
            using var json = JsonDocument.Parse(Create(phase).Serialize()); var value = json.RootElement.GetProperty("menuPreferenceRefresh");
            TestAssert.Equal(1, value.EnumerateObject().Count()); TestAssert.Equal(phase, value.GetProperty("phase").GetString());
            TestAssert.True(!json.RootElement.GetProperty("notice").GetString()!.Contains("Use changes", StringComparison.Ordinal), "Paused metadata suppresses selected action help");
        }
        // The producer cannot combine this populated panel pause with a hidden rebuild or editor
        foreach (string invalid in new[] { "", "Pending", "rebuild", "unlocked" }) TestAssert.Throws<InvalidOperationException>(() => Create(invalid));
        TestAssert.Throws<InvalidOperationException>(() => Create(open: false)); TestAssert.Throws<InvalidOperationException>(() => Create(root: true));
        TestAssert.Throws<InvalidOperationException>(() => Create(index: -1)); TestAssert.Throws<InvalidOperationException>(() => Create(index: 1));
        TestAssert.Throws<InvalidOperationException>(() => Create(snapshot: populated with { Rows = Array.Empty<MenuRow>() }));
        TestAssert.Throws<InvalidOperationException>(() => Create(width: new())); TestAssert.Throws<InvalidOperationException>(() => Create(instructions: new("editing")));
        TestAssert.Throws<InvalidOperationException>(() => Create(position: new())); TestAssert.Throws<InvalidOperationException>(() => Create(credits: new(0)));
        TestAssert.Throws<InvalidOperationException>(() => Create(unlock: "pause"));
        var legacy = StateDocument.Create(42, 0, true, populated, 0); TestAssert.True(legacy.MenuPreferenceRefresh is null, "Old producers remain outside the pause");
        TestAssert.Equal("Use changes spotlight", legacy.Notice);
    }

    public static void FocusProjectionClearsOnlyRefreshMetadataWhileRetainingCommittedQuickExitAndOwnerEpochs()
    {
        var rig = new MenuPreferenceRefreshTests.Rig(); rig.Open(); rig.Select("Quick Exit"); var owner = rig.Current();
        // Projection changes the renderer packet while the retained owner remains on its timeline
        var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(owner.MenuPreferenceRefresh is not null && hidden.MenuPreferenceRefresh is null && !hidden.IsOpen, "Projection cannot publish a closed locked menu");
        TestAssert.Equal(owner.Appearance, hidden.Appearance); TestAssert.True(hidden.Appearance!.QuickExit, "Quick Exit is actual committed policy even while its checkbox display waits");
        TestAssert.Equal(owner.NavigationRevision, hidden.NavigationRevision); TestAssert.Equal(owner.MenuRevision, hidden.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase); rig.NoGameWork();
    }
}
