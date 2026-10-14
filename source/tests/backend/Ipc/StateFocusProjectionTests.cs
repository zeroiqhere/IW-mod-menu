using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StateFocusProjectionTests {
    public static void FocusHiddenProjectionClearsTransientPanelMetadataWithoutChangingTheOwnedMenu() {
        var clock = new ManualTimeProvider();
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        var editor = controller.Snapshot();
        var hiddenEditor = StateFocusProjection.Apply(editor, false);
        TestAssert.True(!hiddenEditor.IsOpen && hiddenEditor.WidthEditor is null, "An editor cannot remain active in a closed display frame");
        TestAssert.True(controller.Snapshot().WidthEditor is not null, "A projection cannot end the owned editor visit");
        WidthEditorTests.Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        var rebuilding = controller.Snapshot();
        TestAssert.Equal("rebuild", rebuilding.MenuUnlock);
        var hidden = StateFocusProjection.Apply(rebuilding, false);
        TestAssert.True(!hidden.IsOpen && hidden.MenuUnlock is null && hidden.WidthEditor is null,
            "The native protocol rejects any transient panel metadata in a closed display frame");
        TestAssert.Equal(rebuilding.Appearance, hidden.Appearance);
        TestAssert.Equal(rebuilding.NavigationRevision, hidden.NavigationRevision);
        TestAssert.True(ReferenceEquals(rebuilding, StateFocusProjection.Apply(rebuilding, true)), "A focused projection retains the actual immutable snapshot");
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        TestAssert.True(controller.Snapshot().MenuUnlock is null, "The actual retained timer continues independently of the projection");
    }
}
