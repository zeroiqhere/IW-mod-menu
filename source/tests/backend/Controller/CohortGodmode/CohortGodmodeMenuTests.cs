using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The source God Mode checkbox cannot borrow a personal, peer, or lobby-ammo capability
internal static class CohortGodmodeMenuTests {
    public static void CompleteCohortEnablesOnlyTheSourceGodModeRowWithItsExactPrivateTarget() {
        var fixture = new CohortGodmodeMenuFixture();
        foreach (string value in new[] { "ON", "OFF", "UNKNOWN" }) {
            var state = fixture.Session.State with { AllPlayersGodmodeValue = value };
            var rows = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state).Rows;
            TestAssert.Equal(12, rows.Count);
            TestAssert.Equal("God Mode", rows[5].Label);
            TestAssert.Equal(value, rows[5].Value);
            TestAssert.Equal(value != "UNKNOWN", rows[5].Enabled);
            TestAssert.Equal(MenuActionKind.ToggleAllPlayersGodmode, rows[5].Action.Kind);
            TestAssert.Equal(MenuPage.AllPlayers, rows[5].Action.Page);
            TestAssert.True(ReferenceEquals(state.AllPlayersGodmodeTarget, rows[5].Action.ExpectedCohort),
                "The action must retain the exact private cohort receipt");
            TestAssert.True(rows.Take(11).Where((_, index) => index != 5).All(row =>
                !row.Enabled && row.Value == "UNAVAILABLE" && row.Action.Kind == MenuActionKind.None),
                "Other unimplemented all-player callbacks must remain inert");
        }
    }

    public static void MissingCohortOrBlockedViewsCannotBorrowOtherFeatureCapabilities() {
        var fixture = new CohortGodmodeMenuFixture();
        foreach (var state in new[] {
            fixture.Session.State with { AllPlayersGodmodeTarget = null },
            fixture.Session.State with { Connected = false },
            fixture.Session.State with { Preview = true }
        }) {
            var row = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state).Rows[5];
            TestAssert.True(!row.Enabled && row.Action.Kind == MenuActionKind.None,
                "Only current whole-cohort authority may dispatch the aggregate callback");
        }
        var ineligible = OverlayMenuBuilder.Build(MenuPage.AllPlayers,
            fixture.Session.State with { CanAllPlayersGodmode = false }).Rows[5];
        TestAssert.True(!ineligible.Enabled && ineligible.Value == "OFF"
            && ineligible.Action.Kind == MenuActionKind.ToggleAllPlayersGodmode,
            "A retained dead or ineligible owner must disclose confirmed OFF without granting another enable");
        var busy = OverlayMenuBuilder.Build(MenuPage.AllPlayers, fixture.Session.State with { Busy = true }).Rows[5];
        TestAssert.True(!busy.Enabled && busy.Value == "UNKNOWN", "A busy supported cohort keeps its identity but cannot accept input");
        var missing = OverlayMenuBuilder.Build(MenuPage.AllPlayers,
            fixture.Session.State with { AllPlayersGodmodeTarget = null }).Rows[5];
        TestAssert.Equal("UNAVAILABLE", missing.Value);
    }

    public static void CohortReceiptNeverLeaksThroughActionStateOrPublishedFrameSerialization() {
        var fixture = new CohortGodmodeMenuFixture();
        var row = OverlayMenuBuilder.Build(MenuPage.AllPlayers, fixture.Session.State).Rows[5];
        TestAssert.True(row.Action.ExpectedCohort is not null, "The source action must carry authority before serialization");
        foreach (string wire in new[] { JsonSerializer.Serialize(row.Action), JsonSerializer.Serialize(fixture.Session.State) }) {
            TestAssert.True(!wire.Contains("ExpectedCohort") && !wire.Contains("AllPlayersGodmodeTarget")
                && !wire.Contains("OwnerToken") && !wire.Contains("AccountId"),
                "Private actor and cohort facts cannot enter the wire");
        }
        fixture.EnterAllPlayers();
        string packet = System.Text.Encoding.UTF8.GetString(fixture.Controller.Snapshot().Serialize());
        TestAssert.True(!packet.Contains("ExpectedCohort") && !packet.Contains("AllPlayersGodmodeTarget"),
            "Published rows must contain display fields only");
    }

    public static void ObservationPollingFollowsOnlyTheOpenMainAllPlayersVisitAndStopsOnCloseOrShutdown() {
        var fixture = new CohortGodmodeMenuFixture();
        fixture.Controller.Snapshot();
        fixture.OpenMain();
        TestAssert.True(!fixture.Session.ObservationFlags.Contains(true), "Root Main must not request cohort polling");
        fixture.EnterAllPlayers();
        TestAssert.True(fixture.Session.ObservationFlags.LastOrDefault(), "The actual All Players visit must enable polling");
        int changes = fixture.Session.ObservationFlags.Count;
        fixture.Controller.Snapshot();
        fixture.Controller.Snapshot();
        TestAssert.Equal(changes, fixture.Session.ObservationFlags.Count);
        fixture.Send(OverlayInput.Back);
        TestAssert.True(!fixture.Session.ObservationFlags.Last(), "Back must disable observation immediately");
        fixture.Navigation.OpenPage(MenuPage.AllPlayers);
        fixture.Controller.Snapshot();
        fixture.Send(OverlayInput.Toggle);
        TestAssert.True(!fixture.Session.ObservationFlags.Last(), "Close must disable observation immediately");
        fixture.OpenMain();
        fixture.Navigation.OpenPage(MenuPage.AllPlayers);
        fixture.Controller.Snapshot();
        fixture.Controller.RequestShutdown();
        TestAssert.True(!fixture.Session.ObservationFlags.Last(), "Shutdown must disarm polling without waiting for frame publication");
    }

    public static void ReplacedCohortBetweenQueueAndDispatchCannotStartOrReplayTheAggregateAction() {
        var fixture = new CohortGodmodeMenuFixture();
        fixture.EnterAllPlayers();
        fixture.SelectGodmode();
        TestAssert.True(fixture.Controller.Busy, "The real selection must queue before execution");
        fixture.Session.State = fixture.Session.State with { AllPlayersGodmodeTarget = CohortGodmodeMenuFixture.Target() };
        fixture.Controller.StartQueuedAction();
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(0, fixture.Session.Executed.Count);
        TestAssert.True(!fixture.Controller.Busy, "A stale private cohort intent must be dropped rather than replayed");
    }

    public static void AggregateBooleanCallbackUsesTheSource180RedrawAnd430InputLock() {
        var fixture = new CohortGodmodeMenuFixture();
        fixture.EnterAllPlayers();
        var initial = fixture.Controller.Snapshot();
        TestAssert.Equal("OFF", initial.MenuBooleanPaint!.Rows.Single(row => row.Index == 5).Value);
        fixture.SelectGodmode();
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(1, fixture.Session.Executed.Count);
        fixture.Session.State = fixture.Session.State with { AllPlayersGodmodeValue = "ON" };
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(179));
        var pending = fixture.Controller.Snapshot();
        TestAssert.Equal("pending", pending.MenuOptionCallback!.Phase);
        TestAssert.Equal("ON", pending.Rows[5].Value);
        TestAssert.Equal("OFF", pending.MenuBooleanPaint!.Rows.Single(row => row.Index == 5).Value);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var redraw = fixture.Controller.Snapshot();
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal("ON", redraw.MenuBooleanPaint!.Rows.Single(row => row.Index == 5).Value);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(249));
        fixture.Send(OverlayInput.Select);
        TestAssert.Equal(1, fixture.Session.Executed.Count);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(fixture.Controller.Snapshot().MenuOptionCallback is null,
            "The source caller must unlock after 430 ms without another redraw");
    }

    public static void HeldAggregateWorkPublishesUnknownWithoutErasingTheCachedSourceCheckbox() {
        var fixture = new CohortGodmodeMenuFixture();
        fixture.EnterAllPlayers();
        fixture.Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.SelectGodmode();
        fixture.Controller.StartQueuedAction();
        var pending = fixture.Controller.Snapshot();
        TestAssert.True(fixture.Controller.Busy && !pending.Rows[5].Enabled, "Held cohort work must disable another input");
        TestAssert.Equal("UNKNOWN", pending.Rows[5].Value);
        TestAssert.Equal("OFF", pending.MenuBooleanPaint!.Rows.Single(row => row.Index == 5).Value);
        fixture.Session.Pending.SetResult();
        fixture.Controller.ObserveCompletion();

        // An idle source worker can disarm capability without a controller-owned action
        var worker = new CohortGodmodeMenuFixture();
        worker.EnterAllPlayers();
        var before = worker.Controller.Snapshot();
        worker.Session.State = worker.Session.State with {
            CanAllPlayersGodmode = false, AllPlayersGodmodeValue = "UNKNOWN", Busy = false
        };
        var observing = worker.Controller.Snapshot();
        TestAssert.True(!worker.Controller.Busy && !observing.Rows[5].Enabled,
            "A retained observation poll must not grant another aggregate toggle");
        TestAssert.Equal("UNKNOWN", observing.Rows[5].Value);
        TestAssert.True(observing.MenuBooleanPaint is not null, "The idle poll cannot retire the existing source checkbox");
        TestAssert.Equal(before.MenuBooleanPaint!.Revision, observing.MenuBooleanPaint!.Revision);
        TestAssert.Equal("OFF", observing.MenuBooleanPaint.Rows.Single(row => row.Index == 5).Value);
        TestAssert.Equal(before.MenuRevision!.Value, observing.MenuRevision!.Value);
        worker.Session.State = worker.Session.State with { AllPlayersGodmodeValue = "OFF" };
        var deadOff = worker.Controller.Snapshot();
        TestAssert.True(!deadOff.Rows[5].Enabled && deadOff.Rows[5].Value == "OFF",
            "Life-ineligible confirmed OFF must stay visible without granting ON");
    }

    public static void CohortCheckboxCacheOwnsItsCopyAndRejectsInvalidValues() {
        var fixture = new CohortGodmodeMenuFixture();
        var rows = OverlayMenuBuilder.Build(MenuPage.AllPlayers, fixture.Session.State).Rows.ToArray();
        var cache = MenuBooleanPaintCache.Capture(1, rows);
        TestAssert.True(cache is not null, "The source whole-cohort action must be captured as BOOL paint");
        rows[5] = rows[5] with { Value = "ON" };
        TestAssert.Equal("OFF", cache!.Frame.Rows.Single(row => row.Index == 5).Value);
        rows[5] = rows[5] with { Value = "UNAVAILABLE" };
        TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(2, rows));
    }
}
