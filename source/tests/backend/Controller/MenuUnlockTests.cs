using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuUnlockTests {
    public static void FinishPublishesExactRebuildPauseAndUnlockedBoundariesFromAMonotonicClock() {
        var (controller, _, clock, sequence) = FinishEditor();
        var rebuilding = controller.Snapshot();
        TestAssert.Equal("rebuild", rebuilding.MenuUnlock);
        TestAssert.True(rebuilding.IsOpen && !rebuilding.IsRootMenu && rebuilding.WidthEditor is null,
            "Finishing restores the actual parent while panel rebuilding is separate metadata");
        TestAssert.Equal("Menu Customization", rebuilding.Title);
        TestAssert.Equal("Width Editor", rebuilding.Rows[rebuilding.SelectedIndex].Label);
        TestAssert.Equal(261, rebuilding.Appearance?.MenuWidth);
        TestAssert.True(rebuilding.Rows[rebuilding.SelectedIndex].Enabled, "The lock cannot invent a missing row capability");
        TestAssert.True(rebuilding.Notice.Contains("Returning to menu; controls paused", StringComparison.Ordinal)
            && !rebuilding.Notice.Contains("Select to open Width Editor", StringComparison.Ordinal), "Locked help must not advertise a currently ignored Select");
        ulong revision = rebuilding.NavigationRevision;
        clock.Advance(TimeSpan.FromMilliseconds(49));
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(99));
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var unlocked = controller.Snapshot();
        TestAssert.True(unlocked.MenuUnlock is null, "Controls must unlock at exactly the nominal150ms boundary");
        TestAssert.Equal(revision, unlocked.NavigationRevision);
        TestAssert.Equal(rebuilding.Appearance, unlocked.Appearance);
        TestAssert.True(!unlocked.Notice.Contains("controls paused", StringComparison.Ordinal)
            && unlocked.Notice.Contains("Select to open Width Editor", StringComparison.Ordinal), "Selected-row help returns after unlock");
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(1, controller.Snapshot().WidthEditor?.Step);
    }

    public static void LockedCommandsAcknowledgeOnceAndCannotReplayOrExtendThePause() {
        var (controller, session, clock, sequence) = FinishEditor(quickExit: true);
        var initial = controller.Snapshot();
        ulong lastRejected = 0;
        foreach (OverlayInput input in Enum.GetValues<OverlayInput>()) {
            lastRejected = sequence++;
            Send(controller, lastRejected, input);
            var frame = controller.Snapshot();
            TestAssert.Equal(initial.Appearance, frame.Appearance);
            TestAssert.Equal(initial.SelectedIndex, frame.SelectedIndex);
            TestAssert.Equal(initial.NavigationRevision, frame.NavigationRevision);
            TestAssert.True(frame.IsOpen && frame.MenuUnlock == "rebuild", "Every normal input must be ignored during rebuild");
        }
        clock.Advance(TimeSpan.FromMilliseconds(50));
        Send(controller, sequence++, OverlayInput.Back);
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Send(controller, lastRejected, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().WidthEditor is null, "An acknowledged input cannot replay once controls resume");
        TestAssert.Equal(sequence - 1, controller.LastSequence);
        TestAssert.True(controller.Snapshot().MenuUnlock is null, "Rejected inputs cannot extend the original pause");
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().WidthEditor is not null, "A new deliberate Select must work after unlock");
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void ForeignInvalidAndFocusRejectedCommandsDoNotAlterOrReplayTheUnlockState() {
        var (controller, _, clock, sequence) = FinishEditor();
        var initial = controller.Snapshot();
        controller.Consume(new(sequence++, 1, 99, OverlayInput.Down, true), () => true);
        controller.Consume(new(sequence++, 1, 42, null, false), () => true);
        ulong focusRejected = sequence++;
        controller.Consume(new(focusRejected, 1, 42, OverlayInput.Select, true), () => false);
        TestAssert.Equal(initial.NavigationRevision, controller.Snapshot().NavigationRevision);
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        Send(controller, focusRejected, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().WidthEditor is null, "Rejected focus input remains acknowledged after unlock");
        TestAssert.Equal(focusRejected, controller.LastSequence);
    }

    public static void UtcJumpsCannotChangeThePauseAndTimestampRollbackRestartsItsBoundedLifetime() {
        var (controller, _, clock, _) = FinishEditor();
        clock.MoveWallClock(TimeSpan.FromDays(1000));
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        clock.MoveWallClock(TimeSpan.FromDays(-2000));
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(75));
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(149));
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(controller.Snapshot().MenuUnlock is null, "A rollback restart still has a finite monotonic lifetime");
    }

    public static void SignedElapsedDeltaOverflowRestartsInsteadOfKeepingTheMenuLockedIndefinitely() {
        var clock = new ManualTimeProvider();
        // Timestamp counters are opaque signed values, so exercise the real subtraction boundary
        clock.RollBackTimestamp(TimeSpan.MaxValue);
        clock.RollBackTimestamp(TimeSpan.FromTicks(1));
        var (controller, _, sequence) = WidthEditorTests.OpenEditor(clock: clock);
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.MaxValue);
        clock.Advance(TimeSpan.FromTicks(1));
        TestAssert.Equal(TimeSpan.MinValue, clock.GetElapsedTime(long.MinValue, 0));
        TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(149));
        TestAssert.Equal("pause", controller.Snapshot().MenuUnlock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(controller.Snapshot().MenuUnlock is null, "A signed delta overflow must restart one bounded150ms pause");
    }

    public static void ShutdownClearsThePauseButOrdinaryEditorHidingNeverStartsOne() {
        foreach (bool final in new[] { false, true }) {
            var (controller, session, _, _) = FinishEditor();
            TestAssert.Equal("rebuild", controller.Snapshot().MenuUnlock);
            if (final) {
                var closed = controller.ClosedState();
                TestAssert.True(!closed.IsOpen && closed.MenuUnlock is null, "Final closed state must clear the transient lock");
            } else {
                controller.RequestShutdown();
                TestAssert.True(controller.Snapshot().MenuUnlock is null, "External cleanup must clear the transient lock");
                TestAssert.True(controller.ExitRequested, "Input pause cannot delay explicit cleanup");
            }
            TestAssert.Equal(0, session.Executed.Count);
            TestAssert.Equal(0, session.ShutdownCalls);
        }
        foreach (OverlayInput input in new[] { OverlayInput.Toggle, OverlayInput.Close }) {
            var clock = new ManualTimeProvider();
            var (controller, _, sequence) = WidthEditorTests.OpenEditor(quickExit: true, clock: clock);
            Send(controller, sequence++, input);
            TestAssert.True(!controller.Snapshot().IsOpen && controller.Snapshot().MenuUnlock is null,
                "Only Finish can begin SoftUnlock timing");
        }
    }

    public static void PendingCompletionAndAtomicHeartbeatsContinueDuringThePauseWithoutMovingItsParent() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, true, true, clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        WidthEditorTests.HighlightWidth(controller, ref sequence);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        TestAssert.True(controller.Busy && controller.Snapshot().MenuUnlock == "rebuild", "The test needs concurrent pending work and input pause");
        session.State = session.State with { Connected = true, CanConnect = false };
        session.Pending.SetResult();
        var completed = controller.Snapshot();
        TestAssert.True(!controller.Busy && completed.MenuUnlock == "rebuild", "Completion must remain independent of paused input");
        TestAssert.Equal("Menu Customization", completed.Title);
        string directory = Path.Combine(Path.GetTempPath(), "apparition-unlock-heartbeats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            files.WriteState(completed);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            files.WriteState(controller.Snapshot());
            using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "state.json")));
            TestAssert.Equal(2ul, json.RootElement.GetProperty("generation").GetUInt64());
            TestAssert.Equal("pause", json.RootElement.GetProperty("menuUnlock").GetString());
            TestAssert.Equal(sequence - 1, json.RootElement.GetProperty("lastSequence").GetUInt64());
        } finally { Directory.Delete(directory, true); }
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void UnlockMetadataSerializesExactPhasesAndDefaultsToNullForOlderCreateCallers() {
        var (controller, _, clock, _) = FinishEditor();
        using (var rebuild = JsonDocument.Parse(controller.Snapshot().Serialize())) {
            TestAssert.Equal("rebuild", rebuild.RootElement.GetProperty("menuUnlock").GetString());
        }
        clock.Advance(TimeSpan.FromMilliseconds(50));
        using (var pause = JsonDocument.Parse(controller.Snapshot().Serialize())) {
            TestAssert.Equal("pause", pause.RootElement.GetProperty("menuUnlock").GetString());
        }
        clock.Advance(TimeSpan.FromMilliseconds(100));
        using (var unlocked = JsonDocument.Parse(controller.Snapshot().Serialize())) {
            TestAssert.Equal(JsonValueKind.Null, unlocked.RootElement.GetProperty("menuUnlock").ValueKind);
        }
        var legacy = StateDocument.Create(42, 1, false, new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        TestAssert.True(legacy.MenuUnlock is null, "Generic older callers must remain unlocked");
    }

    public static void DelayedPlayerSelectionCannotReplaceTheRestoredParentDuringOrAfterUnlock() {
        var clock = new ManualTimeProvider();
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.State = session.State with { Connected = true, CanConnect = false, CanSelectPlayers = true,
            CanRefreshPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        var controller = new BackendController(42, session, true, true, clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.True(controller.Busy, "The regression needs a pending verified-player selection");
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Up);
        Send(controller, sequence++, OverlayInput.Select);
        WidthEditorTests.HighlightWidth(controller, ref sequence);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        var parent = controller.Snapshot();
        TestAssert.Equal("Menu Customization", parent.Title);
        TestAssert.Equal("rebuild", parent.MenuUnlock);

        // Completing old game work cannot revive its earlier menu-navigation intent
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending.SetResult();
        var completed = controller.Snapshot();
        TestAssert.True(!controller.Busy && completed.MenuUnlock == "rebuild", "Selection completion and pause timing remain independent");
        TestAssert.Equal(parent.Title, completed.Title);
        TestAssert.Equal(parent.SelectedIndex, completed.SelectedIndex);
        TestAssert.Equal(parent.NavigationRevision, completed.NavigationRevision);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        TestAssert.Equal(parent.Title, controller.Snapshot().Title);
        TestAssert.True(controller.Snapshot().MenuUnlock is null, "The same retained parent unlocks normally");
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(MenuActionKind.SelectPlayer, session.Executed[0].Kind);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    private static (BackendController Controller, SessionBoundary Session, ManualTimeProvider Clock, ulong Sequence) FinishEditor(bool quickExit = false) {
        var clock = new ManualTimeProvider();
        var (controller, session, sequence) = WidthEditorTests.OpenEditor(quickExit, clock);
        Send(controller, sequence++, OverlayInput.IncreaseWidth);
        Send(controller, sequence++, OverlayInput.FinishWidthEditor);
        return (controller, session, clock, sequence);
    }
    private static void Send(BackendController controller, ulong sequence, OverlayInput input) => WidthEditorTests.Send(controller, sequence, input);
}
