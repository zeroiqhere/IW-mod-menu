using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ActionContextTests {
    public static void ChangingActionContextInvalidatesCommandsWithoutMovingOrRepaintingTheMenu() {
        var navigation = new OverlayNavigation();
        navigation.Open();
        navigation.SetRows(Rows());
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.OpenControls);
        navigation.SetRows(Rows());
        navigation.SelectIndex(2);
        ulong before = navigation.Revision;
        ulong painting = navigation.MenuRevision;
        var rows = navigation.Rows;

        // A different applied slot changes what Select means while the visible cursor stays put
        navigation.InvalidateActionContext();

        TestAssert.Equal(before + 1, navigation.Revision);
        TestAssert.Equal(painting, navigation.MenuRevision);
        TestAssert.Equal(2, navigation.SelectedIndex);
        TestAssert.Equal(MenuPage.OpenControls, navigation.Page);
        TestAssert.True(navigation.IsOpen && ReferenceEquals(rows, navigation.Rows),
            "Command invalidation must preserve the open menu and its rows");
        TestAssert.Equal(2, navigation.Handle(OverlayInput.Select)!.Slot);
        // The parent path and retained parent cursor survive this local context change
        navigation.Handle(OverlayInput.Back);
        navigation.SetRows(Rows());
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(1, navigation.SelectedIndex);
    }

    public static void ExhaustedCommandContextRejectsWithoutChangingAnyNavigationState() {
        var navigation = new OverlayNavigation(ulong.MaxValue, 17);
        var rows = navigation.Rows;
        bool rejected = false;
        try {
            navigation.InvalidateActionContext();
        } catch (OverflowException) {
            rejected = true;
        }

        // Exhaustion cannot wrap an old capture into a fresh accepted command scope
        TestAssert.True(rejected, "The final command revision must reject another context change");
        TestAssert.Equal(ulong.MaxValue, navigation.Revision);
        TestAssert.Equal(17UL, navigation.MenuRevision);
        TestAssert.Equal(MenuPage.Main, navigation.Page);
        TestAssert.Equal(0, navigation.SelectedIndex);
        TestAssert.True(!navigation.IsOpen && ReferenceEquals(rows, navigation.Rows),
            "Failed invalidation must preserve every existing owner");
    }

    private static IReadOnlyList<MenuRow> Rows() => Enumerable.Range(0, 3)
        .Select(index => new MenuRow("Row " + index, "", true,
            new(MenuActionKind.ChangeAccent, Slot: index))).ToArray();
}
