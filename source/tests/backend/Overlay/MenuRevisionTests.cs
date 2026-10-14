using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuRevisionTests {
    public static void CursorValueAvailabilityDetailAndRepeatedSnapshotsKeepThePresentationContextStable() {
        var navigation = new OverlayNavigation(); navigation.Open(); navigation.SetRows(Rows());
        ulong menu = navigation.MenuRevision, selection = navigation.Revision;
        navigation.Handle(OverlayInput.Down);
        TestAssert.True(navigation.Revision > selection, "Cursor movement must still invalidate delayed action context");
        TestAssert.Equal(menu, navigation.MenuRevision);
        var values = Rows().Select(row => row with { Value = "ON", Enabled = false, Detail = "Changing status" }).ToArray();
        navigation.SetRows(values); navigation.SetRows(values);
        TestAssert.Equal(menu, navigation.MenuRevision);
        navigation.SelectIndex(0); navigation.SelectIndex(1);
        TestAssert.Equal(menu, navigation.MenuRevision);
    }

    public static void OpenClosePageBackStructureAndExplicitRebuildAdvanceOnlyTheNecessaryContext() {
        var navigation = new OverlayNavigation();
        ulong revision = navigation.MenuRevision;
        void Changed(Action action) { action(); TestAssert.True(navigation.MenuRevision > revision, "An actual menu rebuild must advance its context"); revision = navigation.MenuRevision; }
        Changed(navigation.Open); Changed(() => navigation.SetRows(Rows()));
        navigation.Open(); navigation.SetRows(Rows()); TestAssert.Equal(revision, navigation.MenuRevision);
        Changed(() => navigation.OpenPage(MenuPage.Preferences));
        Changed(() => navigation.SetRows(Rows("Other")));
        Changed(() => navigation.Handle(OverlayInput.Back));
        Changed(navigation.Close); Changed(navigation.Open);
        Changed(() => navigation.SetRows(Rows().Select(row => row with { Action = new(MenuActionKind.None) }).ToArray()));
        ulong selection = navigation.Revision; Changed(navigation.InvalidatePresentation);
        TestAssert.Equal(selection, navigation.Revision);
    }

    public static void InvalidRowsNeverChangeEitherCounterSelectionOrThePreviouslyPublishedRows() {
        var navigation = new OverlayNavigation(); navigation.Open(); navigation.SetRows(Rows()); navigation.SelectIndex(1);
        var retained = navigation.Rows; ulong menu = navigation.MenuRevision, selection = navigation.Revision;
        TestAssert.Throws<ArgumentException>(() => navigation.SetRows(new[] { Rows()[0], null! }));
        TestAssert.Throws<ArgumentException>(() => navigation.SetRows(new[] { Rows()[0] with { Action = null! } }));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => navigation.SetRows(Enumerable.Repeat(Rows()[0], 129).ToArray()));
        TestAssert.Equal(menu, navigation.MenuRevision); TestAssert.Equal(selection, navigation.Revision);
        TestAssert.Equal(1, navigation.SelectedIndex); TestAssert.True(ReferenceEquals(retained, navigation.Rows), "Invalid snapshots cannot replace retained rows");
    }

    public static void CounterOverflowRejectsStructuralReplacementOpeningAndPageChangesWithoutPartialMutation() {
        var menuFull = new OverlayNavigation(0, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(menuFull.Open);
        TestAssert.True(!menuFull.IsOpen && menuFull.Revision == 0, "A menu epoch overflow must not partially open or change the selection epoch");
        TestAssert.Throws<OverflowException>(() => menuFull.SetRows(Rows()));
        TestAssert.Equal(0, menuFull.Rows.Count);
        var selectionFull = new OverlayNavigation(ulong.MaxValue - 2, 0);
        selectionFull.Open(); selectionFull.SetRows(Rows()); selectionFull.SelectIndex(1);
        var retained = selectionFull.Rows; ulong menu = selectionFull.MenuRevision;
        TestAssert.Throws<OverflowException>(() => selectionFull.SetRows(new[] { Rows()[0] }));
        TestAssert.True(ReferenceEquals(retained, selectionFull.Rows), "A clamp overflow cannot replace rows first");
        TestAssert.Equal(1, selectionFull.SelectedIndex); TestAssert.Equal(menu, selectionFull.MenuRevision);
        TestAssert.Throws<OverflowException>(() => selectionFull.OpenPage(MenuPage.Preferences));
        TestAssert.Equal(MenuPage.Main, selectionFull.Page); TestAssert.Equal(1, selectionFull.SelectedIndex);
        TestAssert.Equal(menu, selectionFull.MenuRevision);
        TestAssert.Throws<OverflowException>(selectionFull.Close);
        TestAssert.True(selectionFull.IsOpen, "A close overflow cannot hide the retained menu first");
        var backFull = new OverlayNavigation(ulong.MaxValue - 4, 0);
        backFull.Open(); backFull.SetRows(Rows()); backFull.SelectIndex(1); backFull.OpenPage(MenuPage.Preferences);
        ulong beforeBack = backFull.MenuRevision;
        TestAssert.Throws<OverflowException>(() => backFull.Handle(OverlayInput.Back));
        TestAssert.Equal(MenuPage.Preferences, backFull.Page); TestAssert.Equal(0, backFull.SelectedIndex);
        TestAssert.Equal(beforeBack, backFull.MenuRevision);
    }

    private static MenuRow[] Rows(string prefix = "Row") => Enumerable.Range(0, 2)
        .Select(index => new MenuRow(prefix + index, "OFF", true, new(MenuActionKind.ChangeAccent, Slot: index))).ToArray();
}
