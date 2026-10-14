using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPositionTests
{
    public static void DerivedDefaultSignedMovementAndOffsetBoundsMatchTheSourceWithoutAScreenClamp()
    {
        var appearance = MenuAppearance.Default;
        TestAssert.True(appearance.MenuPosition is null, "The old appearance derives its position");
        TestAssert.Equal(new MenuPosition(0, 85), MenuPosition.Resolve(appearance));
        var origin = new MenuPosition(-1000, -500);
        foreach (var pair in new[] { (OverlayInput.MoveMenuUp,new MenuPosition(-1000,-510)),
            (OverlayInput.MoveMenuDown,new MenuPosition(-1000,-490)), (OverlayInput.MoveMenuLeft,new MenuPosition(-1010,-500)),
            (OverlayInput.MoveMenuRight,new MenuPosition(-990,-500)) })
        {
            TestAssert.True(origin.TryMove(pair.Item1, 10, out var moved), "Signed source movement must remain available off screen");
            TestAssert.Equal(pair.Item2, moved); TestAssert.Equal(new MenuPosition(-1000, -500), origin);
        }
        var step = new LocalMenuPositionEditorState();
        for (int index = 0; index < 20; index++) TestAssert.True(step.TryAdjustStep(1, out step), "Valid offset increments are accepted");
        TestAssert.Equal(10, step.Step);
        for (int index = 0; index < 20; index++) TestAssert.True(step.TryAdjustStep(-1, out step), "Valid offset decrements are accepted");
        TestAssert.Equal(1, step.Step);
    }

    public static void OverflowInvalidStepsAndUnrelatedInputsRejectWithoutChangingEitherCoordinate()
    {
        foreach (var pair in new[] { (new MenuPosition(int.MaxValue,85),OverlayInput.MoveMenuRight),
            (new MenuPosition(int.MinValue,85),OverlayInput.MoveMenuLeft), (new MenuPosition(0,int.MaxValue),OverlayInput.MoveMenuDown),
            (new MenuPosition(0,int.MinValue),OverlayInput.MoveMenuUp) })
        {
            TestAssert.True(!pair.Item1.TryMove(pair.Item2, 10, out var unchanged) && unchanged == pair.Item1,
                "Coordinate overflow cannot partially commit another axis");
        }
        var point = new MenuPosition(0, 85);
        foreach (int invalid in new[] { int.MinValue, 0, 11, int.MaxValue })
        {
            TestAssert.True(!point.TryMove(OverlayInput.MoveMenuRight, invalid, out var unchanged) && unchanged == point, "Only source offset bounds are supported");
            var state = new LocalMenuPositionEditorState(invalid);
            TestAssert.True(!state.IsSupported() && !state.TryAdjustStep(1, out var retained) && retained == state,
                "Invalid metadata cannot silently repair its own offset");
        }
        TestAssert.True(!point.TryMove(OverlayInput.Select, 1, out var retainedPoint) && retainedPoint == point, "Ordinary input is not a movement command");
        var valid = new LocalMenuPositionEditorState();
        foreach (int direction in new[] { -2, 0, 2 }) TestAssert.True(!valid.TryAdjustStep(direction, out var retained) && retained == valid, "Offsets require an exact direction");
        TestAssert.Equal(13, (int)MenuPage.MenuPositionEditor); TestAssert.Equal(18, (int)OverlayInput.MoveMenuUp);
        TestAssert.Equal(24, (int)OverlayInput.FinishMenuPositionEditor);
    }
}
