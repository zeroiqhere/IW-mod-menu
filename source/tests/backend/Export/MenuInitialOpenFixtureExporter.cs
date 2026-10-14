using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Each profile uses the production controller with an owned clock and no game transport
internal static class MenuInitialOpenFixtureExporter
{
    public static void Add(IDictionary<string, StateDocument> fixtures)
    {
        var root = new Rig();
        root.Send(OverlayInput.Toggle);
        fixtures.Add("menu-initial-open-rebuild.json", root.Current());
        root.Clock.Advance(TimeSpan.FromMilliseconds(49));
        fixtures.Add("menu-initial-open-pre-draw.json", root.Current());
        root.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var drawn = root.Current();
        fixtures.Add("menu-initial-open-drawn.json", drawn);
        fixtures.Add("menu-initial-open-focus-hidden.json", StateFocusProjection.Apply(drawn, false));
        root.Clock.Advance(TimeSpan.FromMilliseconds(549));
        fixtures.Add("menu-initial-open-pre-unlock.json", root.Current());
        root.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("menu-initial-open-input-ready.json", root.Current());
        root.Clock.Advance(TimeSpan.FromMilliseconds(149));
        fixtures.Add("menu-initial-open-last.json", root.Current());
        root.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("menu-initial-open-complete.json", root.Current());
        root.AssertNoGameWork();

        var late = new Rig();
        late.Send(OverlayInput.Toggle);
        late.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        fixtures.Add("menu-initial-open-late.json", late.Current());
        late.Clock.Advance(TimeSpan.FromMilliseconds(549));
        fixtures.Add("menu-initial-open-late-pre-unlock.json", late.Current());
        late.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("menu-initial-open-late-ready.json", late.Current());
        late.AssertNoGameWork();

        // A normal reopen preserves its selected child and uses the next event identity
        var retained = new Rig();
        retained.OpenSettled();
        retained.Select("Menu Customization");
        retained.Select("Menu Preferences");
        retained.Highlight("Large Cursor");
        retained.Send(OverlayInput.Toggle);
        retained.Send(OverlayInput.Toggle);
        fixtures.Add("menu-initial-open-retained-rebuild.json", retained.Current());
        retained.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-initial-open-retained-drawn.json", retained.Current());
        retained.AssertNoGameWork();

        var empty = new Rig();
        empty.OpenSettled();
        empty.Select("Messages");
        empty.Send(OverlayInput.Toggle);
        empty.Send(OverlayInput.Toggle);
        fixtures.Add("menu-initial-open-empty-rebuild.json", empty.Current());
        empty.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-initial-open-empty-drawn.json", empty.Current());
        empty.AssertNoGameWork();

        var single = new Rig(preview: false);
        single.OpenSettled();
        single.Select("Players Menu");
        single.Send(OverlayInput.Toggle);
        single.Send(OverlayInput.Toggle);
        single.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-initial-open-single-drawn.json", single.Current());
        single.AssertNoGameWork();

        // Actual Players retains its Refresh row while the read-only roster changes
        var dynamic = new Rig(preview: false);
        dynamic.OpenSettled();
        dynamic.Select("Players Menu");
        dynamic.Send(OverlayInput.Toggle);
        dynamic.Send(OverlayInput.Toggle);
        dynamic.Clock.Advance(TimeSpan.FromMilliseconds(50));
        dynamic.Controller.ReadMenuPublicationPhase();
        dynamic.Session.State = dynamic.Session.State with
        {
            Players = new[] { new OverlayPlayer(1, "Friend"), new OverlayPlayer(2, "Other") }
        };
        fixtures.Add("menu-initial-open-dynamic-drawn.json", dynamic.Current());
        dynamic.Clock.Advance(TimeSpan.FromMilliseconds(100));
        dynamic.Session.State = dynamic.Session.State with
        {
            Players = Array.Empty<OverlayPlayer>()
        };
        fixtures.Add("menu-initial-open-dynamic-changed.json", dynamic.Current());
        dynamic.AssertNoGameWork();
    }

    private sealed class Rig
    {
        internal SessionBoundary Session { get; } = new();
        internal ManualTimeProvider Clock { get; } = new();
        internal BackendController Controller
        {
            get;
        }
        private ulong sequence = 1;

        internal Rig(bool preview = true)
        {
            Session.State = Session.State with
            {
                Preview = preview
            };
            Controller = new(42, Session, choiceControlsAvailable: true, clock: Clock);
        }
        internal StateDocument Current() => Controller.Snapshot();
        internal void Send(OverlayInput input) => Controller.Consume(new(sequence++, 1, 42, input, true), () => true);
        internal void OpenSettled()
        {
            Send(OverlayInput.Toggle);
            MenuOpenTestHarness.Settle(Controller, Clock);
        }
        internal void Highlight(string label)
        {
            var snapshot = Current();
            int target = snapshot.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "The actual menu requires the selected fixture row");
            for (int moved = 0; moved < snapshot.Rows.Count && Current().SelectedIndex != target; moved++)
                Send(OverlayInput.Down);
        }
        internal void Select(string label)
        {
            Highlight(label);
            Send(OverlayInput.Select);
        }
        internal void AssertNoGameWork()
        {
            Controller.StartQueuedAction();
            TestAssert.Equal(0, Session.Executed.Count);
            TestAssert.Equal(0, Session.ShutdownCalls);
            TestAssert.True(!Controller.Busy && !Controller.ExitRequested, "Offline profiles cannot dispatch features or cleanup");
        }
    }
}
