using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPositionEpochTests
{
    public static void FailedEditorEntryAtTheNavigationBoundaryCannotRetainAnUnpublishedVisit()
    {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var navigation = new OverlayNavigation(ulong.MaxValue - 3, 0);
        navigation.Open(); navigation.OpenPage(MenuPage.Customization);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.Customization, session.State,
            menuPositionEditorControlsAvailable: true).Rows);
        navigation.SelectIndex(3);
        var controller = new BackendController(42, session, navigation, true);
        var original = controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, original.NavigationRevision);
        TestAssert.Throws<OverflowException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Select, true), () => true));
        var retained = controller.Snapshot();
        TestAssert.Equal(original.MenuRevision, retained.MenuRevision);
        TestAssert.Equal(original.NavigationRevision, retained.NavigationRevision);
        TestAssert.Equal("Reposition Menu", retained.Rows[retained.SelectedIndex].Label);
        TestAssert.True(retained.MenuPositionEditor is null && !retained.Notice.Contains("Position X:", StringComparison.Ordinal),
            "A rejected page change cannot retain a latent editor visit or advertise its coordinates");
        TestAssert.Equal(0, session.Executed.Count);
    }
}
