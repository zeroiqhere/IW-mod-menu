using Apparition.Backend.QuickMenu;

namespace Backend.Tests;

internal static class QuickMenuNavigationTests {
    public static void MainAndQuickKeepIndependentRetainedPathsAcrossCloseAndReopen() {
        var original = new QuickMenuNavigation();
        var main = original.Open(QuickMenuSurface.Main).WithCursor(4).Visit("Basic Scripts").WithCursor(1).Close();
        var quick = main.Open(QuickMenuSurface.Quick).WithCursor(6).Visit("Perk Menu").WithCursor(2).Close();

        TestAssert.Equal("Basic Scripts", quick.MainPage);
        TestAssert.Equal("Perk Menu", quick.QuickPage);
        TestAssert.Equal("Main", string.Join('|', quick.Parents(QuickMenuSurface.Main)));
        TestAssert.Equal("Quick Menu", string.Join('|', quick.Parents(QuickMenuSurface.Quick)));
        TestAssert.Equal("Main", original.MainPage);
        TestAssert.Equal("Quick Menu", original.QuickPage);
        var resumed = quick.Open(QuickMenuSurface.Quick);
        TestAssert.Equal("Perk Menu", resumed.CurrentPage);
        TestAssert.Equal(2, resumed.CurrentCursor);
        resumed = resumed.Back();
        TestAssert.Equal("Quick Menu", resumed.CurrentPage);
        TestAssert.Equal(6, resumed.CurrentCursor);
        TestAssert.Equal<QuickMenuSurface?>(null, resumed.Back().ActiveSurface);
        TestAssert.Equal("Basic Scripts", resumed.Close().Open(QuickMenuSurface.Main).CurrentPage);
        TestAssert.Throws<NotSupportedException>(() => ((IList<string>)quick.Parents(QuickMenuSurface.Main))[0] = "Changed");
    }

    public static void SharedSourcePageNamesShareCursorAndSliderDraftsAcrossBothMenus() {
        // Both source families use a page-name map rather than a cursor saved inside each parent entry
        // Draft storage checks the shared map only; no submenu option or callback is claimed
        var state = new QuickMenuNavigation().Open(QuickMenuSurface.Main).Visit("Perk Menu")
            .WithCursor(1).WithChoiceDraft(4, 2, 3).Visit("Nested source page").Close();
        state = state.Open(QuickMenuSurface.Quick).Visit("Perk Menu").WithCursor(3).WithChoiceDraft(4, 1, 3).Close();
        var resumed = state.Open(QuickMenuSurface.Main).Back();

        TestAssert.Equal("Perk Menu", resumed.CurrentPage);
        TestAssert.Equal(3, resumed.CurrentCursor);
        TestAssert.Equal(1, resumed.GetChoiceDraft("Perk Menu", 4));
        TestAssert.Equal<int?>(null, resumed.GetChoiceDraft("Perk Menu", 5));
        TestAssert.Equal(0, resumed.GetCursor("perk menu"));
        TestAssert.Equal("Quick Menu|Perk Menu", string.Join('|', state.Open(QuickMenuSurface.Quick).Visit("Another child").Parents(QuickMenuSurface.Quick)));
    }

    public static void InvalidNavigationAndDraftRequestsLeavePriorStateUnchanged() {
        var closed = new QuickMenuNavigation();
        TestAssert.Throws<InvalidOperationException>(() => closed.Visit("Perk Menu"));
        TestAssert.Throws<InvalidOperationException>(() => closed.WithCursor(0));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => closed.Open((QuickMenuSurface)99));
        var state = closed.Open(QuickMenuSurface.Quick).WithCursor(2);
        TestAssert.Throws<InvalidOperationException>(() => state.Open(QuickMenuSurface.Main));
        TestAssert.True(ReferenceEquals(state, state.Open(QuickMenuSurface.Quick)), "Repeated open discarded retained state");
        foreach (string invalid in new[] { "", " ", new string('x', 257) })
            TestAssert.Throws<ArgumentException>(() => state.Visit(invalid));
        foreach (int invalid in new[] { -1, 128 })
            TestAssert.Throws<ArgumentOutOfRangeException>(() => state.WithCursor(invalid));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => state.WithChoiceDraft(0, 3, 3));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => state.WithChoiceDraft(128, 0, 3));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => state.WithChoiceDraft(0, 0, 0));
        TestAssert.Equal("Quick Menu", state.CurrentPage);
        TestAssert.Equal(2, state.CurrentCursor);
        TestAssert.Equal<int?>(null, state.GetChoiceDraft("Quick Menu", 0));
        TestAssert.Equal<QuickMenuSurface?>(null, closed.ActiveSurface);

        var full = state;
        for (int depth = 0; depth < 128; depth++) full = full.Visit("Owned path " + depth);
        TestAssert.Equal(128, full.Parents(QuickMenuSurface.Quick).Count);
        TestAssert.Throws<InvalidOperationException>(() => full.Visit("Beyond the local path bound"));
        TestAssert.Equal("Owned path 127", full.CurrentPage);
        TestAssert.Equal(128, full.Parents(QuickMenuSurface.Quick).Count);
    }
}
