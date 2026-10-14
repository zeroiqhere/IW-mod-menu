using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuCreditsTests
{
    internal const OverlayInput Finish = OverlayInput.FinishMenuCredits;

    public static void CreditsEnterImmediatelyWithSourceTitleEmptyRowsAndHonestAttributionWithoutGameWork()
    {
        var rig = new Rig(); rig.Open(); var parent = rig.Current();
        TestAssert.Equal(7, parent.Rows.Count); TestAssert.Equal("Menu Credits", parent.Rows[0].Label);
        TestAssert.True(parent.Rows[0].Enabled, "The first source row must open actual credits");
        TestAssert.Equal("Open Controls", parent.Rows[1].Label);
        TestAssert.True(!parent.Rows[1].Enabled && parent.Rows[1].Value == "UNAVAILABLE", "Unsupported Open Controls must remain truthful");
        // Entry uses the same fresh Use route as other local customization pages
        rig.Select("Menu Credits"); var state = rig.Current();
        TestAssert.Equal("Menu Customization", state.Title);
        TestAssert.True(state.IsOpen && !state.IsRootMenu && state.SelectedIndex == 0 && state.Rows.Count == 0,
            "Credits require a dedicated empty child presentation");
        TestAssert.Equal(0, Elapsed(state));
        foreach (string credit in new[] { "BO3", "CF4_99", "SyndiShanX", "Memory.dll", "Dear ImGui", "MinHook", "nlohmann/json" })
            TestAssert.True(state.Notice.Contains(credit, StringComparison.Ordinal), "Attribution must identify the actual reference and dependencies: " + credit);
        TestAssert.True(!state.Notice.Contains("Biggest & Best", StringComparison.Ordinal), "The IW port cannot copy BO3 marketing claims");
        rig.NoGameWork();
    }

    public static void ElapsedProgressKeepsBothEpochsStableAndNaturalFinishRunsTheExactParentUnlock()
    {
        var rig = new Rig(); rig.Enter(); var initial = rig.Current();
        foreach (int delta in new[] { 500, 500, 38999 })
        {
            rig.Clock.Advance(TimeSpan.FromMilliseconds(delta)); var current = rig.Current();
            TestAssert.True(Elapsed(current) is >= 0 and <= 39999, "The active timeline must remain in its strict domain");
            TestAssert.Equal(initial.NavigationRevision, current.NavigationRevision); TestAssert.Equal(initial.MenuRevision, current.MenuRevision);
        }
        TestAssert.Equal(39999, Elapsed(rig.Current()));
        // Natural completion is observed by the owner loop before any session snapshot
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.Equal("rebuild", rig.Controller.ReadMenuPublicationPhase());
        rig.AssertParent("rebuild");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); rig.AssertParent("rebuild");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.AssertParent("pause");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(99)); rig.AssertParent("pause");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); rig.AssertParent(null);
        rig.NoGameWork();
    }

    public static void FinishRequiresFreshExactPidEpochAndBothFocusChecksAndNeverReplays()
    {
        var rig = new Rig(); rig.Enter(); var initial = rig.Current();
        rig.Send(Finish, focused: false); ulong rejected = rig.Controller.LastSequence;
        foreach (var command in new[] { new CommandEnvelope(rejected, 1, 42, Finish, true, initial.NavigationRevision),
            new(rig.Sequence++, 1, 99, Finish, true, initial.NavigationRevision), new(rig.Sequence++, 1, 42, Finish, false, initial.NavigationRevision),
            new(rig.Sequence++, 1, 42, Finish, true), new(rig.Sequence++, 1, 42, Finish, true, initial.NavigationRevision + 1) })
            rig.Controller.Consume(command, () => true);
        // The second focus check occurs immediately before the local finish commit
        int checks = 0;
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, Finish, true, initial.NavigationRevision), () => ++checks == 1);
        TestAssert.Equal(0, Elapsed(rig.Current()));
        rig.Send(Finish); rig.AssertParent("rebuild"); rig.NoGameWork();
    }

    public static void DuplicateEntryAndOrdinaryInputsCannotRestartOrNavigateTheCreditsTimeline()
    {
        var rig = new Rig(); rig.Enter(); rig.Clock.Advance(TimeSpan.FromMilliseconds(1000)); var visit = rig.Current();
        foreach (var input in new[] { OverlayInput.Up, OverlayInput.Down, OverlayInput.Select, OverlayInput.Back,
            OverlayInput.PreviousChoice, OverlayInput.NextChoice, OverlayInput.FinishWidthEditor,
            OverlayInput.FinishInstructionPositionEditor, OverlayInput.FinishMenuPositionEditor }) rig.Send(input);
        TestAssert.Equal(1000, Elapsed(rig.Current())); TestAssert.Equal(visit.NavigationRevision, rig.Current().NavigationRevision);
        rig.NoGameWork();
    }

    public static void ToggleCloseAndShutdownCancelWithoutResurrectionAndReentryRejectsOldVisitPackets()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var rig = new Rig(); rig.Open();
            if (mode == 1) { rig.Select("Menu Preferences"); rig.Select("Quick Exit"); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Send(OverlayInput.Back); }
            rig.Select("Menu Credits"); var old = rig.Current();
            if (mode == 2) { rig.Controller.RequestShutdown(); rig.Controller.ClosedState(); }
            else rig.Send(mode == 0 ? OverlayInput.Toggle : OverlayInput.Close);
            var closed = rig.Current(); TestAssert.True(!closed.IsOpen && Elapsed(closed) is null && closed.MenuUnlock is null, "Cancellation clears only the local visit");
            rig.Clock.Advance(TimeSpan.FromSeconds(50)); rig.Controller.ReadMenuPublicationPhase();
            TestAssert.True(!rig.Current().IsOpen && Elapsed(rig.Current()) is null, "A retired timeline cannot reopen its parent");
            if (mode != 2)
            {
                rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.AssertParent(null); rig.Send(OverlayInput.Select);
                rig.Controller.Consume(new(rig.Sequence++, 1, 42, Finish, true, old.NavigationRevision), () => true);
                TestAssert.Equal(0, Elapsed(rig.Current()));
            }
            rig.NoGameWork();
        }
    }

    public static void FocusHiddenFramesRetainTheOwnerTimerAndCheapCompletionDoesNotReadOrObserveSessionWork()
    {
        var rig = new Rig(); rig.Enter(); int reads = rig.Session.MenuStateReads;
        var hidden = StateFocusProjection.Apply(rig.Current(), false);
        TestAssert.True(!hidden.IsOpen && Elapsed(hidden) is null, "Hidden renderer frames cannot carry active credits");
        // A phase query remains safe while the live session refuses snapshot reads
        rig.Session.RejectMenuStateReads = true;
        rig.Clock.Advance(TimeSpan.FromMilliseconds(40000)); TestAssert.Equal("rebuild", rig.Controller.ReadMenuPublicationPhase());
        rig.Session.RejectMenuStateReads = false; TestAssert.Equal(reads + 1, rig.Session.MenuStateReads);
        rig.AssertParent("rebuild"); rig.NoGameWork();
    }

    public static void ClockRollbackAndSignedElapsedOverflowRestartOnlyTheTimelineConservatively()
    {
        var rig = new Rig(); rig.Enter(); rig.Clock.Advance(TimeSpan.FromSeconds(3)); TestAssert.Equal(3000, Elapsed(rig.Current()));
        rig.Clock.RollBackTimestamp(TimeSpan.FromSeconds(4)); TestAssert.Equal(0, Elapsed(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(500)); TestAssert.Equal(500, Elapsed(rig.Current()));
        // Opaque signed timestamps can overflow their delta without moving backwards
        var overflow = new Rig(); overflow.Clock.RollBackTimestamp(TimeSpan.MaxValue); overflow.Clock.RollBackTimestamp(TimeSpan.FromTicks(1)); overflow.Enter();
        overflow.Clock.Advance(TimeSpan.MaxValue); overflow.Clock.Advance(TimeSpan.FromTicks(1));
        TestAssert.Equal(0, Elapsed(overflow.Current())); overflow.Clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.Equal(1, Elapsed(overflow.Current())); rig.NoGameWork(); overflow.NoGameWork();
    }

    public static void BusySessionWorkRemainsIndependentAndEntryRechecksFocusBeforeCreatingItsLocalTimer()
    {
        var rejected = new Rig(); rejected.Open(); rejected.Highlight("Menu Credits"); int checks = 0;
        rejected.Controller.Consume(new(rejected.Sequence++, 1, 42, OverlayInput.Select, true), () => ++checks == 1);
        TestAssert.True(Elapsed(rejected.Current()) is null, "Focus loss before entry must not create a latent credits state"); rejected.NoGameWork();
        var rig = new Rig(preview: false); rig.Session.Pending = new TaskCompletionSource();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        rig.Select("Menu Customization"); rig.Select("Menu Credits"); rig.Clock.Advance(TimeSpan.FromSeconds(40));
        rig.Controller.ReadMenuPublicationPhase(); rig.AssertParent("rebuild");
        TestAssert.True(rig.Controller.Busy, "Natural credits completion cannot complete a pending game operation");
        TestAssert.Equal(1, rig.Session.Executed.Count); TestAssert.Equal(0, rig.Session.ShutdownCalls);
        rig.Session.Pending.SetResult(); rig.Controller.ObserveCompletion(); rig.AssertParent("rebuild");
    }

    public static void CounterExhaustionRejectsEntryWithoutRetainingAnUnpublishedCreditsTimer()
    {
        var session = new SessionBoundary(); var nav = new OverlayNavigation(ulong.MaxValue - 2, 0);
        nav.Open(); nav.OpenPage(MenuPage.Customization); nav.SetRows(OverlayMenuBuilder.Build(MenuPage.Customization, session.State).Rows);
        // The final counter rejects page entry before any local timer becomes owned
        var controller = new BackendController(42, session, nav, false); var original = controller.Snapshot();
        TestAssert.Equal("Menu Credits", original.Rows[0].Label);
        TestAssert.Throws<OverflowException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Select, true), () => true));
        var retained = controller.Snapshot(); TestAssert.True(Elapsed(retained) is null, "Failed navigation cannot create a credits timer");
        TestAssert.Equal(original.NavigationRevision, retained.NavigationRevision); TestAssert.Equal(original.MenuRevision, retained.MenuRevision);
        TestAssert.True(!retained.Notice.Contains("CF4_99", StringComparison.Ordinal), "Unpublished credits cannot leak into the retained parent notice");
    }

    public static void FailedClockStartLeavesTheParentNavigationAndMetadataUnchanged()
    {
        var faultClock = new FailingStartClock(); var rig = new Rig(clock: faultClock); rig.Open(); var parent = rig.Current(); faultClock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.Select));
        var retained = rig.Current(); TestAssert.Equal(parent.NavigationRevision, retained.NavigationRevision);
        TestAssert.Equal(parent.MenuRevision, retained.MenuRevision); TestAssert.Equal(parent.Rows.Count, retained.Rows.Count);
        TestAssert.True(retained.MenuCredits is null && retained.Rows[retained.SelectedIndex].Label == "Menu Credits",
            "A rejected clock start must leave the actual parent and no latent timer"); rig.NoGameWork();
    }
    private sealed class FailingStartClock : TimeProvider, IManualClockFixture
    {
        public ManualTimeProvider Time { get; } = new(); internal bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }

    internal static int? Elapsed(StateDocument state)
    {
        using var json = JsonDocument.Parse(state.Serialize());
        return json.RootElement.TryGetProperty("menuCredits", out var credits) && credits.ValueKind == JsonValueKind.Object
            ? credits.GetProperty("elapsedMilliseconds").GetInt32() : null;
    }

    internal sealed class Rig
    {
        internal SessionBoundary Session { get; } = new(); internal ManualTimeProvider Clock { get; } = new();
        internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal ManualTimeProvider SetupClock { get; }
        internal Rig(bool preview = true, TimeProvider? clock = null) { SetupClock = clock is IManualClockFixture fixture ? fixture.Time : Clock; Session.State = Session.State with { Preview = preview }; Controller = new(42, Session, clock: clock ?? Clock); }
        internal StateDocument Current() => Controller.Snapshot();
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true,
            input >= OverlayInput.Close ? Current().NavigationRevision : null), () => focused);
        internal void Open() { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, SetupClock); Select("Menu Customization"); }
        internal void Enter() { Open(); Select("Menu Credits"); }
        internal void Highlight(string label)
        {
            var state = Current(); int target = state.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "Required row missing: " + label);
            for (int index = 0; index < state.Rows.Count && Current().SelectedIndex != target; index++) Send(OverlayInput.Down);
        }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void AssertParent(string? phase)
        {
            var state = Current(); TestAssert.True(Elapsed(state) is null && state.IsOpen && state.MenuUnlock == phase, "Credits must restore the exact parent and unlock phase");
            TestAssert.Equal("Menu Credits", state.Rows[state.SelectedIndex].Label);
        }
        internal void NoGameWork() { Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls); }
    }
}
