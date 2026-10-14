using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// New profiles use only the production controller and a private monotonic test clock
internal static class StealthFixtureExporter {
    internal const int TotalFixtureCount = 158;

    internal static void Add(IDictionary<string, StateDocument> fixtures) {
        var refresh = new MenuDesignTests.Rig();
        refresh.Open();
        refresh.Select("Stealth UI");
        CaptureRefresh(fixtures, refresh);
        refresh.Select("Stealth UI");
        refresh.Clock.Advance(TimeSpan.FromMilliseconds(430));
        fixtures.Add("stealth-disabled-unlocked.json", refresh.Current());
        refresh.NoGameWork();

        // A retained normal opening can outlast its independent input wait
        var retained = StealthOpeningTests.Retained("Stealth UI");
        retained.Send(OverlayInput.Select);
        fixtures.Add("stealth-retained-opening-pending.json", retained.Current());
        retained.Clock.Advance(TimeSpan.FromMilliseconds(179));
        fixtures.Add("stealth-retained-opening-pre-refresh.json", retained.Current());
        retained.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("stealth-retained-opening-cooldown.json", retained.Current());
        retained.NoGameWork();

        var on = new MenuDesignTests.Rig();
        on.Open();
        on.Select("Option Counter");
        CaptureRebuild(fixtures, "counter-on", on);
        on.Select("Option Counter");
        CaptureRebuild(fixtures, "counter-off", on);
        on.NoGameWork();

        var stealth = new MenuDesignTests.Rig();
        stealth.Open();
        stealth.Select("Option Counter");
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(250));
        stealth.Current();
        stealth.Select("Stealth UI");
        CaptureRebuild(fixtures, "stealth-rebuild", stealth);
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(199));
        fixtures.Add("stealth-opening-last.json", stealth.Current());
        stealth.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("stealth-opening-complete.json", stealth.Current());
        stealth.Highlight("Bool Display");
        stealth.Send(OverlayInput.Select);
        fixtures.Add("stealth-boxes-rejected.json", stealth.Current());
        stealth.Send(OverlayInput.PreviousChoice);
        stealth.Send(OverlayInput.Select);
        fixtures.Add("stealth-text-color.json", stealth.Current());
        stealth.NoGameWork();
    }

    private static void CaptureRefresh(IDictionary<string, StateDocument> fixtures, MenuDesignTests.Rig rig) {
        fixtures.Add("stealth-refresh-pending.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        fixtures.Add("stealth-refresh-pre-refresh.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("stealth-refresh-cooldown.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
        fixtures.Add("stealth-refresh-pre-unlock.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("stealth-refresh-unlocked.json", rig.Current());
    }

    private static void CaptureRebuild(IDictionary<string, StateDocument> fixtures, string prefix, MenuDesignTests.Rig rig) {
        fixtures.Add(prefix + "-rebuild.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49));
        fixtures.Add(prefix + "-pre-rebuild.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add(prefix + "-cooldown.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(199));
        fixtures.Add(prefix + "-pre-unlock.json", rig.Current());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add(prefix + "-unlocked.json", rig.Current());
    }
}
