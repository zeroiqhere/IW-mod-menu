using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class FamilyNavigationControllerTests {
    public static void ControllerBackReadsNewestCursorAfterNestedPageRevisit() {
        var (controller, navigation, session, _) = RevisitedCustomization();
        // Back crosses the same page-name path through the real command admission route
        controller.Consume(new(1, 1, 42, OverlayInput.Back, true), () => true);
        TestAssert.Equal(MenuPage.ThemeColors, navigation.Page);
        TestAssert.Equal(5, controller.Snapshot().SelectedIndex);
        controller.Consume(new(2, 1, 42, OverlayInput.Back, true), () => true);
        TestAssert.Equal(MenuPage.Customization, navigation.Page);
        TestAssert.Equal(4, controller.Snapshot().SelectedIndex);
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void ControllerReopenKeepsTheLatestSharedParentCursor() {
        var (controller, navigation, session, clock) = RevisitedCustomization();
        controller.Consume(new(1, 1, 42, OverlayInput.Back, true), () => true);
        // Closing retains history, and the ordinary opening wait still governs a fresh visit
        controller.Consume(new(2, 1, 42, OverlayInput.Toggle, true), () => true);
        TestAssert.True(!controller.Snapshot().IsOpen, "The local menu closes without a game action");
        controller.Consume(new(3, 1, 42, OverlayInput.Toggle, true), () => true);
        MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal(MenuPage.ThemeColors, navigation.Page);
        controller.Consume(new(4, 1, 42, OverlayInput.Back, true), () => true);
        TestAssert.Equal(MenuPage.Customization, navigation.Page);
        TestAssert.Equal(4, controller.Snapshot().SelectedIndex);
        TestAssert.Equal(0, session.Executed.Count);
    }

    private static (BackendController Controller, OverlayNavigation Navigation,
        SessionBoundary Session, ManualTimeProvider Clock) RevisitedCustomization() {
        var session = new SessionBoundary();
        var navigation = new OverlayNavigation();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, navigation, false, clock: clock);
        navigation.Open();
        controller.Snapshot();
        navigation.OpenPage(MenuPage.Customization);
        controller.Snapshot();
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.ThemeColors);
        controller.Snapshot();
        navigation.SelectIndex(5);
        // A reused page key changes its shared cursor even while an older path still refers to it
        navigation.OpenPage(MenuPage.Customization);
        controller.Snapshot();
        navigation.SelectIndex(4);
        return (controller, navigation, session, clock);
    }
}
