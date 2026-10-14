using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using System.Text;

namespace Backend.Tests;

internal static class ChoiceDraftTests {
    public static void SourceChoiceCommandsParseWithTheirExactCamelCaseNames() {
        foreach (var pair in new[] { ("previousChoice", OverlayInput.PreviousChoice), ("nextChoice", OverlayInput.NextChoice) }) {
            var command = CommandEnvelope.Parse(Encoding.UTF8.GetBytes($"{{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"{pair.Item1}\"}}"));
            TestAssert.True(command?.Valid == true, "Known local choice input must parse");
            TestAssert.Equal(pair.Item2, command!.Input);
        }
    }

    public static void DraftMovementWrapsSourceChoicesWithoutApplyingUntilFreshUse() {
        var (controller, session, sequence, clock) = Open(true);
        Send(controller, sequence++, OverlayInput.PreviousChoice);
        TestAssert.Equal("Text Color", controller.Snapshot().Rows[1].Value);
        TestAssert.Equal("Boxes", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.True(controller.Snapshot().Notice.Contains("Applied: Boxes", StringComparison.Ordinal), "Candidate must distinguish actual appearance");
        Send(controller, sequence++, OverlayInput.NextChoice);
        TestAssert.Equal("Boxes", controller.Snapshot().Rows[1].Value);
        Send(controller, sequence++, OverlayInput.NextChoice);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Text", controller.Snapshot().Appearance?.BooleanDisplay);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.PreviousChoice);
        TestAssert.Equal("Left", controller.Snapshot().Rows[2].Value);
        TestAssert.Equal("Right", controller.Snapshot().Appearance?.BoolBoxLocation);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Left", controller.Snapshot().Appearance?.BoolBoxLocation);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void MissingChoiceBindingsDisableRowsAndExplainExactSavedActionsWithoutFallback() {
        var (controller, session, sequence, clock) = Open(false);
        var state = controller.Snapshot();
        TestAssert.True(!state.Rows[1].Enabled && !state.Rows[2].Enabled, "Both source choices require verified bindings");
        TestAssert.True(state.Notice.Contains("+actionslot 3", StringComparison.Ordinal)
            && state.Notice.Contains("+actionslot 4", StringComparison.Ordinal), "Missing keys must name the required saved actions");
        Send(controller, sequence++, OverlayInput.NextChoice);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        TestAssert.Equal("Scroll Animation Time (ms)", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
        TestAssert.True(!controller.Snapshot().Rows[3].Enabled, "The numeric slider also requires choice bindings");
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        clock.Advance(TimeSpan.FromMilliseconds(430));
        TestAssert.True(controller.Snapshot().Appearance?.QuickExit == true, "Booleans remain available without choice bindings");
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void DraftsResumeAcrossPageBackAndCloseReopenButNewControllersStartFresh() {
        var (controller, _, sequence, clock) = Open(true);
        Send(controller, sequence++, OverlayInput.NextChoice);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal("Text", controller.Snapshot().Rows[1].Value);
        TestAssert.Equal("Boxes", controller.Snapshot().Appearance?.BooleanDisplay);
        var (fresh, _, _, _) = Open(true);
        TestAssert.Equal("Boxes", fresh.Snapshot().Rows[1].Value);
    }

    public static void StaleForeignUnknownAndFocusLostDraftOrCommitInputsNeverReplay() {
        var (controller, session, sequence, clock) = Open(true);
        int reads = 0;
        ulong failed = sequence++;
        controller.Consume(new(failed, 1, 42, OverlayInput.NextChoice, true), () => ++reads == 1);
        Send(controller, failed, OverlayInput.NextChoice);
        controller.Consume(new(sequence++, 1, 99, OverlayInput.NextChoice, true), () => true);
        controller.Consume(new(sequence++, 1, 42, null, false), () => true);
        TestAssert.Equal("Boxes", controller.Snapshot().Rows[1].Value);
        Send(controller, sequence++, OverlayInput.NextChoice);
        reads = 0;
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Select, true), () => ++reads == 1);
        TestAssert.Equal("Boxes", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.Equal("Text", controller.Snapshot().Rows[1].Value);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Text", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void ChoiceDirectionsCannotToggleBooleanRowsOrAnyGameFeature() {
        var (controller, session, sequence, clock) = Open(true);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Down);
        TestAssert.Equal("Quick Exit", controller.Snapshot().Rows[controller.Snapshot().SelectedIndex].Label);
        Send(controller, sequence++, OverlayInput.NextChoice);
        Send(controller, sequence++, OverlayInput.PreviousChoice);
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Up);
        session.State = session.State with { Preview = false, Connected = true, CanConnect = false,
            CanPersonalGodmode = true, PersonalGodmodeValue = "OFF" };
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.True(controller.Snapshot().Rows[0].Enabled, "The test must inspect an eligible game feature row");
        Send(controller, sequence++, OverlayInput.NextChoice);
        Send(controller, sequence++, OverlayInput.PreviousChoice);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void DraftHelpersInitializeActualValuesAndRejectUnknownIndicesAndDirections() {
        var appearance = MenuAppearance.Default with { BooleanDisplay = "Text Color", BoolBoxLocation = "Left" };
        var drafts = MenuChoiceDrafts.FromAppearance(appearance);
        TestAssert.Equal("Text Color", drafts.Candidate(0));
        TestAssert.Equal("Left", drafts.Candidate(1));
        TestAssert.True(drafts.TryMove(0, 1, out var moved), "Supported move must wrap");
        TestAssert.Equal("Boxes", moved.Candidate(0));
        foreach (var pair in new[] { (7, 1), (2, -1), (0, 0), (0, 2), (-1, 1) }) {
            TestAssert.True(!drafts.TryMove(pair.Item1, pair.Item2, out var unchanged), "Unknown adjustment must reject");
            TestAssert.Equal(drafts, unchanged);
        }
    }

    public static void NavigationRevisionDisarmsChangedContextsButStaysStableForDraftsAndHeartbeats() {
        var (controller, _, sequence, clock) = Open(true);
        ulong initial = controller.Snapshot().NavigationRevision;
        TestAssert.True(initial > 0, "Real controller snapshots must expose their navigation revision");
        Send(controller, sequence++, OverlayInput.NextChoice);
        TestAssert.Equal(initial, controller.Snapshot().NavigationRevision);
        controller.Snapshot();
        TestAssert.Equal(initial, controller.Snapshot().NavigationRevision);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(initial, controller.Snapshot().NavigationRevision);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Up);
        ulong returned = controller.Snapshot().NavigationRevision;
        TestAssert.True(returned > initial, "Moving away and returning to the same row must invalidate held choice context");
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Select);
        ulong pageReturned = controller.Snapshot().NavigationRevision;
        TestAssert.True(pageReturned > returned, "Back and return must retain a distinct revision");
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.True(controller.Snapshot().NavigationRevision > pageReturned, "Close and reopen must disarm old held choice keys");
        using var json = System.Text.Json.JsonDocument.Parse(controller.Snapshot().Serialize());
        TestAssert.Equal(controller.Snapshot().NavigationRevision, json.RootElement.GetProperty("navigationRevision").GetUInt64());
        var generic = StateDocument.Create(42, 1, false, new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        TestAssert.Equal(0UL, generic.NavigationRevision);
    }

