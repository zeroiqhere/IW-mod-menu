using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// These profiles publish the real scoped controller without linking a game transport
internal static class ScopedOpeningFixtureProfiles {
    internal const int ProfileCount = 15;
    internal static readonly IReadOnlySet<string> ClosedProfiles = new HashSet<string>(StringComparer.Ordinal) {
        "opening-closed.json", "opening-pending.json", "opening-quick-dead-closed.json",
        "opening-main-dead-closed.json", "opening-quick-owner-retired.json", "opening-quick-focus-hidden.json"
    };

    public static void Add(IDictionary<string, StateDocument> fixtures) {
        ScopedOpeningFixture NewRig() {
            var rig = new ScopedOpeningFixture();
            rig.Session.State = rig.Session.State with { Preview = true };
            return rig;
        }
        void Add(string name, ScopedOpeningFixture rig) {
            fixtures.Add("opening-" + name + ".json", rig.Controller.Snapshot());
            if (rig.Session.Executed.Count != 0) throw new InvalidOperationException("Opening profiles cannot execute game features");
        }
        var quick = NewRig();
        Add("closed", quick);
        quick.Queue(OverlayInput.OpenQuick);
        Add("pending", quick);
        quick.Controller.StartQueuedAction(); quick.Finish();
        Add("quick-drawn", quick);
        fixtures.Add("opening-quick-focus-hidden.json", StateFocusProjection.Apply(quick.Controller.Snapshot(), false));
        quick.Clock.Advance(TimeSpan.FromMilliseconds(549)); Add("quick-wait", quick);
        quick.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("quick-ready", quick);
        quick.Send(OverlayInput.Down); Add("quick-scrolled", quick);
        quick.Session.Change(LocalPlayerLife.Dead); Add("quick-dead", quick);
        quick.Queue(OverlayInput.JumpBack); Add("quick-dead-closed", quick);
        quick.Open(OverlayInput.OpenQuick); Add("quick-dead-reopened", quick);

        var main = NewRig(); main.Open(OverlayInput.OpenMain); Add("main-rebuild", main);
        main.Clock.Advance(TimeSpan.FromMilliseconds(50)); main.Controller.Snapshot();
        main.Session.Change(LocalPlayerLife.Dead);
        main.Clock.Advance(TimeSpan.FromMilliseconds(549)); Add("main-dead-wait", main);
        main.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("main-dead-closed", main);

        var recovery = NewRig(); recovery.Session.RequiresLocalMenuAdmission = true;
        recovery.Open(OverlayInput.OpenQuick);
        recovery.Controller.RequestShutdown();
        recovery.Controller.RetryAfterShutdownFailure("Controlled offline cleanup failure");
        Add("cleanup-retry", recovery);

        var owner = NewRig(); owner.Open(OverlayInput.OpenQuick);
        owner.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); Add("quick-owner-retired", owner);
    }
}
