using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPositionFixtureTests
{
    public static void ActualPositionProfilesExposeImmediateEntryLiveCoordinatesOffsetBoundsAndTimedParentReturn()
    {
        var fixtures = MenuFixtureExporter.Build(); TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var initial = fixtures["menu-position-editor-default.json"];
        var moved = fixtures["menu-position-editor-moved.json"];
        var maximum = fixtures["menu-position-editor-step-max.json"];
        foreach (var state in new[] { initial, moved, maximum })
        {
            TestAssert.True(state.MenuPositionEditor is not null && state.IsOpen && !state.IsRootMenu && state.Rows.Count == 0 && state.SelectedIndex == 0,
                "Actual editor profiles must be empty child panels");
            TestAssert.Equal("Menu Customization", state.Title); TestAssert.Equal(initial.MenuRevision, state.MenuRevision);
            TestAssert.Equal(initial.NavigationRevision, state.NavigationRevision);
        }
        TestAssert.Equal(1, initial.MenuPositionEditor?.Step); TestAssert.True(initial.Appearance?.MenuPosition is null, "Default anchor stays derived");
        TestAssert.Equal(new MenuPosition(1, 86), moved.Appearance?.MenuPosition); TestAssert.Equal(1, moved.MenuPositionEditor?.Step);
        TestAssert.Equal(new MenuPosition(11, 96), maximum.Appearance?.MenuPosition); TestAssert.Equal(10, maximum.MenuPositionEditor?.Step);
        var rebuild = fixtures["menu-position-unlock-rebuild.json"]; var pause = fixtures["menu-position-unlock-pause.json"];
        var parent = fixtures["customization-positioned.json"];
        TestAssert.Equal("rebuild", rebuild.MenuUnlock); TestAssert.Equal("pause", pause.MenuUnlock); TestAssert.True(parent.MenuUnlock is null, "Parent completes its existing150ms unlock");
        foreach (var state in new[] { rebuild, pause, parent })
        {
            TestAssert.True(state.MenuPositionEditor is null && state.MenuRevision > initial.MenuRevision, "Parent return retires editor context");
            TestAssert.Equal("Reposition Menu", state.Rows[state.SelectedIndex].Label);
            TestAssert.Equal(new MenuPosition(11, 96), state.Appearance?.MenuPosition);
        }
        foreach (var fixture in fixtures.Where(pair => !pair.Key.StartsWith("menu-position-", StringComparison.Ordinal) && pair.Key != "customization-positioned.json"))
        {
            TestAssert.True(fixture.Value.Appearance?.MenuPosition is null && fixture.Value.MenuPositionEditor is null,
                "The original39 profiles retain their derived position and prior modes");
        }
    }
}
