using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Each declared subgraph follows actual commands and copied cached actor observations
internal static class InstructionHudFixtureProfiles {
    public static IReadOnlyList<InstructionHudFixtureExporter.Profile> Build() {
        var profiles = new List<InstructionHudFixtureExporter.Profile>();
        void Add(InstructionHudExportFixture rig, string name, bool hideFocus = false) {
            rig.NoGameWrites();
            var frame = rig.Current();
            if (hideFocus) frame = StateFocusProjection.Apply(frame, false);
            profiles.Add(new("instruction-hud-" + name + ".json",
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, frame));
        }
        var rig = new InstructionHudExportFixture();
        Add(rig, "alive-closed");
        rig.Open();
        Add(rig, "main-rebuild0");
        rig.Wait(50);
        Add(rig, "main-drawn50");
        rig.Wait(550);
        Add(rig, "main-ready600");
        rig.Send(OverlayInput.Toggle);
        Add(rig, "main-closed");
        rig.Open(OverlayInput.OpenQuick);
        Add(rig, "quick-open0");
        rig.Wait(550);
        Add(rig, "quick-ready550");
        rig.Session.Change(LocalPlayerLife.Dead);
        Add(rig, "quick-dead");
        rig.Send(OverlayInput.JumpBack);
        Add(rig, "dead-closed");
        rig.Session.Change(LocalPlayerLife.Alive);
        Add(rig, "alive-again-closed");
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        Add(rig, "permission-lost");
        rig.Session.Change(LocalPlayerLife.Alive);
        Add(rig, "permission-restored");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add(rig, "owner-replaced");

        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        Add(rig, "design-rebuild0");
        rig.Wait(50);
        Add(rig, "design-drawn50");
        rig.Wait(200);
        Add(rig, "design-ready250");
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Alternate Style");
        Add(rig, "alternate");
        rig.Wait(430);
        rig.Select("Disable Background");
        Add(rig, "background-off");
        rig.Wait(430);
        rig.Select("Disable");
        Add(rig, "disabled");
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        Add(rig, "re-enabled");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Width Editor");
        Add(rig, "width-soft-lock");
        rig.Send(OverlayInput.FinishWidthEditor);
        Add(rig, "width-return0");
        rig.Wait(150);
        Add(rig, "width-return150");
        Add(rig, "focus-hidden", hideFocus: true);

        // This separate actor chronology disables Quick through the actual preference callback
        var quickDisabled = new InstructionHudExportFixture();
        quickDisabled.SettleMain();
        quickDisabled.Preferences();
        quickDisabled.Select("Disable Quick Menu");
        quickDisabled.Wait(430);
        quickDisabled.Send(OverlayInput.Toggle);
        quickDisabled.Session.Change(LocalPlayerLife.Dead);
        Add(quickDisabled, "dead-closed-quick-disabled");

        var death = new InstructionHudExportFixture();
        death.BeginClassicDesign();
        death.Clock.Advance(TimeSpan.FromMilliseconds(25));
        death.Session.Change(LocalPlayerLife.Dead);
        Add(death, "design-death25");
        death.Wait(25);
        Add(death, "design-death-drawn50");

        var unknown = new InstructionHudExportFixture();
        unknown.BeginClassicDesign();
        unknown.Clock.Advance(TimeSpan.FromMilliseconds(25));
        unknown.Session.Change(LocalPlayerLife.Unknown);
        Add(unknown, "design-unknown25");
        unknown.Clock.Advance(TimeSpan.FromMilliseconds(5));
        unknown.Session.Change(LocalPlayerLife.Alive);
        Add(unknown, "design-recovered30");
        unknown.Wait(20);
        Add(unknown, "design-recovered-drawn50");

        // Actual permission or owner retirement cancels the body caller instead of borrowing its force
        var lost = new InstructionHudExportFixture();
        lost.BeginClassicDesign();
        lost.Clock.Advance(TimeSpan.FromMilliseconds(25));
        lost.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        Add(lost, "design-permission-lost25");
        lost.Clock.Advance(TimeSpan.FromMilliseconds(5));
        lost.Session.Change(LocalPlayerLife.Alive);
        Add(lost, "design-permission-restored30");
        lost.Wait(20);
        Add(lost, "design-permission-canceled50");
        var replaced = new InstructionHudExportFixture();
        replaced.BeginClassicDesign();
        replaced.Clock.Advance(TimeSpan.FromMilliseconds(25));
        replaced.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add(replaced, "design-owner-replaced25");
        replaced.Wait(25);
        Add(replaced, "design-owner-canceled50");
        TestAssert.Equal(InstructionHudFixtureExporter.ProfileCount, profiles.Count);
        return profiles.AsReadOnly();
    }
}
