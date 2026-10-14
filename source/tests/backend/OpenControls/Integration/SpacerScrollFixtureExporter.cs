using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Appearance choices and spacer movement both use the production controller routes
internal static class SpacerScrollFixtureExporter {
    internal const int TotalFixtureCount = 192;

    internal static void Add(IDictionary<string, StateDocument> profiles) {
        foreach (string mode in new[] { "ordinary", "counter", "stealth" }) {
            var rig = OpeningControlsFixtureExporter.Create();
            rig.Open();
            if (mode != "ordinary") {
                rig.Send(OverlayInput.Back);
                rig.Select("Menu Preferences");
                rig.Select(mode == "counter" ? "Option Counter" : "Stealth UI");
                // Settle each distinct source handler before opening its retained parent editor
                rig.Wait(mode == "counter" ? 250 : 430);
                rig.Send(OverlayInput.Back);
                rig.Select("Open Controls");
            }
            rig.Highlight("Bind Slot");
            Capture("down-before");
            rig.Send(OverlayInput.Down);
            Capture("down-after");
            Capture("up-before");
            rig.Send(OverlayInput.Up);
            Capture("up-after");
            rig.NoGameWork();

            void Capture(string suffix) {
                profiles.Add("spacer-scroll-" + mode + "-" + suffix + ".json", rig.Current());
            }
        }
    }
}
