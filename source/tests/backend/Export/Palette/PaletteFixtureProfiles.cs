using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Source choices are exercised through actual navigation, draft commits and owned caller waits
internal static class PaletteFixtureProfiles {
    private static readonly string[] Designs = { "Apparition", "Classic", "Native", "AIO", "Basic" };

    public static IReadOnlyList<PaletteFixtureExporter.Profile> Build() {
        var profiles = new List<PaletteFixtureExporter.Profile>();
        void Add(string name, MenuChromeFixture rig) {
            rig.NoGameWrites();
            var frame = rig.Current();
            if (frame.Context != "CONNECTED" || rig.Session.State.Preview)
                throw new InvalidOperationException("Palette evidence requires a connected inert source owner");
            profiles.Add(new(name + ".json", rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, frame));
        }
        for (int designIndex = 0; designIndex < Designs.Length; designIndex++) {
            var rig = ReadyPalette(designIndex);
            string prefix = "palette-" + Designs[designIndex].ToLowerInvariant();
            Add(prefix + "-created", rig);
            for (int color = 0; color < 36; color++) {
                // Actual row labels are producer input; the independent source oracle checks them
                rig.Highlight(rig.Current().Rows[color].Label);
                rig.Send(OverlayInput.Select);
                rig.Wait(180);
                rig.Wait(250);
                // The endpoint packet is later than both the caller wait and source color durations
                rig.Wait(570);
                Add(prefix + "-" + color.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), rig);
            }
        }

        var black = ReadyPalette(0);
        black.Select("Black");
        Add("palette-apparition-black-0", black);
        black.Wait(179);
        Add("palette-apparition-black-179", black);
        black.Wait(1);
        Add("palette-apparition-black-180", black);
        black.Wait(249);
        Add("palette-apparition-black-429", black);
        black.Wait(1);
        Add("palette-apparition-black-430", black);

        var uncolored = ReadyPalette(0);
        uncolored.Send(OverlayInput.Back);
        uncolored.Select("Menu Preferences");
        uncolored.Select("Colored Cursor");
        uncolored.Wait(180);
        uncolored.Wait(250);
        uncolored.Send(OverlayInput.Back);
        uncolored.Select("Main Design Color");
        uncolored.Select("Black");
        uncolored.Wait(180);
        uncolored.Wait(250);
        uncolored.Wait(570);
        Add("palette-apparition-cursor-off", uncolored);

        var textColor = ReadyPalette(0);
        textColor.Send(OverlayInput.Back);
        textColor.Select("Menu Preferences");
        textColor.Highlight("Bool Display");
        textColor.StageChoice(OverlayInput.NextChoice);
        textColor.StageChoice(OverlayInput.NextChoice);
        textColor.Send(OverlayInput.Select);
        textColor.Wait(250);
        textColor.Send(OverlayInput.Back);
        textColor.Select("Main Design Color");
        textColor.Select("Black");
        textColor.Wait(180);
        textColor.Wait(250);
        textColor.Wait(570);
        Add("palette-apparition-textcolor-on", textColor);

        if (profiles.Count != PaletteFixtureExporter.ProfileCount)
            throw new InvalidOperationException("The actual palette graph is incomplete");
        return profiles.AsReadOnly();
    }

    private static MenuChromeFixture ReadyPalette(int designIndex) {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        // Even the same default design owns its real source caller wait
        for (int choice = 0; choice < designIndex; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(50);
        rig.Wait(200);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        return rig;
    }
}
