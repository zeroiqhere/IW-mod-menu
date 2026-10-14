using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class LocalWidthEditorStateTests {
    public static void WidthAndOffsetClampAtSourceBoundsWithoutWrapping() {
        var editor = new LocalWidthEditorState();
        TestAssert.Equal(1, editor.Step);
        for (int index = 0; index < 20; index++) {
            TestAssert.True(editor.TryAdjustStep(1, out var next), "A valid increase must be accepted");
            editor = next;
        }
        TestAssert.Equal(10, editor.Step);
        TestAssert.True(editor.TryAdjustWidth(495, 1, out int width), "Width must commit immediately");
        TestAssert.Equal(500, width);
        TestAssert.True(editor.TryAdjustWidth(205, -1, out width), "Decrease must clamp to the source minimum");
        TestAssert.Equal(200, width);
        TestAssert.True(editor.TryAdjustWidth(500, 1, out width), "The maximum is a harmless bounded no-op");
        TestAssert.Equal(500, width);
        for (int index = 0; index < 20; index++) { editor.TryAdjustStep(-1, out var next); editor = next; }
        TestAssert.Equal(1, editor.Step);
    }

    public static void UnsupportedWidthStepOrDirectionCannotChangeAppearanceOrEditorState() {
        foreach (var editor in new[] { new LocalWidthEditorState(0), new LocalWidthEditorState(11) }) {
            TestAssert.True(!editor.TryAdjustWidth(260, 1, out int width) && width == 260, "Invalid offset must reject width changes");
            TestAssert.True(!editor.TryAdjustStep(1, out var unchanged) && unchanged == editor, "Invalid offset cannot repair itself silently");
        }
        var valid = new LocalWidthEditorState();
        foreach (int width in new[] { 199, 501, int.MinValue, int.MaxValue }) {
            TestAssert.True(!valid.TryAdjustWidth(width, 1, out int unchanged) && unchanged == width, "Out of bounds appearance must reject");
            TestAssert.True(!(MenuAppearance.Default with { MenuWidth = width }).IsSupported(), "Appearance must enforce width bounds");
        }
        foreach (int direction in new[] { 0, -2, 2, int.MinValue, int.MaxValue }) {
            TestAssert.True(!valid.TryAdjustWidth(260, direction, out int width) && width == 260, "Only two exact directions are supported");
            TestAssert.True(!valid.TryAdjustStep(direction, out var unchanged) && unchanged == valid, "Invalid direction cannot alter offset");
        }
    }
}
