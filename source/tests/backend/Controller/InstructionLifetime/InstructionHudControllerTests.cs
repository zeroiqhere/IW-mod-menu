using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Actual local callbacks are observed without engine dispatch or native reads
internal static class InstructionHudControllerTests {
    private static InstructionHudState Hud(StateDocument frame) {
        TestAssert.True(frame.InstructionHud is { Exists: true }, "A verified eligible actor must own an independently declared footer");
        return frame.InstructionHud!;
    }

    public static void KnownActorCreatesFooterBeforeOpeningAndClosingPreservesItsCreation() {
        var rig = new MenuChromeFixture();
        var closed = rig.Current();
        var initial = Hud(closed);
        TestAssert.True(!closed.IsOpen && closed.MenuChromeRevision is null, "Footer creation does not create or open the body");
        TestAssert.Equal<ulong?>(1, initial.CreationRevision);
        rig.SettleMain();
        TestAssert.Equal(initial, Hud(rig.Current()));
        rig.Send(OverlayInput.Toggle);
        TestAssert.Equal(initial, Hud(rig.Current()));
        rig.NoGameWrites();
    }

    public static void MenuPagesPaletteRowsAndSoftLocksDoNotRecreateTheIndependentFooter() {
        var rig = new MenuChromeFixture();
        var initial = Hud(rig.Current());
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Select("Red");
        rig.Wait(180);
        rig.Wait(250);
        TestAssert.Equal(initial.CreationRevision, Hud(rig.Current()).CreationRevision);
        rig.Send(OverlayInput.Back);
        rig.Select("Width Editor");
        TestAssert.Equal(initial.CreationRevision, Hud(rig.Current()).CreationRevision);
        rig.Send(OverlayInput.FinishWidthEditor);
        rig.Wait(150);
        TestAssert.Equal(initial.CreationRevision, Hud(rig.Current()).CreationRevision);
        rig.Session.State = rig.Session.State with { Busy = true };
        TestAssert.Equal(initial.CreationRevision, Hud(rig.Current()).CreationRevision);
        rig.NoGameWrites();
    }

    public static void EachObservedDeathRecreatesOnceWhileRevivalOnlyUpdatesHintEligibility() {
        var rig = new MenuChromeFixture();
        ulong first = Hud(rig.Current()).CreationRevision!.Value;
        rig.Session.Change(LocalPlayerLife.Dead);
        var dead = rig.Current();
        TestAssert.Equal(first + 1, Hud(dead).CreationRevision!.Value);
        TestAssert.Equal("dead", dead.LocalMenuEligibility!.Life);
        TestAssert.Equal(Hud(dead), Hud(rig.Current()));
        rig.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(first + 1, Hud(rig.Current()).CreationRevision!.Value);
        rig.Session.Change(LocalPlayerLife.Dead);
        TestAssert.Equal(first + 2, Hud(rig.Current()).CreationRevision!.Value);
        rig.NoGameWrites();
    }

    public static void PermissionUnknownAndActorReplacementCannotBorrowPreviousFooter() {
        var rig = new MenuChromeFixture();
        ulong first = Hud(rig.Current()).CreationRevision!.Value;
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Session.Change(LocalPlayerLife.Unknown);
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Session.Change(LocalPlayerLife.Alive);
        TestAssert.Equal(first + 1, Hud(rig.Current()).CreationRevision!.Value);
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        TestAssert.Equal(first + 2, Hud(rig.Current()).CreationRevision!.Value);
        rig.NoGameWrites();
    }

    public static void DesignRecreationCommitsAtActualDrawAndSameDesignKeepsCreationStyle() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        var initial = Hud(rig.Current());
        rig.Select("Design");
        rig.Wait(250);
        TestAssert.Equal(initial, Hud(rig.Current()));
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(initial, Hud(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        rig.Controller.ReadMenuPublicationPhase();
        TestAssert.Equal(initial.CreationRevision!.Value + 1, Hud(rig.Current()).CreationRevision!.Value);
        TestAssert.Equal("Classic", Hud(rig.Current()).CreationDesign);
        rig.Wait(200);
        TestAssert.Equal(initial.CreationRevision.Value + 1, Hud(rig.Current()).CreationRevision!.Value);
        rig.NoGameWrites();
    }

    public static void AlternateBackgroundAndEnableRecreateButAdaptiveResetAndDisableDoNotInventInstances() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Menu Instructions");
        ulong revision = Hud(rig.Current()).CreationRevision!.Value;
        rig.Select("Adaptive Position");
        rig.Wait(430);
        TestAssert.Equal(revision, Hud(rig.Current()).CreationRevision!.Value);
        rig.Select("Reset Position");
        rig.Wait(250);
        TestAssert.Equal(revision, Hud(rig.Current()).CreationRevision!.Value);
        rig.Select("Alternate Style");
        TestAssert.Equal(++revision, Hud(rig.Current()).CreationRevision!.Value);
        TestAssert.Equal<bool?>(true, Hud(rig.Current()).CreationAlternate);
        rig.Wait(430);
        rig.Select("Disable Background");
        TestAssert.Equal(++revision, Hud(rig.Current()).CreationRevision!.Value);
        TestAssert.Equal<bool?>(false, Hud(rig.Current()).CreationBackground);
        rig.Wait(430);
        rig.Select("Disable");
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(++revision, Hud(rig.Current()).CreationRevision!.Value);
        rig.NoGameWrites();
    }

    public static void FocusHidesTheDeclaredFooterWithoutChangingItsRetainedOwnerOrStyle() {
        var rig = new MenuChromeFixture();
        var owner = rig.Current();
        var initial = Hud(owner);
        TestAssert.Equal(InstructionHudState.Absent, StateFocusProjection.Apply(owner, false).InstructionHud);
        TestAssert.Equal(initial, Hud(rig.Current()));
        rig.Controller.RequestShutdown();
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        TestAssert.Equal(InstructionHudState.Absent, rig.Controller.ClosedState().InstructionHud);
        rig.NoGameWrites();
    }

    public static void LegacyPreviewAndUnavailableProductionKeepOmissionSeparateFromSuppression() {
        var preview = new Apparition.Backend.Controller.BackendController(42, new SessionBoundary());
        TestAssert.True(preview.Snapshot().InstructionHud is null, "Legacy factories do not acquire new source footer behavior");
        var rig = new MenuChromeFixture();
        rig.Session.RequiresLocalMenuAdmission = true;
        rig.Session.AdmissionUnavailable = true;
        TestAssert.Equal(InstructionHudState.Absent, rig.Current().InstructionHud);
        rig.Session.AdmissionUnavailable = false;
        TestAssert.True(Hud(rig.Current()).CreationRevision > 0, "A fresh positive receipt can create the source footer");
        rig.Session.State = rig.Session.State with { Preview = true };
        TestAssert.True(rig.Current().InstructionHud is null, "Inert preview preserves legacy omission");
        rig.NoGameWrites();
    }
}
