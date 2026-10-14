using Apparition.Backend.Preferences;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Reloads exercise real frames and accepted commands rather than setters on hidden controller fields
internal static class CommittedPreferenceControllerTests {
    public static void AllCommittedSettingsLoadBeforeFirstFrameWithoutGrantingGameplay() {
        var appearance = new MenuAppearance(BooleanDisplay: "Text Color", BoolBoxLocation: "Left",
            InstructionsVisible: false, OptionCounter: true, ColoredCursor: false, SpotlightCursor: false,
            LargeCursor: true, QuickExit: true, MenuWidth: 340, AlternateInstructions: true,
            InstructionsBackground: false, AdaptiveInstructions: false, InstructionPosition: new(-40, 600),
            ScrollAnimationMilliseconds: 250, MenuPosition: new(-100, 110), Design: "Apparition", StealthUI: false);
        var saved = new CommittedMenuPreferences(appearance, "#FF0000", false,
            [OpeningAction.Aim, OpeningAction.Melee], [OpeningAction.Aim, OpeningAction.SecondaryOffhand], true);
        var fixture = new PreferenceControllerFixture(saved);
        var frame = fixture.Controller.Snapshot();
        TestAssert.Equal(appearance, frame.Appearance);
        TestAssert.Equal("#FF0000", frame.Accent);
        TestAssert.True(!frame.IsOpen && frame.MainOpeningControls is null,
            "Persisted preferences granted a menu visit or unverified opening capability");
        TestAssert.Equal(0, fixture.Session.Boundary.Executed.Count);
    }

    public static void AcceptedChoicesReloadWhileStagedCandidatesStayPrivate() {
        var fixture = new PreferenceControllerFixture();fixture.OpenPreferences();
        fixture.Send(OverlayInput.Down);fixture.Send(OverlayInput.NextChoice);fixture.Send(OverlayInput.Select);
        TestAssert.Equal("Text", fixture.Controller.Snapshot().Appearance?.BooleanDisplay);
        TestAssert.Equal("Text", fixture.Recreate().Snapshot().Appearance?.BooleanDisplay);
        fixture.Send(OverlayInput.NextChoice);
        TestAssert.Equal("Text Color", fixture.Controller.Snapshot().Rows[1].Value);
        TestAssert.Equal("Text", fixture.Recreate().Snapshot().Appearance?.BooleanDisplay);
    }

    public static void AccountChangesAndRevocationNeverBorrowOrOverwriteAnotherOwnersSettings() {
        var saved = new CommittedMenuPreferences(MenuAppearance.Default with { QuickExit = true }, "#FF0000", false);
        var fixture = new PreferenceControllerFixture(saved);
        var accountA = fixture.Session.Scope!;
        TestAssert.True(fixture.Controller.Snapshot().Appearance?.QuickExit == true, "The first account did not load");
        fixture.Session.Scope = new(456, new object());
        TestAssert.Equal(MenuAppearance.Default, fixture.Controller.Snapshot().Appearance);
        TestAssert.Equal(MenuThemes.DefaultAccent, fixture.Controller.Snapshot().Accent);
        fixture.OpenPreferences();fixture.Highlight("Quick Exit");fixture.Send(OverlayInput.Select);
        TestAssert.True(fixture.Controller.Snapshot().Appearance?.QuickExit == true, "The second account did not commit");
        fixture.Session.Scope = null;fixture.Controller.Snapshot();
        fixture.Session.Scope = accountA;
        TestAssert.Equal("#FF0000", fixture.Controller.Snapshot().Accent);
        TestAssert.True(fixture.Store.TryRead(accountA, out var retained) && retained!.Accent == "#FF0000",
            "Another or revoked owner replaced the first account's stored settings");
    }

