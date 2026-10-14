using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Graph clocks and states come from the same real controller visit, without packet rewrites
internal static class FooterWithdrawalFixtureProfiles {
    public static IReadOnlyList<FooterWithdrawalFixtureExporter.Profile> Build() {
        var profiles = new List<FooterWithdrawalFixtureExporter.Profile>();
        void Add(MenuChromeFixture rig, string graph, string stage) {
            var frame = rig.Current();
            rig.NoGameWrites();
            profiles.Add(new("footer-withdrawal-" + graph + "-" + stage + ".json", graph,
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, frame));
        }
        foreach (bool closed in new[] { false, true }) {
            var rig = FooterWithdrawalControllerTests.Palette();
            string graph = closed ? "closed" : "open";
            if (closed) {
                rig.Wait(250);
                rig.Send(OverlayInput.Toggle);
            }
            Add(rig, graph, "known");
            rig.Session.Change(LocalPlayerLife.Unknown);
            Add(rig, graph, "unknown");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            rig.Session.Change(LocalPlayerLife.Alive);
            Add(rig, graph, "restored");
        }

        var life = FooterWithdrawalControllerTests.Palette();
        Add(life, "life", "alive");
        life.Session.Change(LocalPlayerLife.Unknown);
        Add(life, "life", "unknown-alive");
        life.Session.Change(LocalPlayerLife.Dead);
        Add(life, "life", "dead");
        life.Session.Change(LocalPlayerLife.Unknown);
        Add(life, "life", "unknown-dead");
        life.Session.Change(LocalPlayerLife.Dead);
        Add(life, "life", "dead-again");
        life.Session.Change(LocalPlayerLife.Alive);
        Add(life, "life", "revived");

        foreach (bool ready in new[] { false, true }) {
            var rig = FooterWithdrawalControllerTests.Palette();
            rig.Wait(250);
            rig.Send(OverlayInput.Back);
            rig.Select("Menu Preferences");
            rig.Highlight("Design");
            rig.StageChoice(OverlayInput.NextChoice);
            rig.Send(OverlayInput.Select);
            string graph = ready ? "ready" : "delayed";
            Add(rig, graph, "started");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
            rig.Session.Change(LocalPlayerLife.Unknown);
            Add(rig, graph, "unknown25");
            if (ready) {
                rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
                Add(rig, graph, "drawn-unknown50");
                rig.Clock.Advance(TimeSpan.FromMilliseconds(10));
                rig.Session.Change(LocalPlayerLife.Alive);
                Add(rig, graph, "restored60");
            } else {
                rig.Clock.Advance(TimeSpan.FromMilliseconds(5));
                rig.Session.Change(LocalPlayerLife.Alive);
                Add(rig, graph, "restored30");
                rig.Clock.Advance(TimeSpan.FromMilliseconds(20));
                Add(rig, graph, "drawn50");
            }
        }

        foreach (string cause in new[] { "permission", "provider", "owner", "process" }) {
            var rig = FooterWithdrawalControllerTests.Palette();
            rig.Session.Change(LocalPlayerLife.Unknown);
            Add(rig, cause, "unknown");
            // The inert copied provider fact changes process ownership without any native read or packet edit
            var observationField = typeof(ScopedOpeningFixture.OpeningSession).GetField("observation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            if (cause == "permission") rig.Session.Change(LocalPlayerLife.Unknown, hasMenu: false);
            if (cause == "provider") rig.Session.AdmissionUnavailable = true;
            if (cause == "owner") rig.Session.Change(LocalPlayerLife.Unknown, replaceOwner: true);
            if (cause == "process") observationField.SetValue(rig.Session, rig.Session.Admission.Observation with { ProcessId = 43 });
            Add(rig, cause, "revoked");
            if (cause == "process") observationField.SetValue(rig.Session, rig.Session.Admission.Observation with { ProcessId = 42 });
            rig.Session.AdmissionUnavailable = false;
            rig.Session.Change(LocalPlayerLife.Alive);
            Add(rig, cause, "restored");
        }

        var disable = FooterWithdrawalControllerTests.Palette();
        disable.Wait(250);
        disable.Send(OverlayInput.Back);
        disable.Select("Menu Instructions");
        disable.Session.Change(LocalPlayerLife.Unknown);
        Add(disable, "disable", "unknown");
        disable.Session.Change(LocalPlayerLife.Alive);
        Add(disable, "disable", "restored");
        disable.Select("Disable");
        Add(disable, "disable", "disabled");
        disable.Wait(430);
        disable.Send(OverlayInput.Select);
        Add(disable, "disable", "enabled");

        var prefix = FooterWithdrawalControllerTests.Palette();
        prefix.Wait(250);
        for (int invocation = 1; invocation < 9; invocation++) {
            prefix.Select("Red");
            prefix.Wait(430);
        }
        Add(prefix, "prefix", "pruned");
        prefix.Session.Change(LocalPlayerLife.Unknown);
        Add(prefix, "prefix", "unknown");
        prefix.Clock.Advance(TimeSpan.FromMilliseconds(100));
        prefix.Session.Change(LocalPlayerLife.Alive);
        Add(prefix, "prefix", "restored");
        prefix.Session.Change(LocalPlayerLife.Unknown);
        Add(prefix, "prefix", "unknown-again");
        prefix.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(150));
        Add(prefix, "prefix", "rollback-unknown");
        prefix.Session.Change(LocalPlayerLife.Alive);
        Add(prefix, "prefix", "recovered-timing");

        var rainbow = new MenuChromeFixture();
        rainbow.SettleMain();
        rainbow.Select("Menu Customization");
        rainbow.Select("Main Design Color");
        rainbow.Select("Smooth Rainbow");
        rainbow.Wait(430);
        rainbow.Send(OverlayInput.Back);
        rainbow.Select("Menu Instructions");
        rainbow.Select("Alternate Style");
        rainbow.Wait(430);
        Add(rainbow, "rainbow", "created");
        rainbow.Send(OverlayInput.Back);
        rainbow.Select("Main Design Color");
        rainbow.Select("Red");
        rainbow.Wait(180);
        Add(rainbow, "rainbow", "fixed180");
        rainbow.Session.Change(LocalPlayerLife.Unknown);
        Add(rainbow, "rainbow", "unknown");
        rainbow.Clock.Advance(TimeSpan.FromMilliseconds(250));
        rainbow.Session.Change(LocalPlayerLife.Alive);
        Add(rainbow, "rainbow", "restored430");
        TestAssert.Equal(FooterWithdrawalFixtureExporter.ProfileCount, profiles.Count);
        return profiles.AsReadOnly();
    }
}
