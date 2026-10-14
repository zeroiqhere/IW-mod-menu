namespace Apparition.Backend.Ipc;

// Status and selected help share one bounded notice without repeated paragraphs
internal static class MenuNoticeComposer {
    public static string Compose(string notice, string detail) {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(detail);
        if (detail.Length == 0) return notice;
        if (notice.Length == 0) return detail;
        int position = 0;
        while ((position = notice.IndexOf(detail, position, StringComparison.Ordinal)) >= 0) {
            int end = position + detail.Length;
            // A phrase inside a longer status is still distinct selected-row help
            bool startsParagraph = position == 0 || notice[position - 1] == '\n';
            bool endsParagraph = end == notice.Length || notice[end] == '\n';
            if (startsParagraph && endsParagraph) return notice;
            position++;
        }
        // Existing status order stays intact when new help adds information
        return notice + "\n" + detail;
    }
}