    public static void LiveWidthSamplesSaveOnlyAfterTheCompletedSourceSoftUnlock() {
        var fixture = new PreferenceControllerFixture();fixture.OpenCustomization();
        fixture.Highlight("Width Editor");fixture.Send(OverlayInput.Select);fixture.Send(OverlayInput.IncreaseWidth);
        TestAssert.Equal(261, fixture.Controller.Snapshot().Appearance?.MenuWidth);
        TestAssert.Equal(260, fixture.Recreate().Snapshot().Appearance?.MenuWidth);
        fixture.Send(OverlayInput.FinishWidthEditor);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(149));fixture.Controller.Snapshot();
        TestAssert.Equal(260, fixture.Recreate().Snapshot().Appearance?.MenuWidth);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));fixture.Controller.Snapshot();
        TestAssert.Equal(261, fixture.Recreate().Snapshot().Appearance?.MenuWidth);
    }

    public static void CounterRebuildSavesAfterHudCreationAndOwnerLossCancelsThePendingSave() {
        foreach (bool revoke in new[] { false, true }) {
            var fixture = new PreferenceControllerFixture();fixture.OpenPreferences();
            fixture.Highlight("Option Counter");fixture.Send(OverlayInput.Select);
            TestAssert.True(fixture.Controller.Snapshot().MenuPreferenceRebuild is not null, "The actual source rebuild did not start");
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(49));fixture.Controller.Snapshot();
            TestAssert.True(fixture.Recreate().Snapshot().Appearance?.OptionCounter == false, "Save preceded source HUD creation");
            var scope = fixture.Session.Scope!;
            if (revoke) fixture.Session.Scope = null;
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));fixture.Controller.Snapshot();
            fixture.Session.Scope = scope;
            TestAssert.Equal(!revoke, fixture.Recreate().Snapshot().Appearance?.OptionCounter);
        }
    }

    public static void UnverifiedSavedOpeningActionsStayUnavailableAndSurviveUnrelatedPreferenceCommits() {
        TestAssert.True(VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee], out var capabilities, out _),
            "The actual verified capability set was not constructed");
        var saved = new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false,
            [OpeningAction.Use, OpeningAction.Melee], [OpeningAction.Aim, OpeningAction.SecondaryOffhand], true);
        var fixture = new PreferenceControllerFixture(saved, capabilities);
        var frame = fixture.Controller.Snapshot();
        TestAssert.True(frame.MainOpeningControls?.Actions.SequenceEqual(new[] { OpeningAction.Aim, OpeningAction.Melee }) == true
            && frame.OpeningPolicies is null, "Stored action IDs granted unverified Use or Quick opening");
        TestAssert.True(frame.Notice.Contains("saved opening", StringComparison.OrdinalIgnoreCase),
            "Unavailable saved opening controls did not explain the verified fallback");
        fixture.OpenPreferences();fixture.Highlight("Quick Exit");fixture.Send(OverlayInput.Select);
        TestAssert.True(fixture.Store.TryRead(fixture.Session.Scope!, out var retained)
            && retained!.MainOpeningActions!.SequenceEqual(saved.MainOpeningActions!)
            && retained.QuickOpeningActions!.SequenceEqual(saved.QuickOpeningActions!) && retained.QuickDisabled,
            "An unrelated appearance commit erased the saved action groups that lack current capabilities");
    }

    public static void StorageFailureShowsFeedbackAndLeavesGameplayAndPriorStoreValuesUntouched() {
        var store = new FailingPreferenceStore();
        var fixture = new PreferenceControllerFixture(store: store);
        fixture.OpenPreferences();fixture.Highlight("Quick Exit");fixture.Send(OverlayInput.Select);
        var frame = fixture.Controller.Snapshot();
        TestAssert.True(frame.Appearance?.QuickExit == true && frame.Notice.Contains("could not be saved", StringComparison.OrdinalIgnoreCase),
            "A local store failure escaped the controller or falsely reported a durable commit");
        TestAssert.Equal(0, fixture.Session.Boundary.Executed.Count);
    }

    public static void EveryLoadedDesignStartsItsEditorAtTheCommittedChoice() {
        foreach (string name in new[] { "Apparition", "Classic", "Native", "AIO", "Basic" }) {
            var appearance = MenuAppearance.Default with { Design = name, ColoredCursor = MenuDesignDraft.AllowsColoredCursor(name) };
            var fixture = new PreferenceControllerFixture(new(appearance, MenuThemes.DefaultAccent, false));
            fixture.OpenPreferences();
            TestAssert.Equal(name, fixture.Controller.Snapshot().Rows[0].Value);
            TestAssert.Equal(name, fixture.Controller.Snapshot().Appearance?.Design);
        }
    }

    public static void CheapPhaseReadsAndFailedFramesCannotCommitDeferredSavedValues() {
        var fixture = new PreferenceControllerFixture();fixture.OpenPreferences();
        fixture.Highlight("Option Counter");fixture.Send(OverlayInput.Select);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixture.Controller.ReadMenuPublicationPhase();
        TestAssert.True(fixture.Recreate().Snapshot().Appearance?.OptionCounter == false,
            "A cheap phase read committed settings without the actual frame");
        fixture.Session.Boundary.RejectMenuStateReads = true;
        bool failed = false;
        try { fixture.Controller.Snapshot(); } catch (InvalidOperationException) { failed = true; }
        TestAssert.True(failed, "The controlled failed frame must reach its actual read failure");
        fixture.Session.Boundary.RejectMenuStateReads = false;
        TestAssert.True(fixture.Recreate().Snapshot().Appearance?.OptionCounter == false,
            "A failed frame replaced the original repository value");
        fixture.Controller.Snapshot();
        TestAssert.True(fixture.Recreate().Snapshot().Appearance?.OptionCounter == true,
            "The next successful frame failed to commit the retained source save");
    }

    private sealed class FailingPreferenceStore : ICommittedMenuPreferencesStore {
        public bool TryRead(LocalMenuPreferenceScope scope, out CommittedMenuPreferences? preferences) { preferences = null;return false; }
        public void Write(LocalMenuPreferenceScope scope, CommittedMenuPreferences preferences) => throw new IOException("Owned store refused the write");
    }

    public static void SourceEarlyReturnsDoNotWriteButSameFixedColorAndScrollDurationDo() {
        foreach (string route in new[] { "display", "location", "design", "rainbow", "adaptive", "fixed", "scroll" }) {
            var store = new CountingPreferenceStore();
            var appearance = MenuAppearance.Default with { AlternateInstructions = route == "adaptive" };
            store.Write(new(123, new object()), new(appearance, route == "fixed" ? "#FF0000" : MenuThemes.DefaultAccent,
                route == "rainbow"));
            var fixture = new PreferenceControllerFixture(store: store);store.Writes = 0;
            if (route is "display" or "location" or "design" or "scroll") {
                fixture.OpenPreferences();
                int index = route switch { "display" => 1, "location" => 2, "scroll" => 3, _ => 0 };
                fixture.Highlight(fixture.Controller.Snapshot().Rows[index].Label);
            } else {
                fixture.OpenCustomization();
                fixture.Highlight(route == "adaptive" ? "Menu Instructions" : "Main Design Color");fixture.Send(OverlayInput.Select);
                fixture.Highlight(route == "adaptive" ? "Adaptive Position" : route == "fixed" ? "Red" : "Smooth Rainbow");
            }
            fixture.Send(OverlayInput.Select);
            TestAssert.Equal(route is "fixed" or "scroll" ? 1 : 0, store.Writes);
        }
    }

    public static void ExhaustedOpeningReloadCannotRecycleItsEpochOnAnotherAccount() {
        TestAssert.True(VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee], out var capabilities, out _), "Capability setup failed");
        var fixture = new PreferenceControllerFixture(capabilities: capabilities);
        TestAssert.True(MainOpeningChord.TryCreate([OpeningAction.Aim, OpeningAction.Melee], ulong.MaxValue,
            capabilities, out var exhausted, out _), "The actual exhausted policy was not constructed");
        typeof(Apparition.Backend.Controller.BackendController).GetField("mainOpeningChord",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(fixture.Controller, exhausted);
        foreach (ulong account in new ulong[] { 456, 789 }) {
            fixture.Session.Scope = new(account, new object());
            var frame = fixture.Controller.Snapshot();
            TestAssert.True(frame.MainOpeningControls is null && frame.OpeningPolicies is null
                && frame.Notice.Contains("revision is exhausted", StringComparison.Ordinal),
                "An account reload recycled an exhausted opening epoch");
        }
    }

    public static void NonDisconnectWidthCloseSavesItsLiveValueWithoutWaitingForAnotherVisit() {
        var fixture = new PreferenceControllerFixture();fixture.OpenCustomization();
        fixture.Highlight("Width Editor");fixture.Send(OverlayInput.Select);fixture.Send(OverlayInput.IncreaseWidth);
        fixture.Send(OverlayInput.Toggle);
        TestAssert.True(!fixture.Controller.Snapshot().IsOpen, "The owned non-disconnect close did not finish");
        TestAssert.Equal(261, fixture.Recreate().Snapshot().Appearance?.MenuWidth);
    }

    public static void TerminalCloseCannotSaveTheLiveWidthEditorValue() => CheckTerminalEditorClose("Width Editor", OverlayInput.IncreaseWidth);

    public static void TerminalCloseCannotSaveTheLiveMenuPositionValue() => CheckTerminalEditorClose("Reposition Menu", OverlayInput.MoveMenuRight);

    private static void CheckTerminalEditorClose(string label, OverlayInput movement) {
        var fixture = new PreferenceControllerFixture();fixture.OpenCustomization();
        fixture.Highlight(label);fixture.Send(OverlayInput.Select);fixture.Send(movement);
        // The test reaches a real unsaved editor sample before terminal cleanup
        TestAssert.True(fixture.Controller.Snapshot().Appearance != MenuAppearance.Default,
            "The live editor did not change its displayed value");
        TestAssert.Equal(MenuAppearance.Default, fixture.Recreate().Snapshot().Appearance);
        TestAssert.True(!fixture.Controller.ClosedState().IsOpen, "Terminal cleanup must close the visit");
        TestAssert.Equal(MenuAppearance.Default, fixture.Recreate().Snapshot().Appearance);
        // A repeated terminal frame cannot revive saving through an editor leave hook
        fixture.Controller.ClosedState();
        TestAssert.Equal(MenuAppearance.Default, fixture.Recreate().Snapshot().Appearance);
    }

    public static void InstructionPositionSavesOnlyAfterExitWaitAndTheSourceSoftUnlock() {
        var fixture = new PreferenceControllerFixture();fixture.OpenCustomization();
        fixture.Highlight("Menu Instructions");fixture.Send(OverlayInput.Select);
        fixture.Highlight("Reposition");fixture.Send(OverlayInput.Select);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));fixture.Controller.Snapshot();
        fixture.Send(OverlayInput.MoveInstructionsRight);
        TestAssert.Equal(263, fixture.Controller.Snapshot().Appearance?.InstructionPosition?.X);
        TestAssert.True(fixture.Recreate().Snapshot().Appearance?.InstructionPosition is null, "A live instruction sample was saved");
        fixture.Send(OverlayInput.FinishInstructionPositionEditor);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));fixture.Controller.Snapshot();
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(149));fixture.Controller.Snapshot();
        TestAssert.True(fixture.Recreate().Snapshot().Appearance?.InstructionPosition is null, "Instruction save preceded its full exit wait");
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));fixture.Controller.Snapshot();
        TestAssert.Equal(263, fixture.Recreate().Snapshot().Appearance?.InstructionPosition?.X);
    }

    public static void LoadedMainQuickAndDisabledChoicesPublishOnlyFreshVerifiedPolicies() {
        TestAssert.True(VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use,
            OpeningAction.SecondaryOffhand, OpeningAction.Frag], out var capabilities, out _), "Capability setup failed");
        var saved = new CommittedMenuPreferences(MenuAppearance.Default, "#FF0000", true,
            [OpeningAction.Use, OpeningAction.Melee], [OpeningAction.Aim, OpeningAction.Frag], true);
        var fixture = new PreferenceControllerFixture(saved, capabilities);
        var frame = fixture.Controller.Snapshot();
        TestAssert.True(frame.MainOpeningControls!.Actions.SequenceEqual(saved.MainOpeningActions!)
            && frame.OpeningPolicies!.Quick.Actions.SequenceEqual(saved.QuickOpeningActions!) && frame.OpeningPolicies.QuickDisabled,
            "The verified saved Main, Quick, or Disabled preference failed to reload");
        TestAssert.Equal(MenuThemes.DefaultAccent, frame.Accent);
        TestAssert.True(frame.SmoothRainbowTheme && !frame.IsOpen && frame.OpeningPolicies!.Revision > 0,
            "Loaded settings restored an old visit or lost the Rainbow sentinel");
    }

    private sealed class CountingPreferenceStore : ICommittedMenuPreferencesStore {
        private readonly MemoryCommittedMenuPreferencesStore inner = new();
        public int Writes;
        public bool TryRead(LocalMenuPreferenceScope scope, out CommittedMenuPreferences? preferences) => inner.TryRead(scope, out preferences);
        public void Write(LocalMenuPreferenceScope scope, CommittedMenuPreferences preferences) { inner.Write(scope, preferences);Writes++; }
    }
}
