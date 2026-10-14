using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPreferenceRefreshTests
{
    internal static readonly (string Label, MenuPreference Preference)[] Flags = {
        ("Spotlight Cursor", MenuPreference.SpotlightCursor), ("Colored Cursor", MenuPreference.ColoredCursor),
        ("Large Cursor", MenuPreference.LargeCursor), ("Quick Exit", MenuPreference.QuickExit)
    };

    public static void FourLocalFlagsCommitImmediatelyButRetainDisplayedRowsAndCursorStyleUntilExactly180Milliseconds()
    {
        foreach (var flag in Flags)
        {
            var rig = new Rig(); rig.Open(); rig.Highlight(flag.Label); var original = rig.Current(); rig.Send(OverlayInput.Select);
            var pending = rig.Current(); TestAssert.Equal("pending", Phase(pending));
            TestAssert.Equal(original.Rows[original.SelectedIndex].Value, pending.Rows[pending.SelectedIndex].Value);
            // Native Quick Exit policy is committed while its cached option still displays the old value
            var committed = Changed(original.Appearance!, flag.Preference);
            var shown = committed with
            {
                SpotlightCursor = original.Appearance!.SpotlightCursor,
                ColoredCursor = original.Appearance.ColoredCursor,
                LargeCursor = original.Appearance.LargeCursor
            };
            TestAssert.Equal(shown, pending.Appearance);
            TestAssert.Equal(original.MenuRevision, pending.MenuRevision); TestAssert.Equal(original.NavigationRevision, pending.NavigationRevision);
            TestAssert.True(pending.Notice.Contains("Updating menu; controls paused", StringComparison.Ordinal), "A retained checkbox must explain its pending display");
            TestAssert.True(!pending.Notice.Contains("Hold melee", StringComparison.Ordinal), "Paused controls must suppress selected-row action instructions");
            rig.Clock.Advance(TimeSpan.FromMilliseconds(179)); TestAssert.Equal(shown, rig.Current().Appearance);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var applied = rig.Current();
            TestAssert.Equal("cooldown", Phase(applied)); TestAssert.Equal(committed, applied.Appearance);
            TestAssert.True(applied.Rows[applied.SelectedIndex].Value != original.Rows[original.SelectedIndex].Value, "The actual checkbox updates only when options refresh");
            TestAssert.Equal(original.MenuRevision + 1, applied.MenuRevision); TestAssert.Equal(original.NavigationRevision, applied.NavigationRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); TestAssert.Equal("cooldown", Phase(rig.Current()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Current(); TestAssert.True(Phase(ready) is null, "Input resumes at430ms");
            TestAssert.Equal(applied.MenuRevision, ready.MenuRevision); TestAssert.Equal(committed, ready.Appearance); rig.NoGameWork();
        }
    }

    public static void PausedInputsAcknowledgeWithoutChangingNavigationOrReplayingAfter430Milliseconds()
    {
        var rig = new Rig(); rig.Open(); rig.Highlight(Flags[0].Label); rig.Send(OverlayInput.Select); var pending = rig.Current();
        ulong rejected = 0;
        // Every normal monitor route is acknowledged during the same fixed wait
        foreach (var input in Enum.GetValues<OverlayInput>())
        {
            rig.Send(input); rejected = rig.Controller.LastSequence;
            TestAssert.Equal(pending.SelectedIndex, rig.Current().SelectedIndex); TestAssert.Equal(pending.NavigationRevision, rig.Current().NavigationRevision);
            TestAssert.Equal("pending", Phase(rig.Current())); TestAssert.True(rig.Current().IsOpen, "Monitor waits also disarm Toggle and Close");
        }
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Controller.ReadMenuPublicationPhase();
        rig.Controller.Consume(new(rejected, 1, 42, OverlayInput.Down, true), () => true);
        TestAssert.Equal(pending.SelectedIndex, rig.Current().SelectedIndex);
        rig.Send(OverlayInput.Down); TestAssert.True(rig.Current().SelectedIndex != pending.SelectedIndex, "Only a fresh input can navigate after the cooldown"); rig.NoGameWork();
    }

    public static void RejectedFocusForeignInvalidAndStaleSelectionsCannotStartOrReplayARefresh()
    {
        var rig = new Rig(); rig.Open(); rig.Highlight(Flags[0].Label); var original = rig.Current();
        rig.Send(OverlayInput.Select, focused: false); ulong stale = rig.Controller.LastSequence;
        foreach (var command in new[] { new CommandEnvelope(stale, 1, 42, OverlayInput.Select, true),
            new(rig.Sequence++, 1, 99, OverlayInput.Select, true), new(rig.Sequence++, 1, 42, OverlayInput.Select, false) })
            rig.Controller.Consume(command, () => true);
        int checks = 0; rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true), () => ++checks == 1);
        TestAssert.True(Phase(rig.Current()) is null, "Rejected selections cannot own a local refresh timer");
        TestAssert.Equal(original.Appearance, rig.Current().Appearance); TestAssert.Equal(original.MenuRevision, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void ClockQueriesAdvanceHiddenOwnerPhasesWithoutReadingOrObservingPendingSessionWork()
    {
        var rig = new Rig(preview: false); rig.Session.Pending = new TaskCompletionSource();
        rig.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(rig.Controller, rig.Clock); rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        rig.Select("Menu Customization"); rig.Select("Menu Preferences"); rig.Highlight(Flags[0].Label); rig.Send(OverlayInput.Select);
        var hidden = StateFocusProjection.Apply(rig.Current(), false); TestAssert.True(!hidden.IsOpen && Phase(hidden) is null, "Hidden packets must carry no active refresh metadata");
        // Clock observation cannot become an alternate completion or game-read path
        int reads = rig.Session.MenuStateReads; rig.Session.RejectMenuStateReads = true; rig.Session.Pending.SetResult();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); TestAssert.Equal("preference:cooldown", rig.Controller.ReadMenuPublicationPhase());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); TestAssert.True(rig.Controller.ReadMenuPublicationPhase() is null, "The owner timer expires while hidden");
        TestAssert.Equal(reads, rig.Session.MenuStateReads); TestAssert.True(rig.Controller.Busy, "A cheap timer query cannot observe pending task completion");
        rig.Session.RejectMenuStateReads = false; rig.Controller.ObserveCompletion(); TestAssert.True(!rig.Controller.Busy, "Session observation remains separate");
        TestAssert.True(!rig.Current().Appearance!.SpotlightCursor, "The committed preference survives unrelated operation completion");
        TestAssert.Equal(1, rig.Session.Executed.Count); TestAssert.Equal(0, rig.Session.ShutdownCalls);
    }

    public static void ShutdownCancelsTheTimerWithoutDiscardingCommittedFlagsOrLaterResurrectingPresentation()
    {
        foreach (var flag in Flags)
        {
            var rig = new Rig(); rig.Open(); rig.Highlight(flag.Label); var original = rig.Current(); rig.Send(OverlayInput.Select);
            rig.Controller.RequestShutdown(); var closed = rig.Controller.ClosedState();
            TestAssert.True(!closed.IsOpen && Phase(closed) is null, "Explicit owner cleanup cancels the pending local timer");
            TestAssert.Equal(Changed(original.Appearance!, flag.Preference), closed.Appearance);
            rig.Clock.Advance(TimeSpan.FromSeconds(1)); TestAssert.True(rig.Controller.ReadMenuPublicationPhase() is null, "A cancelled timer cannot rebuild or reopen the menu");
            TestAssert.Equal(closed.MenuRevision, rig.Current().MenuRevision); rig.NoGameWork();
        }
    }

    public static void PreRefreshRollbackRestartsPendingWhilePostRefreshRollbackRestartsOnlyCooldownWithoutReversion()
    {
        var rig = new Rig(); rig.Open(); rig.Highlight(Flags[0].Label); var original = rig.Current(); rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); rig.Current(); rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("pending", Phase(rig.Current())); TestAssert.Equal(original.Appearance, rig.Current().Appearance);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179)); TestAssert.Equal(original.MenuRevision, rig.Current().MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var applied = rig.Current(); TestAssert.Equal(original.MenuRevision + 1, applied.MenuRevision);
        // A fault after redraw must restart cooldown without reapplying the old checkbox or style
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); rig.Current(); rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        var restart = rig.Current(); TestAssert.Equal("cooldown", Phase(restart)); TestAssert.Equal(applied.Appearance, restart.Appearance);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(249)); TestAssert.Equal("cooldown", Phase(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Phase(rig.Current()) is null, "Restarted cooldown retains a finite250ms lifetime");
        TestAssert.Equal(applied.MenuRevision, rig.Current().MenuRevision); rig.NoGameWork();
    }

    public static void ClockStartAndCounterFaultsRejectTheWholeCommitWithoutLatentState()
    {
        var throwingClock = new ThrowingClock(); var throwing = new Rig(clock: throwingClock); throwing.Open(); throwing.Highlight(Flags[0].Label); var original = throwing.Current(); throwingClock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => throwing.Send(OverlayInput.Select));
        throwing.Controller.RequestShutdown(); TestAssert.Equal(original.Appearance, throwing.Controller.ClosedState().Appearance);
        // Admission checks reserve the one required presentation epoch before committing a preference
        var session = new SessionBoundary(); var navigation = new OverlayNavigation(0, ulong.MaxValue - 4);
        navigation.Open(); navigation.OpenPage(MenuPage.Preferences);
        navigation.SetRows(OverlayMenuBuilder.Build(MenuPage.Preferences, session.State).Rows); navigation.InvalidatePresentation();
        navigation.SelectIndex(6); var controller = new BackendController(42, session, navigation, false); var before = controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, before.MenuRevision);
        TestAssert.Throws<OverflowException>(() => controller.Consume(new(1, 1, 42, OverlayInput.Select, true), () => true));
        controller.RequestShutdown(); var retained = controller.Snapshot(); TestAssert.Equal(before.Appearance, retained.Appearance);
        TestAssert.True(Phase(retained) is null, "A counter failure cannot leave a captured presentation or timer");
    }

    public static void SignedElapsedOverflowRestartsOnlyTheCurrentStageAndNeverReappliesTheDisplayRefresh()
    {
        foreach (bool applied in new[] { false, true })
        {
            var rig = new Rig(); rig.Clock.RollBackTimestamp(TimeSpan.MaxValue); rig.Clock.RollBackTimestamp(TimeSpan.FromTicks(1));
            rig.Open(); rig.Highlight(Flags[0].Label); var original = rig.Current(); rig.Send(OverlayInput.Select);
            if (applied) { rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); rig.Current(); }
            var before = rig.Current();
            rig.Clock.Advance(TimeSpan.FromTicks(long.MaxValue - (applied ? TimeSpan.FromMilliseconds(180).Ticks : 0)));
            rig.Clock.Advance(TimeSpan.FromTicks(1)); var restarted = rig.Current();
            TestAssert.Equal(applied ? "cooldown" : "pending", Phase(restarted)); TestAssert.Equal(before.Appearance, restarted.Appearance);
            TestAssert.Equal(before.MenuRevision, restarted.MenuRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(applied ? 249 : 179)); TestAssert.Equal(applied ? "cooldown" : "pending", Phase(rig.Current()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            if (!applied) { TestAssert.Equal(original.MenuRevision + 1, rig.Current().MenuRevision); rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); }
            TestAssert.True(Phase(rig.Current()) is null, "An overflow restart retains a finite current-stage lifetime");
            TestAssert.Equal(original.MenuRevision + 1, rig.Current().MenuRevision); rig.NoGameWork();
        }
    }

    public static void CounterRebuildAndOtherImmediateHandlersRemainSeparateFromDeferredPreferenceRefresh()
    {
        foreach (string label in new[] { "Option Counter" })
        {
            var rig = new Rig(); rig.Open(); rig.Select(label); TestAssert.True(Phase(rig.Current()) is null, "Immediate handlers must remain immediate"); rig.NoGameWork();
        }
        var local = new Rig(); local.Open(); local.Highlight("Bool Display"); local.Send(OverlayInput.NextChoice); local.Send(OverlayInput.Select);
        TestAssert.Equal("Text", local.Current().Appearance!.BooleanDisplay); TestAssert.True(Phase(local.Current()) is null, "String choice commits use their existing immediate refresh");
        local.Select("Bool Box Location"); TestAssert.True(Phase(local.Current()) is null, "Location choices cannot acquire a boolean wait");
        local.Select("Scroll Animation Time (ms)"); TestAssert.True(Phase(local.Current()) is null, "Numeric commits cannot acquire a boolean wait");
        local.Send(OverlayInput.Back); local.Select("Menu Instructions");
        foreach (string label in new[] { "Alternate Style", "Disable Background" }) { local.Select(label); TestAssert.True(Phase(local.Current()) is null, "Footer handlers remain immediate"); }
        local.Select("Alternate Style"); local.Select("Adaptive Position"); TestAssert.True(Phase(local.Current()) is null, "Adaptive Position remains immediate");
        local.Send(OverlayInput.Back); local.Select("Main Design Color"); local.Select("Smooth Rainbow");
        TestAssert.True(Phase(local.Current()) is null, "Rainbow retains its existing independent animation"); local.Select("Mint"); TestAssert.True(Phase(local.Current()) is null, "Fixed colors retain immediate propagation"); local.NoGameWork();
    }

    internal static string? Phase(StateDocument state) { using var json = JsonDocument.Parse(state.Serialize()); return json.RootElement.TryGetProperty("menuPreferenceRefresh", out var value) && value.ValueKind == JsonValueKind.Object ? value.GetProperty("phase").GetString() : null; }
    internal static MenuAppearance Changed(MenuAppearance appearance, MenuPreference flag) { TestAssert.True(appearance.TryChange((int)flag, out var changed), "Supported local flag must toggle"); return changed; }
    private sealed class ThrowingClock : TimeProvider, IManualClockFixture { public ManualTimeProvider Time { get; } = new(); internal bool Fail { get; set; } public override long TimestampFrequency => Time.TimestampFrequency; public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp(); }
    internal sealed class Rig
    {
        internal SessionBoundary Session { get; } = new(); internal ManualTimeProvider Clock { get; } = new(); internal BackendController Controller { get; }
        internal ulong Sequence { get; set; } = 1;
        internal ManualTimeProvider SetupClock { get; }
        internal Rig(bool preview = true, TimeProvider? clock = null) { SetupClock = clock is IManualClockFixture fixture ? fixture.Time : Clock; Session.State = Session.State with { Preview = preview }; Controller = new(42, Session, true, clock: clock ?? Clock); }
        internal StateDocument Current() => Controller.Snapshot();
        internal void Send(OverlayInput input, bool focused = true) => Controller.Consume(new(Sequence++, 1, 42, input, true, input >= OverlayInput.Close ? Current().NavigationRevision : null), () => focused);
        internal void Open() { Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(Controller, SetupClock); Select("Menu Customization"); Select("Menu Preferences"); }
        internal void Highlight(string label) { var rows = Current().Rows; int target = rows.ToList().FindIndex(row => row.Label == label); TestAssert.True(target >= 0, "Required row missing: " + label); for (int index = 0; index < rows.Count && Current().SelectedIndex != target; index++) Send(OverlayInput.Down); }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void NoGameWork() { Controller.StartQueuedAction(); TestAssert.Equal(0, Session.Executed.Count); TestAssert.Equal(0, Session.ShutdownCalls); }
    }
}
