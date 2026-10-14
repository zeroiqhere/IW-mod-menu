using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Labels choose current source rows without hardcoding retained cursor positions
internal static class PlayerMenuTestNavigation {
    internal static void Select(ScopedOpeningFixture rig, string label) {
        var frame = rig.Controller.Snapshot();
        int index = frame.Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(index >= 0, "The current source row must exist: " + label);
        for (int moved = 0; moved < frame.Rows.Count && rig.Navigation.SelectedIndex != index; ++moved) rig.Send(OverlayInput.Down);
        TestAssert.Equal(index, rig.Navigation.SelectedIndex);
        rig.Send(OverlayInput.Select);
    }
}
