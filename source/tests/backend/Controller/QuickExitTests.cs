using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using System.Text;
using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class QuickExitTests {
    public static void QuickExitDefaultsFalseAndAppendsItsPreferenceAndProtocolValues() {
        TestAssert.True(!MenuAppearance.Default.QuickExit, "Quick Exit is optional and defaults off");
        TestAssert.Equal(6, (int)MenuPreference.OptionCounter);
        TestAssert.Equal(7, (int)MenuPreference.QuickExit);
        TestAssert.Equal(6, (int)OverlayInput.NextChoice);
        TestAssert.Equal(7, (int)OverlayInput.Close);
        var (controller, _, _, _) = Preferences();
        var snapshot = controller.Snapshot();
        TestAssert.Equal(12, snapshot.Rows.Count);
        TestAssert.Equal("Quick Exit", snapshot.Rows[4].Label);
        TestAssert.Equal("OFF", snapshot.Rows[4].Value);
        TestAssert.True(snapshot.Rows[4].Enabled, "Local Quick Exit remains available in preview");
        using var json = JsonDocument.Parse(snapshot.Serialize());
        TestAssert.True(!json.RootElement.GetProperty("appearance").GetProperty("quickExit").GetBoolean(), "The public field is a boolean");
        var old = JsonSerializer.Deserialize<MenuAppearance>("{\"booleanDisplay\":\"Text\",\"boolBoxLocation\":\"Left\",\"instructionsVisible\":false,\"optionCounter\":true,\"coloredCursor\":false,\"spotlightCursor\":false,\"largeCursor\":true}",
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        TestAssert.True(old is { QuickExit: false, BooleanDisplay: "Text", BoolBoxLocation: "Left", LargeCursor: true },
            "An older seven-field appearance keeps its values and defaults Quick Exit off");
    }

    public static void QuickExitToggleChangesOnlyLocalAppearanceAndNeverDispatchesOrShutsDown() {
        var (controller, session, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        TestAssert.Equal(MenuAppearance.Default with { QuickExit = true }, controller.Snapshot().Appearance);
        TestAssert.Equal("ON", controller.Snapshot().Rows[4].Value);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal(MenuAppearance.Default, controller.Snapshot().Appearance);
        controller.StartQueuedAction();
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
        TestAssert.True(!controller.ExitRequested, "The appearance option cannot request session exit");
    }

    public static void FocusedNestedCloseRetainsPageCursorAndParentPathForReopening() {
        var (controller, session, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        ulong revision = controller.Snapshot().NavigationRevision;
        Send(controller, sequence++, OverlayInput.Close);
        var closed = controller.Snapshot();
        TestAssert.True(!closed.IsOpen && !closed.IsRootMenu, "Local close hides the actual nested page");
        TestAssert.Equal("Menu Preferences", closed.Title);
        TestAssert.Equal(4, closed.SelectedIndex);
        TestAssert.True(closed.NavigationRevision > revision, "Closing invalidates old input context");
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal("Menu Preferences", controller.Snapshot().Title);
        TestAssert.Equal(4, controller.Snapshot().SelectedIndex);
        Send(controller, sequence++, OverlayInput.Back);
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
        TestAssert.True(!controller.ExitRequested, "Close cannot become Exit after reopening");
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void RootCloseUsesNavigationIdentityRatherThanTheTitleAndNeverRequestsExit() {
        var (controller, _, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        TestAssert.True(controller.Snapshot().IsRootMenu, "Main navigation is the root menu");
        Send(controller, sequence++, OverlayInput.Close);
        TestAssert.True(!controller.Snapshot().IsOpen && controller.Snapshot().IsRootMenu,
            "Root close must remain a local root snapshot");
        TestAssert.True(!controller.ExitRequested, "Root close cannot dispatch the Exit menu action");
        var generic = StateDocument.Create(42, 1, false, new("Any title", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        TestAssert.True(generic.IsRootMenu, "Older generic producers retain the root default");
        using var json = JsonDocument.Parse(controller.Snapshot().Serialize());
        TestAssert.True(json.RootElement.GetProperty("isRootMenu").GetBoolean(), "Root identity must serialize explicitly");
    }

    public static void OffStaleForeignUnknownAndImmediateFocusLossCloseCommandsAreAcknowledgedWithoutClosing() {
        var (controller, session, sequence, clock) = Preferences();
        Send(controller, sequence++, OverlayInput.Close);
        TestAssert.True(controller.Snapshot().IsOpen, "Quick Exit off cannot use the close route");
        SelectQuickExit(controller, ref sequence, clock);
        ulong stale = sequence - 1;
        Send(controller, stale, OverlayInput.Close);
        controller.Consume(new(sequence++, 1, 99, OverlayInput.Close, true, controller.Snapshot().NavigationRevision), () => true);
        controller.Consume(new(sequence++, 1, 42, null, false), () => true);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Close, true, controller.Snapshot().NavigationRevision), () => false);
        int reads = 0;
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Close, true, controller.Snapshot().NavigationRevision), () => ++reads == 1);
        TestAssert.True(controller.Snapshot().IsOpen, "Every rejected close must leave the menu open");
        TestAssert.Equal(sequence - 1, controller.LastSequence);
        Send(controller, sequence++, OverlayInput.Close);
        TestAssert.True(!controller.Snapshot().IsOpen, "A new deliberate focused close remains available");
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void CloseDuringPendingPlayerSelectionCannotReopenTheMenuOnCompletion() {
        var (controller, session, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        session.State = session.State with { Preview = false, Connected = true, CanConnect = false,
            CanRefreshPlayers = true, CanSelectPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        TestAssert.True(controller.Busy, "The regression requires a pending selection");
        Send(controller, sequence++, OverlayInput.Close);
        TestAssert.True(!controller.Snapshot().IsOpen && controller.Busy, "Closing cannot complete or cancel the pending session work");
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending.SetResult();
        TestAssert.True(!controller.Snapshot().IsOpen, "Delayed selection cannot reopen a closed menu");
        TestAssert.Equal("Players Menu", controller.Snapshot().Title);
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
        TestAssert.True(!controller.ExitRequested, "Pending close must not trigger session cleanup");
    }

    public static void CloseFromPlayersIsRejectedWhenSnapshotCompletesSelectionIntoAnotherPage() {
        var (controller, session, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Back);
        session.State = session.State with { Preview = false, Connected = true, CanConnect = false,
            CanRefreshPlayers = true, CanSelectPlayers = true, Players = new[] { new OverlayPlayer(1, "Friend") } };
        session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        controller.StartQueuedAction();
        ulong observedRevision = controller.Snapshot().NavigationRevision;

        // Consume must observe completed selection before deciding whether Close still fits
        session.State = session.State with { SelectedSlot = 1, SelectedName = "Friend", CanTeammateGodmode = true };
        session.Pending.SetResult();
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Close, true, observedRevision), () => true);
        var selected = controller.Snapshot();
        TestAssert.Equal("Friend", selected.Title);
        TestAssert.True(selected.IsOpen && !controller.Busy, "The old Players close cannot hide the newly selected page");
        TestAssert.True(selected.NavigationRevision > observedRevision, "Completion must invalidate the old close context");
        TestAssert.Equal(sequence - 1, controller.LastSequence);
        TestAssert.Equal(1, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void CloseProtocolParsesExactlyAndTapBackKeepsItsExistingPageBehavior() {
        var parsed = CommandEnvelope.Parse(Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"close\",\"navigationRevision\":0}"));
        TestAssert.True(parsed?.Valid == true && parsed.Input == OverlayInput.Close, "Known local close input must parse");
        var (controller, _, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        Send(controller, sequence++, OverlayInput.Back);
        TestAssert.True(controller.Snapshot().IsOpen, "Tap Back still returns to the parent");
        TestAssert.Equal("Menu Customization", controller.Snapshot().Title);
    }

    public static void CloseEpochRejectsMissingMalformedAndUnknownFieldsButAcceptsUnsignedBoundaries() {
        foreach (string epoch in new[] { "0", "18446744073709551615" }) {
            var parsed = ParseClose(1, ",\"navigationRevision\":" + epoch);
            TestAssert.True(parsed is { Valid: true, Input: OverlayInput.Close }, "Exact unsigned epochs must parse");
            TestAssert.Equal(ulong.Parse(epoch), parsed!.NavigationRevision);
        }
        foreach (string suffix in new[] { "", ",\"navigationRevision\":null", ",\"navigationRevision\":true",
            ",\"navigationRevision\":\"1\"", ",\"navigationRevision\":-1", ",\"navigationRevision\":1.0",
            ",\"navigationRevision\":18446744073709551616", ",\"navigationRevision\":0,\"navigationRevision\":1",
            ",\"navigationRevision\":0,\"other\":1" }) {
            var parsed = ParseClose(2, suffix);
            TestAssert.True(parsed is { Sequence: 2, Valid: false }, "A recoverable rejected epoch must still be acknowledged");
        }
        var legacy = CommandEnvelope.Parse(Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":3,\"processId\":42,\"input\":\"back\"}"));
        TestAssert.True(legacy is { Valid: true, NavigationRevision: null }, "Legacy inputs do not require an epoch");
        var optional = CommandEnvelope.Parse(Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":4,\"processId\":42,\"input\":\"back\",\"navigationRevision\":7}"));
        TestAssert.True(optional is { Valid: true, NavigationRevision: 7 }, "Other inputs may carry a strict optional epoch");
        var malformed = CommandEnvelope.Parse(Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":5,\"processId\":42,\"input\":\"back\",\"navigationRevision\":true}"));
        TestAssert.True(malformed is { Sequence: 5, Valid: false }, "Optional epochs cannot change type on legacy inputs");
    }

    public static void CloseFromAnEarlierPageOrReopenedMenuIsAcknowledgedWithoutClosing() {
        var (controller, session, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        ulong oldRevision = controller.Snapshot().NavigationRevision;
        Send(controller, sequence++, OverlayInput.Back);
        Send(controller, sequence++, OverlayInput.Select);
        TestAssert.Equal("Menu Preferences", controller.Snapshot().Title);
        TestAssert.Equal(4, controller.Snapshot().SelectedIndex);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Close, true, oldRevision), () => true);
        TestAssert.True(controller.Snapshot().IsOpen, "Returning to the same page cannot restore an old close context");

        oldRevision = controller.Snapshot().NavigationRevision;
        Send(controller, sequence++, OverlayInput.Close);
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        ulong rejectedSequence = sequence++;
        controller.Consume(new(rejectedSequence, 1, 42, OverlayInput.Close, true, oldRevision), () => true);
        TestAssert.True(controller.Snapshot().IsOpen, "Reopening must invalidate a delayed close packet");
        TestAssert.Equal(rejectedSequence, controller.LastSequence);
        controller.Consume(new(rejectedSequence, 1, 42, OverlayInput.Close, true, controller.Snapshot().NavigationRevision), () => true);
        TestAssert.True(controller.Snapshot().IsOpen, "Replacing an acknowledged stale packet cannot replay it");
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void MissingOrMalformedCloseEpochIsAcknowledgedAndCannotReplayAfterCorrection() {
        var (controller, _, sequence, clock) = Preferences();
        SelectQuickExit(controller, ref sequence, clock);
        controller.Consume(new(sequence++, 1, 42, OverlayInput.Close, true), () => true);
        TestAssert.True(controller.Snapshot().IsOpen, "Direct callers cannot bypass the mandatory close epoch");
        foreach (string suffix in new[] { "", ",\"navigationRevision\":false", ",\"navigationRevision\":-1" }) {
            ulong rejectedSequence = sequence++;
            controller.Consume(ParseClose(rejectedSequence, suffix)!, () => true);
            TestAssert.True(controller.Snapshot().IsOpen, "Invalid close envelopes must leave navigation untouched");
            TestAssert.Equal(rejectedSequence, controller.LastSequence);
            controller.Consume(new(rejectedSequence, 1, 42, OverlayInput.Close, true, controller.Snapshot().NavigationRevision), () => true);
            TestAssert.True(controller.Snapshot().IsOpen, "An invalid acknowledged packet cannot be corrected in place");
        }
        Send(controller, sequence++, OverlayInput.Close);
        TestAssert.True(!controller.Snapshot().IsOpen, "A new close with the actual epoch must still work");
    }

    private static CommandEnvelope? ParseClose(ulong sequence, string suffix) => CommandEnvelope.Parse(
        Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":" + sequence + ",\"processId\":42,\"input\":\"close\"" + suffix + "}"));

    private static (BackendController Controller, SessionBoundary Session, ulong Sequence, ManualTimeProvider Clock) Preferences() {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, true, clock: clock);
        ulong sequence = 1;
        Send(controller, sequence++, OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, clock);
        for (int index = 0; index < 2; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        // Locate the implemented child by label so unrelated rows cannot change this setup
        int preferences = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Menu Preferences");
        TestAssert.True(preferences >= 0, "Customization must expose Menu Preferences");
        for (int index = 0; index < preferences; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        return (controller, session, sequence, clock);
    }
    private static void SelectQuickExit(BackendController controller, ref ulong sequence, ManualTimeProvider clock) {
        int quickExit = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Quick Exit");
        TestAssert.True(quickExit >= 0, "Quick Exit must remain an actual supported preference");
        for (int index = 0; index < quickExit; index++) Send(controller, sequence++, OverlayInput.Down);
        Send(controller, sequence++, OverlayInput.Select);
        // Close behavior is checked after the source boolean monitor cooldown
        clock.Advance(TimeSpan.FromMilliseconds(430)); controller.Snapshot();
    }
    private static void Send(BackendController controller, ulong sequence, OverlayInput input) =>
        controller.Consume(new(sequence, 1, 42, input, true, input == OverlayInput.Close ? controller.Snapshot().NavigationRevision : null), () => true);
}
