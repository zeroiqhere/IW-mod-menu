using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These tests exercise the real navigation owner without opening a game or a window
internal static class FamilyNavigationTests {
    public static void MainAndQuickFirstOpenUseDistinctRoots() {
        var navigation = Started();
        navigation.Close();
        navigation.Open(MenuSurface.Quick);

        TestAssert.Equal(MenuSurface.Quick, navigation.Surface);
        TestAssert.Equal(MenuPage.QuickMenu, navigation.Page);
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.Equal(0, navigation.Rows.Count);
        navigation.Close();
        navigation.Open();
        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.Equal(MenuPage.Main, navigation.Page);
    }

    public static void FamiliesRetainIndependentPagesAndParentPathsAcrossReopen() {
        var navigation = Started();
        navigation.SelectIndex(2);
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(5));
        navigation.SelectIndex(4);
        navigation.Close();

        navigation.Open(MenuSurface.Quick);
        navigation.SetRows(Rows(3));
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(Rows(3));
        navigation.SelectIndex(2);
        navigation.Close();

        navigation.Open(MenuSurface.Main);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Customization, navigation.Page);
        TestAssert.Equal(4, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
        navigation.Close();

        navigation.Open(MenuSurface.Quick);
        navigation.SetRows(Rows(3));
        TestAssert.Equal(MenuPage.Personal, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(3));
        TestAssert.Equal(MenuPage.QuickMenu, navigation.Page);
        TestAssert.Equal(1, navigation.SelectedIndex);
    }

    public static void BackReadsSharedParentCursorChangedByTheOtherFamily() {
        var navigation = Started();
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(5));
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.Preferences);
        navigation.SetRows(Rows(5));
        navigation.Close();

        // The second family changes the same page cursor while Main retains a nested path
        navigation.Open(MenuSurface.Quick);
        navigation.SetRows(Rows(5));
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(1, navigation.SelectedIndex);
        navigation.SelectIndex(4);
        navigation.Close();

        navigation.Open(MenuSurface.Main);
        navigation.SetRows(Rows(5));
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Customization, navigation.Page);
        TestAssert.Equal(4, navigation.SelectedIndex);
    }

    public static void CyclicVisitsRestoreTheLatestSharedCursorInsteadOfAParentCopy() {
        var navigation = Started();
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(5));
        // A cyclic path must not restore the older cursor captured by its first visit
        navigation.OpenPage(MenuPage.Main);
        navigation.SetRows(Rows(5));
        navigation.SelectIndex(4);

        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(4, navigation.SelectedIndex);
    }

    public static void SwitchingAnOpenFamilyRejectsWithoutChangingPublishedState() {
        var navigation = Started();
        navigation.SelectIndex(2);
        var rows = navigation.Rows;
        ulong revision = navigation.Revision;
        ulong menuRevision = navigation.MenuRevision;

        TestAssert.Throws<InvalidOperationException>(() => navigation.Open(MenuSurface.Quick));
        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
        TestAssert.True(ReferenceEquals(rows, navigation.Rows), "Rejected family switches retain published rows");
        navigation.Open(MenuSurface.Main);
        TestAssert.Equal(revision, navigation.Revision);
        navigation.Close();
        navigation.Open(MenuSurface.Quick);
        navigation.SetRows(Rows(3));
        revision = navigation.Revision;
        menuRevision = navigation.MenuRevision;
        navigation.Open(MenuSurface.Quick);
        TestAssert.Throws<InvalidOperationException>(() => navigation.Open());
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
        TestAssert.Equal(MenuSurface.Quick, navigation.Surface);
    }

    public static void PageAndFamilyChangesCannotDispatchRetainedRowsBeforeFreshRows() {
        var navigation = Started();
        var retained = navigation.Rows;
        navigation.OpenPage(MenuPage.Personal);

        TestAssert.True(navigation.Handle(OverlayInput.Select) is null, "A new page cannot dispatch the previous page action");
        navigation.Handle(OverlayInput.Down);
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.True(ReferenceEquals(retained, navigation.Rows), "Published old snapshots remain intact until replacement");
        navigation.SetRows(Rows(3));
        TestAssert.Equal(0, navigation.Handle(OverlayInput.Select)!.Slot);
        navigation.Close();
        navigation.Open(MenuSurface.Quick);
        TestAssert.True(navigation.Handle(OverlayInput.Select) is null, "A family root requires its own fresh rows");
        TestAssert.Equal(5, retained.Count);
    }

    public static void FreshRowsClampSharedCursorsAndSkipOnlyTypedSpacers() {
        var navigation = Started();
        navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(Rows(5));
        navigation.SelectIndex(4);
        navigation.Close();
        navigation.Open(MenuSurface.Quick);
        navigation.OpenPage(MenuPage.Personal);
        // A smaller fresh list can clamp onto a spacer, then retain inspectable help
        navigation.SetRows(new[] {
            new MenuRow("Unavailable", "UNAVAILABLE", false, new(MenuActionKind.None)),
            new MenuRow("", "", false, new(MenuActionKind.None), IsSpacer: true)
        });

        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.True(navigation.Handle(OverlayInput.Select) is null, "Disabled help remains visible without dispatch");
        navigation.Handle(OverlayInput.Down);
        TestAssert.Equal(0, navigation.SelectedIndex);
        navigation.SetRows(Array.Empty<MenuRow>());
        TestAssert.Equal(0, navigation.SelectedIndex);
    }

    public static void FamilyRowsAreCopiedAndAllSpacerRejectionPreservesTheSnapshot() {
        var navigation = Started();
        navigation.Close();
        navigation.Open(MenuSurface.Quick);
        MenuRow[] source = Rows(3);
        navigation.SetRows(source);
        var retained = navigation.Rows;
        source[0] = source[0] with { Label = "Changed outside navigation" };
        ulong revision = navigation.Revision;
        ulong menuRevision = navigation.MenuRevision;

        TestAssert.Equal("Row 0", retained[0].Label);
        TestAssert.Throws<ArgumentException>(() => navigation.SetRows(new[] {
            new MenuRow("", "", false, new(MenuActionKind.None), IsSpacer: true)
        }));
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
        TestAssert.True(ReferenceEquals(retained, navigation.Rows), "Invalid fresh rows cannot replace the snapshot");
    }

    public static void UnknownSurfaceRejectsBeforeChangingClosedOrOpenNavigation() {
        var navigation = Started();
        TestAssert.Throws<ArgumentOutOfRangeException>(() => navigation.Open((MenuSurface)99));
        navigation.Close();
        ulong revision = navigation.Revision;
        ulong menuRevision = navigation.MenuRevision;

        TestAssert.Throws<ArgumentOutOfRangeException>(() => navigation.Open((MenuSurface)99));
        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.True(!navigation.IsOpen, "Invalid surfaces cannot reopen a menu");
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
    }

    public static void FamilyOpenOverflowPreservesThePriorOwnerRowsAndCursor() {
        var navigation = new OverlayNavigation(ulong.MaxValue - 4, 0);
        navigation.Open();
        navigation.SetRows(Rows(5));
        navigation.SelectIndex(2);
        navigation.Close();
        var retained = navigation.Rows;
        ulong revision = navigation.Revision;
        ulong menuRevision = navigation.MenuRevision;

        // Switching would consume the final epoch and change the shared selection
        TestAssert.Throws<OverflowException>(() => navigation.Open(MenuSurface.Quick));
        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
        TestAssert.True(!navigation.IsOpen && ReferenceEquals(retained, navigation.Rows), "Overflow cannot switch ownership or discard rows");
    }

    public static void BackOverflowKeepsTheFamilyParentAvailableForRetry() {
        var navigation = new OverlayNavigation(ulong.MaxValue - 5, 0);
        navigation.Open(MenuSurface.Quick);
        navigation.SetRows(Rows(3));
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.Personal);
        ulong revision = navigation.Revision;
        ulong menuRevision = navigation.MenuRevision;

        TestAssert.Throws<OverflowException>(() => navigation.Handle(OverlayInput.Back));
        TestAssert.Equal(MenuSurface.Quick, navigation.Surface);
        TestAssert.Equal(MenuPage.Personal, navigation.Page);
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.Equal(revision, navigation.Revision);
        TestAssert.Equal(menuRevision, navigation.MenuRevision);
    }

    public static void ClosedPageSetupRemainsMainAndReopenKeepsItsParentPath() {
        var navigation = new OverlayNavigation();
        navigation.OpenPage(MenuPage.Preferences);
        navigation.SetRows(Rows(4));
        navigation.SelectIndex(3);
        navigation.Open();

        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.Equal(MenuPage.Preferences, navigation.Page);
        TestAssert.Equal(3, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(0, navigation.SelectedIndex);
    }

    private static OverlayNavigation Started() {
        var navigation = new OverlayNavigation();
        navigation.Open();
        navigation.SetRows(Rows(5));
        return navigation;
    }

    // Logical actions keep these regressions independent of a game backend
    private static MenuRow[] Rows(int count) => Enumerable.Range(0, count)
        .Select(index => new MenuRow("Row " + index, "", true,
            new(MenuActionKind.ChangeAccent, Slot: index))).ToArray();
}
