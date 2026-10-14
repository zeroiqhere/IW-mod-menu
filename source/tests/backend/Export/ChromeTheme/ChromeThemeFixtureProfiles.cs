using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Each graph follows actual owner commands and clocks; packet fields are never rewritten
internal static class ChromeThemeFixtureProfiles {
    public static IReadOnlyList<ChromeThemeFixtureExporter.Profile> Build() {
        var profiles = new List<ChromeThemeFixtureExporter.Profile>();
        void Save(string name, string graph, MenuChromeFixture rig, StateDocument? projection = null) {
            profiles.Add(new("chrome-theme-" + name + ".json", graph,
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, projection ?? rig.Current()));
            rig.NoGameWrites();
        }
        string[] designs = { "apparition", "classic", "native", "aio", "basic" };
        for (int design = 0; design < designs.Length; design++) {
            var rig = ChromeThemeControllerTests.DesignedPalette(design);
            string graph = designs[design];
            Save(graph + "-created", graph, rig);
            rig.Select("Red");
            Save(graph + "-red0", graph, rig);
            rig.Wait(180);
            Save(graph + "-red180", graph, rig);
            rig.Wait(250);
            Save(graph + "-red430", graph, rig);
            rig.Send(OverlayInput.Select);
            Save(graph + "-reselected0", graph, rig);
            rig.Wait(500);
            Save(graph + "-reselected500", graph, rig);
        }

        var life = ChromeThemeControllerTests.DesignedPalette(0);
        life.Select("Red");
        life.Wait(430);
        Save("focus-withdrawn", "lifetime", life, StateFocusProjection.Apply(life.Current(), false));
        life.Send(OverlayInput.Back);
        Save("parent", "lifetime", life);
        life.Select("Width Editor");
        Save("width", "lifetime", life);
        life.Send(OverlayInput.FinishWidthEditor);
        Save("width-return0", "lifetime", life);
        life.Wait(50);
        Save("width-return50", "lifetime", life);
        life.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(49));
        Save("width-return-rollback", "lifetime", life);
        life.Wait(150);
        life.Select("Menu Credits");
        Save("credits", "lifetime", life);
        life.Send(OverlayInput.FinishMenuCredits);
        life.Wait(150);
        life.Select("Reposition Menu");
        Save("position", "lifetime", life);
        life.Send(OverlayInput.FinishMenuPositionEditor);
        life.Wait(150);
        life.Send(OverlayInput.Toggle);
        Save("closed", "lifetime", life);
        life.Open();
        Save("reopened0", "lifetime", life);
        life.Wait(50);
        Save("reopened50", "lifetime", life);
        life.Wait(550);
        life.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Save("owner-retired", "lifetime", life);

        var overlaps = ChromeThemeControllerTests.DesignedPalette(0);
        for (int invocation = 0; invocation < 9; invocation++) {
            overlaps.Select("Red");
            if (invocation < 8) overlaps.Wait(430);
        }
        Save("overlap9", "overlaps", overlaps);
        overlaps.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
        Save("clock-recovery", "overlaps", overlaps);

        var rainbow = ChromeThemeControllerTests.DesignedPalette(4);
        rainbow.Select("Red");
        rainbow.Wait(430);
        rainbow.Select("Smooth Rainbow");
        rainbow.Wait(430);
        rainbow.Select("Red");
        Save("same-color-rainbow-stop", "rainbow", rainbow);
        rainbow.Wait(430);
        rainbow.Select("Smooth Rainbow");
        rainbow.Wait(430);
        rainbow.Send(OverlayInput.Toggle);
        rainbow.Open();
        rainbow.Wait(50);
        Save("rainbow-created", "rainbow", rainbow);
        rainbow.Wait(1000);
        rainbow.Select("Red");
        Save("delayed-fixed-stop", "rainbow", rainbow);

        var changed = ChromeThemeControllerTests.DesignedPalette(0);
        changed.Send(OverlayInput.Back);
        changed.Select("Menu Preferences");
        changed.Highlight("Design");
        changed.StageChoice(OverlayInput.NextChoice);
        changed.Send(OverlayInput.Select);
        Save("design0", "design", changed);
        changed.Wait(49);
        Save("design49", "design", changed);
        changed.Wait(1);
        Save("design50", "design", changed);
        changed.Wait(200);
        Save("design250", "design", changed);
        var unknown = ChromeThemeControllerTests.DesignedPalette(0);
        unknown.Select("Red");
        unknown.Wait(180);
        Save("known-life-before-withdrawal", "withdrawal", unknown);
        unknown.Session.Change(LocalPlayerLife.Unknown);
        Save("unknown-life", "withdrawal", unknown);
        unknown.Wait(250);
        unknown.Session.Change(LocalPlayerLife.Alive);
        Save("known-life-restored", "withdrawal", unknown);
        TestAssert.Equal(ChromeThemeFixtureExporter.ProfileCount, profiles.Count);
        return profiles.AsReadOnly();
    }
}
