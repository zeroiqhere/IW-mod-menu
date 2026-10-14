using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuNoticeTests {
    private static StateDocument Document(string notice, string detail) => StateDocument.Create(42, 1, true,
        new("Apparition", "PREVIEW", new[] { new MenuRow("Connect", "", false, new(MenuActionKind.None), detail) }, notice), 0);

    public static void AnExistingCompleteSelectedDetailParagraphIsDisplayedOnlyOnce() {
        foreach (string notice in new[] { "Preview mode", "Cleanup failed\nPreview mode", "Preview mode\nCleanup failed",
            "Cleanup failed\nPreview mode\nAnother status" }) {
            TestAssert.Equal(notice, Document(notice, "Preview mode").Notice);
        }
        TestAssert.Equal("Cleanup failed\nLine one\nLine two\nStatus", Document("Cleanup failed\nLine one\nLine two\nStatus", "Line one\nLine two").Notice);
    }

    public static void PartialOrDifferentlyCasedDetailsRemainDistinctAndEmptyDetailsDoNotAddLines() {
        foreach (string notice in new[] { "Preview mode unavailable", "Not Preview mode", "preview mode" })
            TestAssert.Equal(notice + "\nPreview mode", Document(notice, "Preview mode").Notice);
        TestAssert.Equal("Status", Document("Status", "").Notice);
        TestAssert.Equal("Help", Document("", "Help").Notice);
        TestAssert.Equal("", Document("", "").Notice);
    }

    public static void DuplicateComparisonUsesTheSameControlSanitizationAsThePublishedNotice() {
        var frame = Document("Cleanup failed\r\nPreview\t mode", "Preview mode");
        TestAssert.Equal("Cleanup failed\nPreview mode", frame.Notice);
        TestAssert.Equal("Line one\nLine two", Document("Line one\r\nLine two", "Line one\nLine two").Notice);
    }
}
