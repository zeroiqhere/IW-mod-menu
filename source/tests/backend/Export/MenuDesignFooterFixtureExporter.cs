using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Adaptive profiles use actual supported controls before the source Design commit
internal static class MenuDesignFooterFixtureExporter
{
    public static void Add(IDictionary<string, StateDocument> fixtures)
    {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Instructions");
        rig.Select("Adaptive Position");
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Stage("AIO");
        rig.Send(OverlayInput.Select);

        // Both packets observe the same missing body and existing captured footer paint
        fixtures.Add("preferences-design-adaptive-aio-rebuild.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49));
        fixtures.Add("preferences-design-adaptive-aio-pre-rebuild.json", rig.Current());
        rig.NoGameWork();
    }
}
