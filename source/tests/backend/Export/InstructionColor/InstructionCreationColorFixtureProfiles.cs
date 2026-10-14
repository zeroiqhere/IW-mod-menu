using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// One real controller owns the complete chronology, including callbacks, redraws and actor facts
internal static class InstructionCreationColorFixtureProfiles {
    public static IReadOnlyList<InstructionCreationColorFixtureExporter.Profile> Build() {
        var rig = new InstructionHudExportFixture();
        var profiles = new List<InstructionCreationColorFixtureExporter.Profile>();
        void Add(string name) {
            rig.NoGameWrites();
            profiles.Add(new("instruction-color-" + name + ".json",
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));
        }
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        for (int choice = 0; choice < 3; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(50);
        rig.Wait(200);
        Add("aio-created-blue");
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Red");
        Add("fixed-red0");
        rig.Wait(180);
        Add("fixed-red180");
        rig.Wait(250);
        Add("fixed-red430");
        // This later observation is not a claim about an unproved engine interpolation curve
        rig.Wait(570);
        Add("fixed-red1000");
        rig.Send(OverlayInput.Back);
        Add("parent");
        rig.Wait(250);
        rig.Send(OverlayInput.Toggle);
        Add("closed");
        rig.Open();
        Add("reopen0");
        rig.Wait(50);
        Add("reopen50");
        rig.Wait(550);
        Add("reopen600");
        rig.Select("Menu Instructions");
        rig.Select("Alternate Style");
        Add("alternate-red");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Smooth Rainbow");
        Add("rainbow0");
        rig.Wait(430);
        Add("rainbow430");
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Alternate Style");
        Add("rainbow-recreated");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Green");
        Add("fixed-green0");
        rig.Wait(180);
        Add("fixed-green180");
        rig.Wait(250);
        Add("fixed-green430");
        rig.Wait(570);
        // A first native receipt here must still use the immutable Rainbow creation mode
        Add("fixed-green1000");
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Disable Background");
        Add("background-disabled");
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        Add("background-recreated");
        rig.Wait(430);
        rig.Select("Disable");
        Add("disabled");
        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        Add("re-enabled");
        rig.Wait(430);
        rig.Send(OverlayInput.Toggle);
        rig.Session.Change(LocalPlayerLife.Dead);
        Add("dead-closed");
        rig.Session.Change(LocalPlayerLife.Alive);
        Add("alive-again");
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        Add("permission-lost");
        rig.Session.Change(LocalPlayerLife.Alive);
        Add("permission-restored");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add("owner-replaced");

        rig.Open();
        rig.Wait(50);
        rig.Wait(550);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Smooth Rainbow");
        rig.Wait(430);
        Add("gap-rainbow");
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Design");
        rig.StageChoice(OverlayInput.PreviousChoice);
        rig.StageChoice(OverlayInput.PreviousChoice);
        rig.Send(OverlayInput.Select);
        Add("design-out0");
        rig.Wait(49);
        Add("design-out49");
        rig.Wait(1);
        Add("design-out50");
        TestAssert.Equal(InstructionCreationColorFixtureExporter.ProfileCount, profiles.Count);
        return profiles.AsReadOnly();
    }
}
