using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuCreditsProtocolTests
{
    public static void ExactFinishNameRequiresAStrictUnsignedEpochWhileMalformedPacketsRemainAcknowledged()
    {
        CommandEnvelope? Parse(string suffix, string name = "finishMenuCredits") => CommandEnvelope.Parse(Encoding.UTF8.GetBytes(
            "{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"" + name + "\"" + suffix + "}"));
        foreach (string epoch in new[] { "0", "18446744073709551615" })
            TestAssert.True(Parse(",\"navigationRevision\":" + epoch) is { Valid: true, Input: OverlayInput.FinishMenuCredits }, "Both strict uint64 epoch boundaries must be accepted");
        // A recoverable sequence does not make malformed editor input actionable
        foreach (string suffix in new[] { "", ",\"navigationRevision\":null", ",\"navigationRevision\":true", ",\"navigationRevision\":-1", ",\"navigationRevision\":1.5", ",\"navigationRevision\":\"0\"", ",\"navigationRevision\":0,\"navigationRevision\":1", ",\"navigationRevision\":0,\"unknown\":1" })
            TestAssert.True(Parse(suffix) is { Valid: false, Sequence: 1 }, "Invalid payloads must be recoverably acknowledged without finishing credits");
        TestAssert.True(Parse(",\"navigationRevision\":0", "finishCredits") is { Valid: false }, "Guessed aliases cannot create new input routes");
        TestAssert.Equal(14, (int)MenuPage.MenuCredits); TestAssert.Equal(25, (int)OverlayInput.FinishMenuCredits);
        TestAssert.Equal(13, (int)MenuPage.MenuPositionEditor); TestAssert.Equal(24, (int)OverlayInput.FinishMenuPositionEditor);
    }

    public static void MetadataHasStrictElapsedBoundsAndRequiresAnOpenEmptyExclusiveChildPresentation()
    {
        var empty = new MenuSnapshot("Menu Customization", "PREVIEW", Array.Empty<MenuRow>(), "");
        StateDocument Create(int elapsed = 0, bool open = true, bool root = false, int index = 0, MenuSnapshot? snapshot = null,
            LocalWidthEditorState? width = null, InstructionPositionEditor? instructions = null,
            LocalMenuPositionEditorState? position = null, string? unlock = null) => StateDocument.Create(42, 0, open,
                snapshot ?? empty, index, isRootMenu: root, widthEditor: width, instructionPositionEditor: instructions,
                menuPositionEditor: position, menuUnlock: unlock, menuCredits: new(elapsed));
        foreach (int elapsed in new[] { 0, 39999 })
        {
            using var json = JsonDocument.Parse(Create(elapsed).Serialize());
            var credits = json.RootElement.GetProperty("menuCredits"); TestAssert.Equal(1, credits.EnumerateObject().Count());
            TestAssert.Equal(elapsed, credits.GetProperty("elapsedMilliseconds").GetInt32());
        }
        // Both the numeric domain and presentation exclusivity are producer guarantees
        TestAssert.Throws<InvalidOperationException>(() => Create(-1)); TestAssert.Throws<InvalidOperationException>(() => Create(40000));
        TestAssert.Throws<InvalidOperationException>(() => Create(open: false)); TestAssert.Throws<InvalidOperationException>(() => Create(root: true));
        TestAssert.Throws<InvalidOperationException>(() => Create(index: 1)); TestAssert.Throws<InvalidOperationException>(() => Create(width: new()));
        TestAssert.Throws<InvalidOperationException>(() => Create(instructions: new("editing"))); TestAssert.Throws<InvalidOperationException>(() => Create(position: new()));
        TestAssert.Throws<InvalidOperationException>(() => Create(unlock: "pause"));
        TestAssert.Throws<InvalidOperationException>(() => Create(snapshot: empty with { Rows = new[] { new MenuRow("Row", "", true, new(MenuActionKind.None)) } }));
        TestAssert.True(StateDocument.Create(42, 0, false, empty, 0).MenuCredits is null, "Legacy factory callers must publish no credits state");
    }

    public static void FocusProjectionHidesOnlyCreditsMetadataAndPreservesTheCommittedOwnerContextAndAppearance()
    {
        var rig = new MenuCreditsTests.Rig(); rig.Enter(); rig.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        var owner = rig.Current(); var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(owner.MenuCredits?.ElapsedMilliseconds == 1000 && hidden.MenuCredits is null && !hidden.IsOpen,
            "Focus projection must hide credits without cancelling its owning timer");
        TestAssert.Equal(owner.NavigationRevision, hidden.NavigationRevision); TestAssert.Equal(owner.MenuRevision, hidden.MenuRevision);
        TestAssert.Equal(owner.Appearance, hidden.Appearance); TestAssert.Equal(owner.LastSequence, hidden.LastSequence);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(500)); TestAssert.Equal(1500, rig.Current().MenuCredits?.ElapsedMilliseconds);
        rig.NoGameWork();
    }
}
