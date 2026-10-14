using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class MenuObservationContextTests {
    public static void VerifiedQuickSnapshotAndFreshSelectUseTheSamePersonalCapability() {
        var fixture = new MenuObservationFixture();
        var frame = fixture.Controller.Snapshot();

        TestAssert.Equal(14, frame.Rows.Count);
        TestAssert.Equal("God Mode", frame.Rows[0].Label);
        TestAssert.Equal("ON", frame.Rows[0].Value);
        TestAssert.True(frame.Rows[0].Enabled, "Verified living Quick keeps the actual personal capability");
        fixture.Send(OverlayInput.Select);
        fixture.Controller.StartQueuedAction();

        TestAssert.Equal(1, fixture.Session.Executed.Count);
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, fixture.Session.Executed[0].Kind);
        TestAssert.True(frame.Rows.Skip(1).All(row => !row.Enabled),
            "Host authority cannot enable any unavailable Quick callback");
    }

    public static void RevokedChangedOrDeadObservationCannotDispatchAnOldLivingRow() {
        foreach (var revoked in new[] {
            LocalPlayerObservation.BindingUnavailable,
            MenuObservationFixture.Observed(LocalPlayerLife.Dead),
            MenuObservationFixture.Observed() with { HasMenu = false },
            MenuObservationFixture.Observed() with { ProcessId = 52 },
            MenuObservationFixture.Observed() with { Identity = null }
        }) {
            var fixture = new MenuObservationFixture();
            TestAssert.Equal("God Mode", fixture.Controller.Snapshot().Rows[0].Label);
            // The first command snapshot still sees the living row; the fresh action check loses it
            fixture.Session.BeforeObservation = count => {
                if (count == 3) fixture.Session.Observation = revoked;
            };

            fixture.Send(OverlayInput.Select);
            fixture.Controller.StartQueuedAction();
            TestAssert.Equal(0, fixture.Session.Executed.Count);
            TestAssert.Equal(1UL, fixture.Controller.LastSequence);
        }
    }

    public static void FocusLostAndReplayedQuickSelectionsStayAcknowledgedWithoutDispatch() {
        var fixture = new MenuObservationFixture();
        fixture.Controller.Snapshot();
        fixture.Send(OverlayInput.Select, focused: false);
        fixture.Controller.StartQueuedAction();
        var replay = new Apparition.Backend.Ipc.CommandEnvelope(1, 1, 51, OverlayInput.Select, true);
        fixture.Controller.Consume(replay, () => true);
        fixture.Controller.StartQueuedAction();

        TestAssert.Equal(1UL, fixture.Controller.LastSequence);
        TestAssert.Equal(0, fixture.Session.Executed.Count);
    }

    public static void NamedSessionHostFalseOrUnknownCannotInventDeveloperAuthority() {
        foreach (bool? role in new bool?[] { false, null }) {
            var fixture = new MenuObservationFixture();
            fixture.Session.Observation = MenuObservationFixture.Observed(host: role);
            var frame = fixture.Controller.Snapshot();

            TestAssert.Equal(12, frame.Rows.Count);
            TestAssert.True(frame.Rows.All(row => row.Label != "Restart Game" && row.Label != "Disconnect"),
                "No inverse session-role value proves Developer or the full GSC host method");
        }
        var deadHost = new MenuObservationFixture();
        deadHost.Session.Observation = MenuObservationFixture.Observed(LocalPlayerLife.Dead);
        var deadFrame = deadHost.Controller.Snapshot();
        TestAssert.True(deadFrame.Rows.Select(row => row.Label).SequenceEqual(new[] { "Respawn", "Restart Game", "Disconnect" }),
            "Known dead host keeps only its source declarations");
    }

    public static void InitialOpenCooldownOwnsTheMonitorLockAndPhaseQueriesDoNotReadTheSession() {
        var fixture = new MenuObservationFixture(MenuSurface.Main, open: false);
        TestAssert.Equal(false, fixture.Controller.ReadMenuObservation().ControlsDisabled);
        fixture.Send(OverlayInput.Toggle);
        TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);
        int before = fixture.Session.ObservationReads;
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixture.Controller.ReadMenuPublicationPhase();
        TestAssert.Equal(before, fixture.Session.ObservationReads);
        fixture.Controller.Snapshot();
        TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);

        fixture.Clock.Advance(TimeSpan.FromMilliseconds(550));
        fixture.Controller.Snapshot();
        TestAssert.Equal(false, fixture.Controller.ReadMenuObservation().ControlsDisabled);
    }

    public static void ModalAndShutdownLocksRetainQuickLifeAndPermissionWithoutGameDispatch() {
        var fixture = new MenuObservationFixture();
        fixture.Navigation.OpenPage(MenuPage.Customization);
        fixture.Controller.Snapshot();
        // Reposition is row3 in the actual seven-row customization inventory
        fixture.Send(OverlayInput.Down);
        fixture.Send(OverlayInput.Down);
        fixture.Send(OverlayInput.Down);
        fixture.Send(OverlayInput.Select);
        var editor = fixture.Controller.Snapshot();
        TestAssert.True(editor.MenuPositionEditor is not null, "The real local editor must be active");
        TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);
        fixture.Send(OverlayInput.FinishMenuPositionEditor, navigationRevision: editor.NavigationRevision);
        TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);

        fixture.Clock.Advance(TimeSpan.FromMilliseconds(150));
        fixture.Controller.Snapshot();
        TestAssert.Equal(false, fixture.Controller.ReadMenuObservation().ControlsDisabled);
        fixture.Controller.RequestShutdown();
        var stopped = fixture.Controller.ReadMenuObservation();
        TestAssert.Equal(true, stopped.ControlsDisabled);
        TestAssert.Equal(true, stopped.HasMenu);
        TestAssert.Equal(LocalPlayerLife.Alive, stopped.Life);
        TestAssert.Equal(0, fixture.Session.Executed.Count);
    }

    public static void PreferenceRebuildRefreshDesignAndCreditsLocksFollowTheirExistingTimelines() {
        var preferences = new[] {
            (Label: "Design", StageChoice: true, Milliseconds: 250),
            (Label: "Quick Exit", StageChoice: false, Milliseconds: 430),
            (Label: "Option Counter", StageChoice: false, Milliseconds: 250),
            (Label: "Stealth UI", StageChoice: false, Milliseconds: 430)
        };
        foreach (var preference in preferences) {
            var fixture = new MenuObservationFixture();
            fixture.Navigation.OpenPage(MenuPage.Preferences);
            var frame = fixture.Controller.Snapshot();
            int index = frame.Rows.ToList().FindIndex(row => row.Label == preference.Label);
            TestAssert.True(index >= 0, "The actual preference row must exist");
            for (int step = 0; step < index; step++) fixture.Send(OverlayInput.Down);
            if (preference.StageChoice) fixture.Send(OverlayInput.NextChoice);
            fixture.Send(OverlayInput.Select);

            TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(preference.Milliseconds - 1));
            fixture.Controller.Snapshot();
            TestAssert.Equal(true, fixture.Controller.ReadMenuObservation().ControlsDisabled);
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            fixture.Controller.Snapshot();
            TestAssert.Equal(false, fixture.Controller.ReadMenuObservation().ControlsDisabled);
            TestAssert.Equal(0, fixture.Session.Executed.Count);
        }

        var credits = new MenuObservationFixture();
        credits.Navigation.OpenPage(MenuPage.Customization);
        credits.Controller.Snapshot();
        credits.Send(OverlayInput.Select);
        var visit = credits.Controller.Snapshot();
        TestAssert.True(visit.MenuCredits is not null, "The first source customization row opens credits");
        TestAssert.Equal(true, credits.Controller.ReadMenuObservation().ControlsDisabled);
        credits.Send(OverlayInput.FinishMenuCredits, navigationRevision: visit.NavigationRevision);
        TestAssert.Equal(true, credits.Controller.ReadMenuObservation().ControlsDisabled);
        credits.Clock.Advance(TimeSpan.FromMilliseconds(150));
        credits.Controller.Snapshot();
        TestAssert.Equal(false, credits.Controller.ReadMenuObservation().ControlsDisabled);
        TestAssert.Equal(0, credits.Session.Executed.Count);
    }

    public static void BusyQuickFrameRetainsItsCatalogButDisablesPersonalAction() {
        var fixture = new MenuObservationFixture();
        fixture.Session.MenuState = fixture.Session.MenuState with { Busy = true };
        var frame = fixture.Controller.Snapshot();

        TestAssert.Equal(14, frame.Rows.Count);
        TestAssert.Equal("God Mode", frame.Rows[0].Label);
        TestAssert.Equal("UNKNOWN", frame.Rows[0].Value);
        TestAssert.True(!frame.Rows[0].Enabled, "Pending session work keeps its existing personal guard");
        fixture.Send(OverlayInput.Select);
        fixture.Controller.StartQueuedAction();
        TestAssert.Equal(0, fixture.Session.Executed.Count);
    }

    public static void MissingObservationPreservesTheMainConnectAndLocalSettingsPaths() {
        var fixture = new MenuObservationFixture(MenuSurface.Main);
        fixture.Session.Observation = LocalPlayerObservation.Unknown("The current actor could not be confirmed");
        TestAssert.Equal("The current actor could not be confirmed", fixture.Controller.ReadMenuObservation().Status);
        fixture.Session.MenuState = fixture.Session.MenuState with { Connected = false, CanConnect = true };
        var frame = fixture.Controller.Snapshot();

        TestAssert.Equal("Connect after spawning", frame.Rows[0].Label);
        TestAssert.True(frame.Rows[0].Enabled, "Observation unavailable must not prevent initial Connect");
        TestAssert.True(frame.Rows.Single(row => row.Label == "Menu Customization").Enabled,
            "Local menu settings remain accessible without inventing permission");
    }
}
