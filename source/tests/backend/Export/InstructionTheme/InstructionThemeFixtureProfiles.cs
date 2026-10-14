using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Independent graphs use actual controller commands and inert authenticated source observations
internal static class InstructionThemeFixtureProfiles {
    public static IReadOnlyList<InstructionThemeFixtureExporter.Profile> Build() {
        var profiles = new List<InstructionThemeFixtureExporter.Profile>();
        var rig = Palette();
        void Capture(string name, string graph = "retained") => profiles.Add(new(
            "instruction-theme-" + name + ".json", graph, rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));

        Capture("empty");
        rig.Select("Red");
        Capture("red0");
        rig.Wait(179);
        Capture("red179");
        rig.Wait(1);
        Capture("red180");
        rig.Wait(250);
        Capture("red430");
        rig.Send(OverlayInput.Select);
        Capture("same-red0");
        rig.Wait(70);
        Capture("same-red70");
        rig.Wait(360);
        Capture("same-red430");
        rig.Select("Smooth Rainbow");
        Capture("rainbow0");
        rig.Wait(430);
        Capture("rainbow430");
        rig.Select("Red");
        Capture("stop-red0");
        rig.Wait(70);
        Capture("stop-red70");
        rig.Wait(430);
        Capture("stop-red500");

        // Rows, close, and focus change authority without inventing a footer recreation
        rig.Send(OverlayInput.Back);
        Capture("parent");
        rig.Wait(250);
        rig.Send(OverlayInput.Toggle);
        Capture("closed");
        profiles.Add(new("instruction-theme-focus-withdrawn.json", "retained",
            rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, StateFocusProjection.Apply(rig.Current(), false)));
        rig.Open();
        Capture("reopen0");
        rig.Wait(50);
        Capture("reopen50");
        rig.Wait(550);
        Capture("reopen600");

        rig.Select("Menu Preferences");
        rig.Highlight("Design");
        for (int choice = 0; choice < 3; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        Capture("design-aio0");
        rig.Wait(49);
        Capture("design-aio49");
        rig.Wait(1);
        Capture("design-aio50");
        rig.Wait(200);
        Capture("design-aio250");
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Green");
        Capture("green-background0");
        rig.Wait(430);
        Capture("green-background430");

        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Disable Background");
        Capture("no-background-created");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Red");
        Capture("no-background-red0");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Capture("owner-replaced");
        rig.NoGameWrites();

        // A second real controller demonstrates bounded overlap without rewriting exported packets
        rig = Palette();
        for (int invocation = 0; invocation < 9; invocation++) {
            rig.Select("Red");
            if (invocation == 7) Capture("overlap-eight", "overlap");
            if (invocation < 8) rig.Wait(430);
        }
        Capture("overlap-nine0", "overlap");
        rig.Wait(499);
        Capture("overlap-nine499", "overlap");
        rig.Wait(1);
        Capture("overlap-nine500", "overlap");
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        Capture("permission-withdrawn", "overlap");
        rig.Session.Change(LocalPlayerLife.Alive);
        Capture("permission-restored", "overlap");
        rig.NoGameWrites();
        return profiles.AsReadOnly();
    }

    private static MenuChromeFixture Palette() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        return rig;
    }
}
