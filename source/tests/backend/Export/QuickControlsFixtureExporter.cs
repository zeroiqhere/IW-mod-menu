using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These profiles drive the real controller using one verified offline saved-binding packet
internal static class QuickControlsFixtureExporter {
    internal const int ProfileCount = 22;
    internal const int TotalFixtureCount = OpeningControlsFixtureExporter.TotalFixtureCount + ProfileCount;

    internal static void Add(IDictionary<string, StateDocument> fixtures) {
        var rig = OpeningControlsFixtureExporter.Create();
        rig.Open();
        Capture("default");
        rig.Highlight("Menu");
        rig.Send(OverlayInput.NextChoice);
        Capture("menu-draft");
        rig.Send(OverlayInput.Select);
        Capture("menu-applied");
        rig.Wait(250);
        rig.SetSlot(2);
        Capture("quick-slot2");

        // Actual policy changes precede the source checkbox redraw and input unlock
        rig.Select("Use");
        Capture("use-pending");
        rig.Wait(179);
        Capture("use-pre-refresh");
        rig.Wait(1);
        Capture("use-redraw");
        rig.Wait(249);
        Capture("use-pre-unlock");
        rig.Wait(1);
        Capture("use-unlocked");
        rig.Select("Aim");
        Capture("duplicate-pending");
        rig.Wait(430);
        rig.Select("None");
        Capture("none-pending");
        rig.Wait(430);
        Capture("none-unlocked");

        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Select("Disable Quick Menu");
        Capture("disable-pending");
        rig.Wait(180);
        Capture("disable-redraw");
        rig.Wait(250);
        Capture("disable-unlocked");
        rig.Select("Disable Quick Menu");
        Capture("reenable-pending");
        rig.Wait(430);
        Capture("reenable-unlocked");
        Capture("hidden", true);
        rig.Send(OverlayInput.Toggle);
        Capture("closed");
        rig.NoGameWork();

        // Missing Secondary Offhand keeps Main-only configuration instead of inventing a Quick tuple
        var fallback = OpeningControlsFixtureExporter.Create(partial: true);
        fallback.Open();
        fixtures.Add("quick-controls-main-only.json", fallback.Current());
        fallback.NoGameWork();

        // Same-target Use pauses input while the previously drawn rows finish their independent fade
        var retained = OpeningControlsFixtureExporter.Create();
        retained.Open();
        retained.Highlight("Menu");
        retained.Send(OverlayInput.Toggle);
        retained.Send(OverlayInput.Toggle);
        retained.Wait(50);
        retained.Wait(550);
        retained.Send(OverlayInput.Select);
        fixtures.Add("quick-controls-selector-opening-wait.json", retained.Current());
        retained.Wait(250);
        fixtures.Add("quick-controls-selector-opening-unlocked.json", retained.Current());
        retained.NoGameWork();

        void Capture(string name, bool hidden = false) {
            var frame = rig.Current();
            fixtures.Add("quick-controls-" + name + ".json", hidden ? StateFocusProjection.Apply(frame, false) : frame);
        }
    }
}
