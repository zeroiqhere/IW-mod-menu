using syndishanx_iw_tool.Overlay;
using Apparition.Backend.Controller;
using System.Text.Json;

namespace Backend.Tests;

internal static class InstructionPositionTests {
    public static void NullablePositionsDeriveTheExactOrdinaryAndAlternateSourceDefaults() {
        TestAssert.True(MenuAppearance.Default.InstructionPosition is null, "Unsaved positions remain derived");
        TestAssert.Equal(new InstructionPosition(255, 472), InstructionPosition.Resolve(MenuAppearance.Default));
        TestAssert.Equal(new InstructionPosition(320, 472), InstructionPosition.Resolve(MenuAppearance.Default with { AlternateInstructions = true }));
        var manual = new InstructionPosition(-400, 700);
        TestAssert.Equal(manual, InstructionPosition.Resolve(MenuAppearance.Default with { InstructionPosition = manual }));
        using var json = JsonDocument.Parse(new BackendController(42, new SessionBoundary()).Snapshot().Serialize());
        TestAssert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("appearance").GetProperty("instructionPosition").ValueKind);
    }
    public static void EachMovementChangesOnlyOneCoordinateByEightWithoutAScreenClamp() {
        var original = new InstructionPosition(-8, -8);
        foreach (var pair in new[] { (OverlayInput.MoveInstructionsUp, new InstructionPosition(-8, -16)),
            (OverlayInput.MoveInstructionsDown, new InstructionPosition(-8, 0)),
            (OverlayInput.MoveInstructionsLeft, new InstructionPosition(-16, -8)),
            (OverlayInput.MoveInstructionsRight, new InstructionPosition(0, -8)) }) {
            TestAssert.True(original.TryMove(pair.Item1, out var updated), "Supported movement must apply");
            TestAssert.Equal(pair.Item2, updated);
            TestAssert.Equal(new InstructionPosition(-8, -8), original);
        }
    }
    public static void CoordinateOverflowAndUnsupportedInputsRejectWithoutChangingThePosition() {
        foreach (var pair in new[] { (new InstructionPosition(int.MaxValue - 7, 0), OverlayInput.MoveInstructionsRight),
            (new InstructionPosition(int.MinValue + 7, 0), OverlayInput.MoveInstructionsLeft),
            (new InstructionPosition(0, int.MaxValue - 7), OverlayInput.MoveInstructionsDown),
            (new InstructionPosition(0, int.MinValue + 7), OverlayInput.MoveInstructionsUp),
            (new InstructionPosition(255, 472), OverlayInput.Up) }) {
            TestAssert.True(!pair.Item1.TryMove(pair.Item2, out var unchanged), "Overflow or an ordinary row input cannot move instructions");
            TestAssert.Equal(pair.Item1, unchanged);
        }
        TestAssert.True(new InstructionPosition(int.MaxValue - 8, 0).TryMove(OverlayInput.MoveInstructionsRight, out var edge),
            "The exact signed coordinate boundary remains valid");
        TestAssert.Equal(int.MaxValue, edge.X);
    }
    public static void AlternateTogglesResetManualPositionWhileOtherPreferencesPreserveIt() {
        var manual = MenuAppearance.Default with { InstructionPosition = new(123, 456), AdaptiveInstructions = true };
        TestAssert.True(manual.TryChange((int)MenuPreference.AlternateInstructions, out var alternate), "Alternate toggle must apply");
        TestAssert.True(alternate.InstructionPosition is null && alternate.AlternateInstructions && !alternate.AdaptiveInstructions,
            "Alternate must run the source reset and clear adaptive");
        var alternateManual = alternate with { InstructionPosition = new(-50, 800) };
        TestAssert.True(alternateManual.TryChange((int)MenuPreference.AlternateInstructions, out var normal), "Disabling alternate must reset too");
        TestAssert.True(normal.InstructionPosition is null && !normal.AlternateInstructions, "Either alternate toggle derives its default position");
        foreach (var preference in new[] { MenuPreference.InstructionsVisible, MenuPreference.InstructionsBackground,
            MenuPreference.OptionCounter, MenuPreference.LargeCursor }) {
            TestAssert.True(manual.TryChange((int)preference, out var changed), "Unrelated local preference must apply");
            TestAssert.Equal(manual.InstructionPosition, changed.InstructionPosition);
        }
    }
    public static void NewPositionEnumsAppendWithoutChangingTheExistingNavigationAndActionIds() {
        TestAssert.Equal(11, (int)MenuPage.Instructions);
        TestAssert.Equal(12, (int)MenuPage.InstructionPositionEditor);
        TestAssert.Equal(11, (int)MenuActionKind.ChangePreference);
        TestAssert.Equal(12, (int)MenuActionKind.ResetInstructionPosition);
        TestAssert.Equal(12, (int)OverlayInput.FinishWidthEditor);
        TestAssert.Equal(13, (int)OverlayInput.MoveInstructionsUp);
        TestAssert.Equal(17, (int)OverlayInput.FinishInstructionPositionEditor);
    }
}
