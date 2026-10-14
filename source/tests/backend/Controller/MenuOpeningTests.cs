using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOpeningTests {
    internal static (ulong Revision, int Elapsed)? Opening(StateDocument state) {
        using var json = JsonDocument.Parse(state.Serialize());
        if (!json.RootElement.TryGetProperty("menuOpening", out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return (value.GetProperty("revision").GetUInt64(), value.GetProperty("elapsedMilliseconds").GetInt32());
    }

    public static void ChangedDesignStartsOpeningAtTheObservedRedrawAndContinuesAfterItsInputCooldown() {
        var rig = Start(); TestAssert.True(Opening(rig.Current()) is null, "Hidden rebuild has no drawn options");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49)); TestAssert.True(Opening(rig.Current()) is null, "Opening cannot begin before redraw");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var first = rig.Current(); var opening = Opening(first);
        TestAssert.True(opening is { Revision: > 0, Elapsed: 0 }, "The actual redraw starts all visible option fades together");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(199)); TestAssert.Equal(199, Opening(rig.Current())!.Value.Elapsed);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Current(); TestAssert.True(ready.MenuDesignTransition is null, "Opening cannot extend the250ms control lock");
        TestAssert.Equal(200, Opening(ready)!.Value.Elapsed); TestAssert.Equal(opening!.Value.Revision, Opening(ready)!.Value.Revision);
        TestAssert.Equal(first.MenuRevision, ready.MenuRevision); TestAssert.Equal(first.NavigationRevision, ready.NavigationRevision); rig.NoGameWork();
    }

    public static void ActualNormalizedCapacityBoundsCompletionWithoutLockingForEveryRowDuration() {
        foreach (bool counter in new[] { false, true }) {
            var rig = new MenuDesignTests.Rig(); rig.Open(); if (counter) {
                rig.Select("Option Counter");
                rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
                rig.Current();
            }
            rig.Stage("Native"); rig.Send(OverlayInput.Select);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var first = rig.Current(); int duration = counter ? 700 : 900;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(duration - 1)); TestAssert.Equal(duration - 1, Opening(rig.Current())!.Value.Elapsed);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Opening(rig.Current()) is null, "The final visible ordinal completes without an extra row delay");
            TestAssert.Equal(first.MenuRevision, rig.Current().MenuRevision); rig.NoGameWork();
        }
    }

    public static void LateOwnerObservationStartsANewDrawAtZeroRatherThanAgingAnUnseenPanel() {
        var rig = Start(); rig.Clock.Advance(TimeSpan.FromMilliseconds(1000)); var first = rig.Current();
        TestAssert.True(first.MenuDesignTransition is null, "The independent monitor cooldown has already elapsed");
        TestAssert.True(Opening(first) is { Elapsed: 0 }, "A late actual redraw starts from zero");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(899)); TestAssert.Equal(899, Opening(rig.Current())!.Value.Elapsed);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); TestAssert.True(Opening(rig.Current()) is null, "A late opening still has a finite visible-row lifetime"); rig.NoGameWork();
    }

    public static void AcceptedCursorMovementCancelsOpeningButRetainsTheScrollPresentationContext() {
        var rig = Start(); AdvanceToReady(rig); var first = rig.Current();
        rig.Send(OverlayInput.Down, false); TestAssert.True(Opening(rig.Current()) is not null, "Rejected focus cannot cancel presentation");
        ulong rejected = rig.Controller.LastSequence; rig.Controller.Consume(new(rejected, 1, 42, OverlayInput.Down, true), () => true);
        TestAssert.True(Opening(rig.Current()) is not null, "A stale input cannot replay");
        rig.Send(OverlayInput.Down); var moved = rig.Current(); TestAssert.True(Opening(moved) is null, "Scrolling takes ownership of current opacity");
        TestAssert.Equal(first.MenuRevision, moved.MenuRevision); TestAssert.True(moved.NavigationRevision > first.NavigationRevision, "The cursor epoch still advances");
        rig.Send(OverlayInput.Up); TestAssert.True(Opening(rig.Current()) is null, "Returning to the same cursor cannot resurrect opening"); rig.NoGameWork();
    }

    public static void DraftAndSameDesignKeepTheEventButANewChangedDesignSupersedesIt() {
        var rig = Start(); AdvanceToReady(rig); var first = Opening(rig.Current())!.Value;
        rig.Send(OverlayInput.NextChoice); TestAssert.Equal(first, Opening(rig.Current())!.Value);
        for (int move = 0; move < 4; move++) rig.Send(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select); TestAssert.Equal(first, Opening(rig.Current())!.Value);
        foreach (var blocked in new[] { OverlayInput.Up, OverlayInput.Down, OverlayInput.Toggle, OverlayInput.Close, OverlayInput.Back }) rig.Send(blocked);
        TestAssert.Equal(first, Opening(rig.Current())!.Value);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250)); TestAssert.Equal(first.Revision, Opening(rig.Current())!.Value.Revision);
        rig.Send(OverlayInput.NextChoice); rig.Send(OverlayInput.Select); TestAssert.True(Opening(rig.Current()) is null, "A new rebuild destroys old option HUDs immediately");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var replacement = Opening(rig.Current())!.Value;
        TestAssert.True(replacement.Revision > first.Revision && replacement.Elapsed == 0, "Each new animated draw has one checked identity"); rig.NoGameWork();
    }

    public static void PageCloseModalAndShutdownCancelWithoutResurrectionOrGameDispatch() {
        foreach (string action in new[] { "back", "toggle", "close", "credits", "shutdown" }) {
            var rig = new MenuDesignTests.Rig(); rig.Open();
            if (action == "close") { rig.Select("Quick Exit"); rig.Clock.Advance(TimeSpan.FromMilliseconds(430)); rig.Current(); }
            rig.Stage("Classic"); rig.Send(OverlayInput.Select); AdvanceToReady(rig);
            if (action == "shutdown") rig.Controller.RequestShutdown();
            else if (action == "toggle") rig.Send(OverlayInput.Toggle);
            else if (action == "close") rig.Send(OverlayInput.Close);
            else { rig.Send(OverlayInput.Back); if (action == "credits") rig.Select("Menu Credits"); }
            TestAssert.True(Opening(rig.Current()) is null, "A replaced or hidden owner cannot retain opening rows");
            rig.Clock.Advance(TimeSpan.FromSeconds(2)); TestAssert.True(Opening(rig.Current()) is null, "Canceled opening cannot return on a later heartbeat"); rig.NoGameWork();
        }
    }

    public static void HiddenOwnerProgressAndPhaseCompletionNeedNoSessionReadOrTaskObservation() {
        var rig = Start(); rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var first = rig.Current();
        var hidden = StateFocusProjection.Apply(first, false); TestAssert.True(Opening(hidden) is null && !hidden.IsOpen, "Focus-hidden packets contain no active fade metadata");
        int reads = rig.Session.MenuStateReads; rig.Session.RejectMenuStateReads = true;
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200)); TestAssert.True(rig.Controller.ReadMenuPublicationPhase() is not null, "Opening continues independently after Design timing retires");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(700)); TestAssert.True(rig.Controller.ReadMenuPublicationPhase() is null, "The owner notices final opacity without session work");
        TestAssert.Equal(reads, rig.Session.MenuStateReads); rig.Session.RejectMenuStateReads = false; TestAssert.True(Opening(rig.Current()) is null, "Focus return joins completed presentation"); rig.NoGameWork();
    }

    internal static MenuDesignTests.Rig Start() { var rig = new MenuDesignTests.Rig(); rig.Open(); rig.Stage("Classic"); rig.Send(OverlayInput.Select); return rig; }
    internal static void AdvanceToReady(MenuDesignTests.Rig rig) { rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Current(); rig.Clock.Advance(TimeSpan.FromMilliseconds(200)); rig.Current(); }
}
