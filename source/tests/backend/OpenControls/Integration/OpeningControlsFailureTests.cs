using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class OpeningControlsFailureTests {
    public static void ClockAndMenuCounterFailuresKeepTupleSelectorsAndRefreshOwnershipUnchanged() {
        foreach (bool exhausted in new[] { false, true }) {
            foreach (string label in new[] { "Use", "Bind Slot" }) {
                var proof = MainOpeningChordTests.AllActions();
                MainOpeningChord.TryCreateDefault(proof, out var chord, out _);
                var view = new OpeningControlsView(chord!, chord!, OpeningControlDraft.Default,
                    OpeningControlDraft.Default, proof, true);
                var navigation = new OverlayNavigation(0, exhausted ? ulong.MaxValue - 3 : 0);
                navigation.SetRows(OpeningControlsMenuRows.Build(view));
                navigation.Open();
                navigation.OpenPage(MenuPage.OpenControls);
                // Publish rows for the new page before selecting its boundary-test action
                navigation.SetRows(OpeningControlsMenuRows.Build(view));
                navigation.SelectIndex(navigation.Rows.ToList().FindIndex(row => row.Label == label));
                var clock = new MenuInitialOpenFailureTests.FaultClock();
                var controller = new BackendController(42, new SessionBoundary(), navigation, false,
                    choiceControlsAvailable: true, clock: clock, openingActions: proof);
                var before = controller.Snapshot();
                clock.Fail = !exhausted;
                var command = new CommandEnvelope(1, 1, 42, OverlayInput.Select, true, before.NavigationRevision, 1);
                if (exhausted) TestAssert.Throws<OverflowException>(() => controller.Consume(command, () => true));
                else TestAssert.Throws<InvalidOperationException>(() => controller.Consume(command, () => true));
                clock.Fail = false;
                var after = controller.Snapshot();
                TestAssert.Equal(before.MenuRevision, after.MenuRevision);
                TestAssert.Equal(before.NavigationRevision, after.NavigationRevision);
                TestAssert.Equal(before.Rows[1].Value, after.Rows[1].Value);
                TestAssert.Equal(OpeningControlsRig.Revision(before), OpeningControlsRig.Revision(after));
                TestAssert.True(OpeningControlsRig.Actions(before).SequenceEqual(OpeningControlsRig.Actions(after)), "Rejected admission must preserve the immutable tuple");
                TestAssert.True(after.MenuPreferenceRefresh is null, "A failed preflight cannot own a timer");
            }
        }
    }

    public static void ExistingOpeningContinuesThroughCapturedRowsAndStopsOnlyAtThe180MillisecondRedraw() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Use");
        rig.Send(OverlayInput.Toggle);
        rig.Send(OverlayInput.Toggle);
        rig.Wait(50);
        rig.Wait(550);
        var before = rig.Current();
        TestAssert.True(before.MenuOpening is not null, "The retained sixteen-row editor still fades after the input wait");
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(before.MenuOpening, rig.Current().MenuOpening);
        rig.Wait(179);
        TestAssert.Equal(before.MenuOpening!.Revision, rig.Current().MenuOpening!.Revision);
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuOpening is null, "Only the actual row redraw retires the old opening");
        TestAssert.Equal(before.MenuRevision + 1, rig.Current().MenuRevision);
        rig.NoGameWork();
    }

    public static void SelectorRollbackRestartsIts250MillisecondWaitAndLatePollingNeverAddsARedraw() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Select("Bind Slot");
        var applied = rig.Current();
        rig.Wait(100);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(249);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is not null, "A selector rollback retains the full remaining stage");
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Only the source selector wait restarts");
        TestAssert.Equal(applied.MenuRevision, rig.Current().MenuRevision);
        rig.Select("Bind Slot");
        var late = rig.Current();
        rig.Wait(1000);
        TestAssert.Equal(late.MenuRevision, rig.Current().MenuRevision);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Late polling cannot redraw an immediate selector twice");
        rig.NoGameWork();
    }

    public static void AwayAndBackCommandsCannotUseTheirOldCursorEpochToStageOrApplyControls() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Menu");
        var old = rig.Current();
        rig.Send(OverlayInput.Back);
        rig.Select("Open Controls");
        var returned = rig.Current();
        foreach (var input in new[] { OverlayInput.NextChoice, OverlayInput.PreviousChoice, OverlayInput.Select }) {
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true,
                old.NavigationRevision, OpeningControlsRig.Revision(old)), () => true);
            var rejected = rig.Current();
            TestAssert.Equal(returned.Rows[0].Value, rejected.Rows[0].Value);
            TestAssert.Equal(returned.NavigationRevision, rejected.NavigationRevision);
            TestAssert.True(rejected.MenuPreferenceRefresh is null, "Stale editor input cannot become a delayed callback");
        }
        rig.NoGameWork();
    }

    public static void FailedPublicationAndFocusProjectionCannotRollBackAnAlreadyCommittedPolicy() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Select("Use");
        bool fail = true;
        StateDocument? published = null;
        var publication = new MenuPhasePublication(state => {
            if (fail) throw new IOException("Owned publication failure");
            published = state;
        }, rig.Clock);
        var pending = rig.Current();
        TestAssert.Throws<IOException>(() => publication.Publish(pending, false));
        TestAssert.True(publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false), "A failed publication remains due");
        fail = false;
        publication.Publish(rig.Current(), false);
        TestAssert.True(!published!.IsOpen && published.MenuPreferenceRefresh is null, "A hidden display carries no active refresh");
        TestAssert.True(OpeningControlsRig.Actions(published).SequenceEqual(new[] { 8, 4 }), "The hidden policy is actual even while row values are old");
        rig.Wait(180);
        publication.Publish(rig.Current(), true);
        TestAssert.Equal("ON", published!.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.True(OpeningControlsRig.Actions(published).SequenceEqual(new[] { 8, 4 }), "Retrying publication cannot apply a chord twice");
        rig.Controller.RequestShutdown();
        var closed = rig.Controller.ClosedState();
        TestAssert.True(closed.MenuPreferenceRefresh is null && !closed.IsOpen, "Shutdown releases local capture ownership");
        TestAssert.True(OpeningControlsRig.Actions(closed).SequenceEqual(new[] { 8, 4 }), "Shutdown cannot resurrect the captured previous policy");
    }
}
