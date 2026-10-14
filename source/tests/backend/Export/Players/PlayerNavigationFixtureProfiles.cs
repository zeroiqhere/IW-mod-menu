using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;

namespace Backend.Tests;

// Every packet is a real controller publication over explicit inert connected session facts
internal static class PlayerNavigationFixtureProfiles {
    internal static IReadOnlyList<PlayerNavigationFixtureExporter.Profile> Build() {
        var profiles = new List<PlayerNavigationFixtureExporter.Profile>();
        ScopedOpeningFixture Ready() {
            var rig = new ScopedOpeningFixture();
            rig.Session.State = rig.Session.State with { Players = new[] { new OverlayPlayer(1, "Friend"),
                new OverlayPlayer(2, ScopedOpeningFixture.Actor.Name) } };
            rig.Open(OverlayInput.OpenMain); Advance(rig, 50); Advance(rig, 550); return rig;
        }
        void Add(string name, string graph, ScopedOpeningFixture rig) {
            var frame = rig.Controller.Snapshot();
            TestAssert.True(!rig.Session.State.Preview && rig.Session.State.Connected, "Packets require an actual connected fake source");
            profiles.Add(new("player-navigation-" + name + ".json", graph,
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, frame));
        }
        var peer = Ready(); PlayerMenuTestNavigation.Select(peer, "Players Menu");
        PlayerMenuTestNavigation.Select(peer, "Friend"); peer.Controller.StartQueuedAction();
        Add("peer-options", "peer", peer); Advance(peer, 250);
        PlayerMenuTestNavigation.Select(peer, "Basic Scripts"); Add("peer-basic-before", "peer", peer);
        peer.Send(OverlayInput.Select); peer.Controller.StartQueuedAction();
        // The controlled adapter changes current facts; the controller alone owns copied paint and waits
        peer.Session.State = peer.Session.State with { TeammateGodmodeValue = "OFF" };
        Add("peer-boolean0", "peer", peer); Advance(peer, 179); Add("peer-boolean179", "peer", peer);
        Advance(peer, 1); Add("peer-boolean180", "peer", peer);
        Advance(peer, 249); Add("peer-boolean429", "peer", peer);
        Advance(peer, 1); Add("peer-boolean430", "peer", peer);
        PlayerMenuTestNavigation.Select(peer, "Unlimited Ammo"); Add("peer-unsupported-ammo", "peer", peer);
        peer.Send(OverlayInput.Back); Add("peer-parent", "peer", peer);
        peer.Session.State = peer.Session.State with { TeammateGodmodeValue = "ON" };
        Advance(peer, 250);
        peer.Send(OverlayInput.Toggle); peer.Open(OverlayInput.OpenQuick); Advance(peer, 550);
        Add("quick-forced-self", "peer", peer);
        peer.Send(OverlayInput.Back); Advance(peer, 250); peer.Open(OverlayInput.OpenMain); Advance(peer, 50); Advance(peer, 550);
        Add("main-peer-return", "peer", peer);
        PlayerMenuTestNavigation.Select(peer, "Basic Scripts");
        var original = peer.Session.State.SelectedPlayerTarget!;
        peer.Session.State = peer.Session.State with { SelectedPlayerTarget = new(original.Identity with { Guid = "FFFFFFFFFFFFFFFF" },
            original.Route, original.OwnerToken, original.SelectionRevision + 1) };
        Add("stale-peer", "peer", peer);

        var self = Ready(); PlayerMenuTestNavigation.Select(self, "Players Menu");
        PlayerMenuTestNavigation.Select(self, ScopedOpeningFixture.Actor.Name); self.Controller.StartQueuedAction();
        Add("self-options", "self", self); Advance(self, 250);
        PlayerMenuTestNavigation.Select(self, "Basic Scripts"); Add("self-basic-before", "self", self);
        self.Send(OverlayInput.Select); self.Controller.StartQueuedAction();
        self.Session.State = self.Session.State with { PersonalGodmodeValue = "ON" };
        Add("self-boolean0", "self", self); Advance(self, 180); Add("self-boolean180", "self", self);
        Advance(self, 250); Add("self-boolean430", "self", self);
        self.Send(OverlayInput.Back); Advance(self, 250);
        // Authentication still supplies a browsing receipt while the personal flags owner is unavailable
        self.Session.State = self.Session.State with { PersonalGodmodeIdentity = null,
            CanPersonalGodmode = false, CanRestorePersonalGodmode = true };
        Add("self-metadata-options", "self", self);
        PlayerMenuTestNavigation.Select(self, "Basic Scripts"); Add("self-metadata-basic", "self", self);
        var all = Ready(); PlayerMenuTestNavigation.Select(all, "All Players Menu"); Add("allplayers-source", "allplayers", all);
        TestAssert.Equal(PlayerNavigationFixtureExporter.ProfileCount, profiles.Count);
        return profiles.AsReadOnly();
    }

    private static void Advance(ScopedOpeningFixture rig, int milliseconds) {
        rig.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds)); rig.Controller.Snapshot();
    }
}
