using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// These actual local callbacks capture source configuration, not an engine or renderer Rainbow sample
internal static class InstructionCreationColorControllerTests {
    private static MenuChromeFixture Aio() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        for (int choice = 0; choice < 3; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(50);
        rig.Wait(200);
        rig.Send(OverlayInput.Back);
        return rig;
    }

    public static void FixedCreationColorSurvivesPaletteRedrawAndClosedMenuVisits() {
        var rig = Aio();
        var initial = rig.Current().InstructionHud!;
        TestAssert.Equal("AIO", initial.CreationDesign);
        TestAssert.Equal("#3998FE", initial.CreationAccent);
        TestAssert.Equal<bool?>(false, initial.CreationRainbow);
        rig.Select("Main Design Color");
        rig.Select("Red");
        TestAssert.Equal("#FF0000", rig.Current().Accent);
        rig.Wait(180);
        rig.Wait(250);
        TestAssert.Equal(initial, rig.Current().InstructionHud);
        rig.Send(OverlayInput.Back);
        TestAssert.Equal(initial, rig.Current().InstructionHud);
        rig.Send(OverlayInput.Toggle);
        TestAssert.Equal(initial, rig.Current().InstructionHud);
        rig.Open();
        rig.Wait(50);
        rig.Wait(550);
        TestAssert.Equal(initial, rig.Current().InstructionHud);
        rig.NoGameWrites();
    }

    public static void SourceRecreationsCaptureTheCurrentFixedColorWithoutChangingExistingTriggers() {
        var rig = Aio();
        rig.Select("Main Design Color");
        rig.Select("Red");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        ulong first = rig.Current().InstructionHud!.CreationRevision!.Value;
        rig.Select("Alternate Style");
        var alternate = rig.Current().InstructionHud!;
        TestAssert.Equal(first + 1, alternate.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", alternate.CreationAccent);
        rig.Wait(430);
        rig.Select("Disable Background");
        TestAssert.Equal(first + 2, rig.Current().InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", rig.Current().InstructionHud!.CreationAccent);
        rig.Wait(430);
        rig.Select("Disable");
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(first + 3, rig.Current().InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", rig.Current().InstructionHud!.CreationAccent);
        rig.Wait(430);
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Equal(first + 4, rig.Current().InstructionHud!.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", rig.Current().InstructionHud!.CreationAccent);
        rig.NoGameWrites();
    }

    public static void RainbowCreationRetainsItsCapturedModeAfterTheCurrentPaletteStopsRainbow() {
        var rig = Aio();
        rig.Select("Main Design Color");
        rig.Select("Smooth Rainbow");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Alternate Style");
        var rainbowCreation = rig.Current().InstructionHud!;
        TestAssert.Equal<bool?>(true, rainbowCreation.CreationRainbow);
        // The fixed field is configuration fallback, not the source's changing Rainbow RGB
        TestAssert.Equal("#3998FE", rainbowCreation.CreationAccent);
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Green");
        var stopped = rig.Current();
        TestAssert.True(!stopped.SmoothRainbowTheme, "The actual fixed callback must stop the current mode");
        TestAssert.Equal("#00FF00", stopped.Accent);
        TestAssert.Equal(rainbowCreation, stopped.InstructionHud);
        rig.Wait(430);
        TestAssert.Equal(rainbowCreation, rig.Current().InstructionHud);
        rig.NoGameWrites();
    }

    public static void WithdrawalAndReplacementCannotBorrowThePreviousCreationColor() {
        var rig = Aio();
        rig.Select("Main Design Color");
        rig.Select("Red");
        rig.Wait(430);
        var retained = rig.Current().InstructionHud!;
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Session.Change(LocalPlayerLife.Alive);
        var restored = rig.Current().InstructionHud!;
        TestAssert.Equal(retained.CreationRevision!.Value + 1, restored.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", restored.CreationAccent);
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        var replacement = rig.Current().InstructionHud!;
        TestAssert.Equal(restored.CreationRevision!.Value + 1, replacement.CreationRevision!.Value);
        TestAssert.Equal("#FF0000", replacement.CreationAccent);
        TestAssert.Equal(InstructionHudState.Absent, StateFocusProjection.Apply(rig.Current(), false).InstructionHud);
        TestAssert.Equal(replacement, rig.Current().InstructionHud);
        rig.Controller.RequestShutdown();
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.NoGameWrites();
    }

    public static void ExhaustedCreationCounterRejectsRefreshBeforeChangingTheCapturedColorOrFlags() {
        var rig = new InstructionHudBoundaryFixture(ulong.MaxValue - 1);
        rig.Open();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Select("Red");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        rig.Current();
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        var frame = rig.Current();
        int alternate = frame.Rows.ToList().FindIndex(row => row.Label == "Alternate Style");
        while (rig.Current().SelectedIndex != alternate) rig.Send(OverlayInput.Down);
        var before = rig.Current();
        TestAssert.Equal("#3998FE", before.InstructionHud!.CreationAccent);
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        var after = rig.Current();
        TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.InstructionHud, after.InstructionHud);
        TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
        TestAssert.True(after.MenuOptionCallback is null, "A failed reservation cannot start a new caller wait");
    }
}
