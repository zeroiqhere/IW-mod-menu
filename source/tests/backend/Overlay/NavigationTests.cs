using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class NavigationTests {
    public static void RevisitingAChildPageRestoresItsSavedCursor() {
        var navigation = Started();
        navigation.SelectIndex(2);
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(4));
        navigation.SelectIndex(3);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(4));
        TestAssert.Equal(3, navigation.SelectedIndex);
    }

    public static void ClosingAndReopeningResumesThePageCursorAndParentPath() {
        var navigation = Started();
        navigation.SelectIndex(2);
        navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(Rows(4));
        navigation.SelectIndex(3);
        navigation.Close();
        navigation.Open();
        navigation.SetRows(Rows(4));
        TestAssert.Equal(MenuPage.Customization, navigation.Page);
        TestAssert.Equal(3, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(2, navigation.SelectedIndex);
    }

    public static void BackAtRootClosesAndReopeningPreservesTheRootCursor() {
        var navigation = Started();
        navigation.SelectIndex(3);
        navigation.Handle(OverlayInput.Back);
        TestAssert.True(!navigation.IsOpen, "Melee at root closes the menu");
        navigation.Open();
        navigation.SetRows(Rows(5));
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(3, navigation.SelectedIndex);
    }

    public static void RestoredChildCursorClampsToTheCurrentRowsBeforeSelection() {
        var navigation = Started();
        navigation.OpenPage(MenuPage.Players);
        navigation.SetRows(Rows(4));
        navigation.SelectIndex(3);
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows(5));
        navigation.OpenPage(MenuPage.Players);
        navigation.SetRows(Rows(2));
        TestAssert.Equal(1, navigation.SelectedIndex);
        TestAssert.Equal(1, navigation.Handle(OverlayInput.Select)!.Slot);
    }

    public static void FirstVisitStartsAtZeroAndDisabledRowsRemainInspectable() {
        var navigation = Started();
        navigation.SelectIndex(4);
        navigation.OpenPage(MenuPage.Support);
        navigation.SetRows(new[] { new MenuRow("Unavailable", "UNAVAILABLE", false,
            new(MenuActionKind.None), "Feature unavailable") });
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.True(navigation.Handle(OverlayInput.Select) is null, "An unavailable row cannot dispatch");
        TestAssert.Equal("Feature unavailable", navigation.Rows[0].Detail);
    }

    private static OverlayNavigation Started() {
        var navigation = new OverlayNavigation();
        navigation.Open();
        navigation.SetRows(Rows(5));
        return navigation;
    }

    private static IReadOnlyList<MenuRow> Rows(int count) => Enumerable.Range(0, count)
        .Select(index => new MenuRow("Row " + index, "", true,
            new(MenuActionKind.ChangeAccent, Slot: index))).ToArray();
}