    public static void FreshUseWithoutMovingADraftCannotSilentlyCycleAChoice() {
        var (controller, session, sequence, clock) = Open(true);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        TestAssert.Equal("Boxes", controller.Snapshot().Rows[1].Value);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void LocalDraftAndCommitRemainSeparateFromAPendingGameOperation() {
        var session = new SessionBoundary { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, true, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        // Locate the implemented child by label so unrelated rows cannot change this setup
        int preferences = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Menu Preferences");
        TestAssert.True(preferences >= 0, "Customization must expose Menu Preferences");
        for (int index = 0; index < preferences; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.NextChoice);
        TestAssert.Equal("Boxes", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.Equal("Text", controller.Snapshot().Rows[1].Value);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Text", controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.True(controller.Busy, "Local choices cannot complete or replace the pending game operation");
        controller.StartQueuedAction();
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(MenuActionKind.Connect, session.Executed[0].Kind);
        session.Pending.SetResult();
        controller.ObserveCompletion();
    }

    private static (BackendController Controller, SessionBoundary Session, ulong Sequence, ManualTimeProvider Clock) Open(bool available) {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, available, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        // Locate the implemented child by label so unrelated rows cannot change this setup
        int preferences = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Menu Preferences");
        TestAssert.True(preferences >= 0, "Customization must expose Menu Preferences");
        for (int index = 0; index < preferences; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Down);
        return (controller, session, sequence, clock);
    }
    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true), () => true);
}
