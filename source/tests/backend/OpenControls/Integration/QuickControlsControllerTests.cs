using System.Text.Json;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Actual controller commands prove local configuration without authorizing Quick opening
internal static class QuickControlsControllerTests {
    public static void DefaultsPublishExactMirrorsAndMissingSecondaryKeepsMainOnly() {
        var complete = OpeningControlsFixtureExporter.Create();
        var initial = complete.Current();
        var pair = Pair(initial);
        TestAssert.Equal(1UL, pair.GetProperty("revision").GetUInt64());
        TestAssert.True(!pair.GetProperty("quickDisabled").GetBoolean(), "The default local preference is not disabled");
        AssertTuple(initial, "main", 1, 5, 4);
        AssertTuple(initial, "quick", 1, 5, 10);
        AssertMirror(initial);
        TestAssert.Throws<InvalidOperationException>(() => (initial with { MainOpeningControls = null }).Serialize());
        var other = MainOpeningControlsState.FromChord(MainOpeningChordTests.Create(
            Apparition.Backend.OpenControls.OpeningAction.Use, Apparition.Backend.OpenControls.OpeningAction.Melee));
        TestAssert.Throws<InvalidOperationException>(() => (initial with { MainOpeningControls = other }).Serialize());
        complete.NoGameWork();

        var partial = OpeningControlsFixtureExporter.Create(partial: true);
        partial.Open();
        TestAssert.True(!HasPair(partial.Current()), "Missing SecondaryOffhand keeps the verified Main-only owner");
        partial.Select("Use");
        TestAssert.True(OpeningControlsRig.Actions(partial.Current()).SequenceEqual(new[] { 8, 4 }),
            "A missing Quick capability cannot disable Main configuration");
        partial.NoGameWork();

        var legacy = new OpeningControlsRig(configured: false);
        TestAssert.True(!HasPair(legacy.Current()) && OpeningControlsRig.Revision(legacy.Current()) is null,
            "Unconfigured callers retain the legacy omitted descriptors");
    }

    public static void QuickSelectorAppliesOnceWithoutEnablingQuickOpeningAndStaleCapturesReject() {
        var rig = OpeningControlsFixtureExporter.Create();
        rig.Open();
        rig.Highlight("Menu");
        rig.Send(OverlayInput.NextChoice);
        var staged = rig.Current();
        rig.Send(OverlayInput.Select);
        var applied = rig.Current();

        TestAssert.Equal(staged.NavigationRevision + 1, applied.NavigationRevision);
        TestAssert.Equal(staged.MenuRevision + 1, applied.MenuRevision);
        TestAssert.Equal(OpeningControlsRig.Revision(staged), OpeningControlsRig.Revision(applied));
        TestAssert.True(applied.Notice.Contains("applied: Quick Menu", StringComparison.Ordinal),
            "The actual selector changes independently from either tuple");
        TestAssert.True(applied.Notice.Contains("opening", StringComparison.OrdinalIgnoreCase)
            && applied.Notice.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
            "Configuring Quick cannot advertise a working Quick opening path");
        AssertTuple(applied, "quick", 1, 5, 10);
        rig.Wait(250);

        // A newer command sequence does not make an obsolete selector capture current
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.NextChoice, true,
            staged.NavigationRevision, OpeningControlsRig.Revision(staged)), () => true);
        TestAssert.Equal("Quick Menu", rig.Current().Rows[0].Value);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Rejected captures own no deferred callback");
        rig.NoGameWork();
    }

    public static void QuickMutationsPublishImmediatelyAndRefreshOnlyItsSelectedSlotAt180() {
        var rig = QuickEditor();
        rig.Highlight("Use");
        var before = rig.Current();
        rig.Send(OverlayInput.Select);
        var pending = rig.Current();
        AssertTuple(pending, "main", 1, 5, 4);
        AssertTuple(pending, "quick", 2, 8, 10);
        AssertMirror(pending);
        TestAssert.Equal(OpeningControlsRig.Revision(before) + 1, OpeningControlsRig.Revision(pending));
        TestAssert.Equal("OFF", Row(pending, "Use").Value);
        TestAssert.Equal("ON", Row(pending, "Aim").Value);
        TestAssert.Equal("OFF", Row(pending, "Secondary Offhand").Value);
        TestAssert.Equal(before.MenuRevision, pending.MenuRevision);

        rig.Wait(179);
        TestAssert.Equal("OFF", Row(rig.Current(), "Use").Value);
        rig.Wait(1);
        TestAssert.Equal("ON", Row(rig.Current(), "Use").Value);
        TestAssert.Equal("OFF", Row(rig.Current(), "Aim").Value);
        TestAssert.Equal(before.MenuRevision + 1, rig.Current().MenuRevision);
        rig.Wait(249);
        TestAssert.Equal("cooldown", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "The source action wait finishes at430");
        rig.NoGameWork();
    }

    public static void QuickDuplicatesNoneAndOffhandConflictKeepSourceRefreshWithoutChangingOtherFamily() {
        var rig = QuickEditor();
        var original = rig.Current();
        rig.Select("Secondary Offhand");
        TestAssert.Equal(OpeningControlsRig.Revision(original), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(430);
        rig.SetSlot(3);
        rig.Select("Frag");
        AssertTuple(rig.Current(), "quick", 1, 5, 10);
        TestAssert.True(rig.Current().Notice.Contains("offhand", StringComparison.OrdinalIgnoreCase),
            "Quick rejects the same within-tuple offhand conflict as Main");
        rig.Wait(430);
        var empty = rig.Current();
        rig.Select("None");
        TestAssert.Equal(OpeningControlsRig.Revision(empty), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(430);
        rig.SetSlot(2);
        rig.Select("None");
        AssertTuple(rig.Current(), "quick", 2, 5);
        AssertTuple(rig.Current(), "main", 1, 5, 4);
        rig.Wait(430);
        TestAssert.Equal("ON", Row(rig.Current(), "None").Value);
        // Source families may share a complete tuple without conflicting with each other
        rig.Select("Melee");
        AssertTuple(rig.Current(), "quick", 3, 5, 4);
        AssertTuple(rig.Current(), "main", 1, 5, 4);
        rig.NoGameWork();
    }

    public static void AppliedTargetAndSlotChangesInvalidateOnlyActionContextWhileNoopsKeepIt() {
        foreach (bool partial in new[] { false, true }) {
            var rig = OpeningControlsFixtureExporter.Create(partial);
            rig.Open();
            rig.Highlight("Bind Slot");
            rig.Send(OverlayInput.NextChoice);
            var before = rig.Current();
            rig.Send(OverlayInput.Select);
            var changed = rig.Current();
            TestAssert.Equal(before.NavigationRevision + 1, changed.NavigationRevision);
            TestAssert.Equal(before.MenuRevision + 1, changed.MenuRevision);
            TestAssert.Equal(OpeningControlsRig.Revision(before), OpeningControlsRig.Revision(changed));
            rig.Wait(250);
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.NextChoice, true,
                before.NavigationRevision, OpeningControlsRig.Revision(before)), () => true);
            TestAssert.Equal("2", rig.Current().Rows[1].Value);

            var sameSlot = rig.Current();
            rig.Send(OverlayInput.Select);
            TestAssert.Equal(sameSlot.NavigationRevision, rig.Current().NavigationRevision);
            TestAssert.Equal(sameSlot.MenuRevision + 1, rig.Current().MenuRevision);
            rig.Wait(250);
            rig.Highlight("Menu");
            var sameTarget = rig.Current();
            rig.Send(OverlayInput.Select);
            TestAssert.Equal(sameTarget.NavigationRevision, rig.Current().NavigationRevision);
            TestAssert.Equal(sameTarget.MenuRevision, rig.Current().MenuRevision);
            rig.NoGameWork();
        }
    }

    public static void QuickPreferenceDisablesConfigurationWithoutDroppingTupleAndShowsCapturedRows() {
        var rig = OpeningControlsFixtureExporter.Create();
        rig.Open();
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Disable Quick Menu");
        var before = rig.Current();
        TestAssert.True(Row(before, "Disable Quick Menu").Enabled, "Verified local configuration can change its disable preference");
        // The new policy action cannot synthesize a missing worker capture from current state
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true,
            before.NavigationRevision), () => true);
        TestAssert.Equal(OpeningControlsRig.Revision(before), OpeningControlsRig.Revision(rig.Current()));
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "An uncaptured preference attempt owns no timer");
        rig.Send(OverlayInput.Select);
        var pending = rig.Current();
        TestAssert.True(Pair(pending).GetProperty("quickDisabled").GetBoolean(), "Actual configuration changes at admission");
        TestAssert.Equal(OpeningControlsRig.Revision(before) + 1, OpeningControlsRig.Revision(pending));
        TestAssert.Equal("OFF", Row(pending, "Disable Quick Menu").Value);
        AssertTuple(pending, "quick", 1, 5, 10);
        AssertTuple(pending, "main", 1, 5, 4);
        rig.Wait(179);
        TestAssert.Equal("OFF", Row(rig.Current(), "Disable Quick Menu").Value);
        rig.Wait(1);
        TestAssert.Equal("ON", Row(rig.Current(), "Disable Quick Menu").Value);
        TestAssert.Equal(before.MenuRevision + 1, rig.Current().MenuRevision);
        rig.Wait(250);
        rig.Send(OverlayInput.Select);
        TestAssert.True(!Pair(rig.Current()).GetProperty("quickDisabled").GetBoolean(), "Reenable changes only the local preference");
        AssertTuple(rig.Current(), "quick", 1, 5, 10);
        rig.NoGameWork();
    }

    public static void ClosedHiddenAndFailedPublicationRetainActualDualPolicies() {
        var rig = QuickEditor();
        rig.Select("Use");
        var pending = rig.Current();
        StateDocument? published = null;
        bool fail = true;
        var publication = new MenuPhasePublication(state => {
            if (fail) throw new IOException("Owned publication failure");
            published = state;
        }, rig.Clock);
        TestAssert.Throws<IOException>(() => publication.Publish(pending, false));
        TestAssert.True(publication.ShouldPublish(rig.Controller.ReadMenuPublicationPhase(), false),
            "A failed write remains due without reverting the configured pair");
        fail = false;
        publication.Publish(rig.Current(), false);
        TestAssert.True(!published!.IsOpen && published.MenuPreferenceRefresh is null, "Focus projection clears presentation only");
        AssertTuple(published, "quick", 2, 8, 10);
        AssertMirror(published);
        rig.Wait(430);
        rig.Send(OverlayInput.Toggle);
        AssertTuple(rig.Current(), "quick", 2, 8, 10);
        rig.Controller.RequestShutdown();
        var closed = rig.Controller.ClosedState();
        AssertTuple(closed, "quick", 2, 8, 10);
        AssertMirror(closed);
        rig.NoGameWork();
    }

    public static void GlobalPolicyCaptureRejectsOldFamilyCompletionOnEveryPage() {
        var rig = QuickEditor();
        rig.Select("Use");
        rig.Wait(430);
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Bool Display");
        var before = rig.Current();
        // Main is still tuple revision1, but the captured global admission epoch is now2
        AssertTuple(before, "main", 1, 5, 4);
        TestAssert.Equal(2UL, OpeningControlsRig.Revision(before));
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.NextChoice, true,
            before.NavigationRevision, 1), () => true);
        TestAssert.Equal(before.Rows[before.SelectedIndex].Value, rig.Current().Rows[before.SelectedIndex].Value);
        rig.Send(OverlayInput.NextChoice);
        TestAssert.Equal("Text", rig.Current().Rows[before.SelectedIndex].Value);
        rig.NoGameWork();
    }

    public static void QuickRefreshClockRollbackAndLatePollNeverRepeatCommitOrRedraw() {
        var rig = QuickEditor();
        rig.Select("Use");
        var admitted = rig.Current();
        rig.Wait(100);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("pending", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(179);
        TestAssert.Equal(admitted.MenuRevision, rig.Current().MenuRevision);
        rig.Wait(1);
        TestAssert.Equal(admitted.MenuRevision + 1, rig.Current().MenuRevision);
        rig.Wait(250);
        AssertTuple(rig.Current(), "quick", 2, 8, 10);
        TestAssert.Equal(2UL, OpeningControlsRig.Revision(rig.Current()));
        rig.Select("Aim");
        var late = rig.Current();
        rig.Wait(1000);
        TestAssert.Equal(late.MenuRevision + 1, rig.Current().MenuRevision);
        AssertTuple(rig.Current(), "quick", 3, 5, 10);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Late polling completes one redraw before retiring the wait");
        rig.NoGameWork();
    }

    public static void NoRedrawMenuSelectorWaitKeepsTheExistingOpeningWithoutAdvertisingActiveControls() {
        foreach (int scenario in new[] { 0, 1, 2 }) {
            var rig = OpeningControlsFixtureExporter.Create(partial: scenario != 0);
            rig.Open();
            rig.Highlight("Menu");
            rig.Send(OverlayInput.Toggle);
            rig.Send(OverlayInput.Toggle);
            rig.Wait(50);
            rig.Wait(550);
            if (scenario == 2) rig.Send(OverlayInput.NextChoice);
            var before = rig.Current();
            TestAssert.True(before.MenuOpening is not null, "The retained editor fade must still be active after inputs unlock");

            // Same target and unverified target rejection both leave the existing HUD untouched
            rig.Send(OverlayInput.Select);
            var waiting = rig.Current();
            TestAssert.Equal("selector", waiting.MenuPreferenceRefresh?.Phase);
            TestAssert.Equal(before.MenuOpening, waiting.MenuOpening);
            TestAssert.Equal(before.NavigationRevision, waiting.NavigationRevision);
            TestAssert.Equal(before.MenuRevision, waiting.MenuRevision);
            TestAssert.True(!waiting.Notice.Contains("Use applies", StringComparison.Ordinal),
                "A no-redraw wait cannot advertise an active selected action");
            rig.Wait(249);
            TestAssert.Equal("selector", rig.Current().MenuPreferenceRefresh?.Phase);
            rig.Wait(1);
            TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "The source selector wait ends at250");
            TestAssert.True(rig.Current().MenuOpening is not null, "The separate opening fade is not owned by selector cooldown");
            TestAssert.Equal(before.MenuRevision, rig.Current().MenuRevision);
            rig.NoGameWork();
        }
    }

    public static void SelectorMetadataRejectsWrongTopologyMissingPolicyAndRedrawCooldownOpening() {
        var rig = OpeningControlsFixtureExporter.Create();
        var actual = rig.Current();
        TestAssert.True(VerifiedOpeningActions.TryCreate(Enum.GetValues<OpeningAction>(), out var capabilities, out _),
            "The schema fixture uses a bounded verified logical inventory");
        var chord = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee);
        var view = new OpeningControlsView(chord, chord, OpeningControlDraft.Default,
            OpeningControlDraft.Default, capabilities!, true);
        var rows = OpeningControlsMenuRows.Build(view).ToArray();
        var model = new MenuSnapshot("Open Controls", "PREVIEW", rows, "Opening controls paused");
        var opening = new MenuOpening(1, 0);
        var selector = new MenuPreferenceRefresh("selector");
        StateDocument Create(MenuSnapshot snapshot, int selected = 0, MainOpeningControlsState? main = null,
            MenuPreferenceRefresh? refresh = null) => StateDocument.Create(42, 1, true, snapshot, selected,
                isRootMenu: false, menuPreferenceRefresh: refresh ?? selector, menuOpening: opening,
                mainOpeningControls: main ?? actual.MainOpeningControls);

        // A genuine no-redraw selector retains the existing opening descriptor
        var valid = Create(model);
        TestAssert.Equal(opening, valid.MenuOpening);
        TestAssert.Equal("selector", valid.MenuPreferenceRefresh?.Phase);
        _ = valid.Serialize();
        TestAssert.Throws<InvalidOperationException>(() => Create(model, selected: 1));
        TestAssert.Throws<InvalidOperationException>(() => Create(model, refresh: new("cooldown")));
        TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, model, 0,
            isRootMenu: false, menuPreferenceRefresh: selector, menuOpening: opening));

        // Even without a fade, the special wait must belong to the full editor inventory
        TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true,
            model with { Rows = rows.Take(3).ToArray() }, 0, isRootMenu: false,
            menuPreferenceRefresh: selector, mainOpeningControls: actual.MainOpeningControls));
        foreach (bool reorder in new[] { false, true }) {
            var malformed = rows.ToArray();
            malformed[3] = reorder ? rows[4] : rows[3] with { OpeningAction = null };
            TestAssert.Throws<InvalidOperationException>(() => Create(model with { Rows = malformed }));
        }

        // A staged slot caption does not replace the applied slot that determines None
        TestAssert.True(OpeningControlDraft.Default.TryStageSlot(2, out var appliedSlot, out _),
            "The second applied slot is a valid editor selection");
        var noneRows = OpeningControlsMenuRows.Build(view with { AppliedSelector = appliedSlot }).ToArray();
        TestAssert.Equal("1", noneRows[1].Value);
        TestAssert.Equal("None", noneRows[3].Label);
        var validNone = Create(model with { Rows = noneRows });
        _ = validNone.Serialize();
        var badNone = noneRows.ToArray();
        badNone[3] = badNone[3] with { Action = new(MenuActionKind.None) };
        TestAssert.Throws<InvalidOperationException>(() => Create(model with { Rows = badNone }));
        var badWire = validNone.Rows.ToArray();
        badWire[3] = badWire[3] with { Label = "Another control" };
        TestAssert.Throws<InvalidOperationException>(() => (validNone with { Rows = badWire }).Serialize());

        // Captions alone cannot give an unrelated action the selector exception
        foreach (int changed in new[] { 0, 1, 2 }) {
            var malformed = rows.ToArray();
            malformed[changed] = changed switch {
                0 => malformed[0] with { Action = new(MenuActionKind.ChangePreference) },
                1 => malformed[1] with { Label = "Another control" },
                _ => new("UNAVAILABLE", "", false, new(MenuActionKind.None))
            };
            TestAssert.Throws<InvalidOperationException>(() => Create(model with { Rows = malformed }));
        }
        TestAssert.Throws<InvalidOperationException>(() => (valid with { MainOpeningControls = null }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { SelectedIndex = 1 }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { IsRootMenu = true }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (valid with { IsOpen = false }).Serialize());
        rig.NoGameWork();
    }

    public static void NoRedrawSelectorClockRecoveryNeverAddsAPresentationInvalidation() {
        var rig = OpeningControlsFixtureExporter.Create();
        rig.Open();
        rig.Highlight("Menu");
        rig.Send(OverlayInput.Select);
        var admitted = rig.Current();
        TestAssert.Equal("selector", admitted.MenuPreferenceRefresh?.Phase);

        rig.Wait(100);
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        TestAssert.Equal("selector", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(249);
        TestAssert.Equal("selector", rig.Current().MenuPreferenceRefresh?.Phase);
        rig.Wait(1);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Clock recovery restarts only the source selector wait");
        TestAssert.Equal(admitted.MenuRevision, rig.Current().MenuRevision);
        TestAssert.Equal(admitted.NavigationRevision, rig.Current().NavigationRevision);

        rig.Send(OverlayInput.Select);
        rig.Wait(1000);
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "One late poll retires the wait without a row rebuild");
        TestAssert.Equal(admitted.MenuRevision, rig.Current().MenuRevision);
        TestAssert.Equal(admitted.NavigationRevision, rig.Current().NavigationRevision);
        rig.NoGameWork();
    }

    private static OpeningControlsRig QuickEditor() {
        var rig = OpeningControlsFixtureExporter.Create();
        rig.Open();
        rig.Highlight("Menu");
        rig.Send(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(250);
        TestAssert.True(rig.Current().Notice.Contains("applied: Quick Menu", StringComparison.Ordinal),
            "The verified Quick target must actually apply");
        return rig;
    }

    private static StateRow Row(StateDocument frame, string label) => frame.Rows.Single(row => row.Label == label);
    private static bool HasPair(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        return json.RootElement.TryGetProperty("openingPolicies", out _);
    }
    private static JsonElement Pair(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        TestAssert.True(json.RootElement.TryGetProperty("openingPolicies", out var pair),
            "Verified startup must publish the actual dual-policy descriptor");
        return pair.Clone();
    }
    private static void AssertTuple(StateDocument frame, string family, ulong revision, params int[] actions) {
        var tuple = Pair(frame).GetProperty(family);
        TestAssert.Equal(revision, tuple.GetProperty("revision").GetUInt64());
        TestAssert.True(tuple.GetProperty("actions").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual(actions),
            "The exact ordered " + family + " tuple must remain coherent");
    }
    private static void AssertMirror(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        var main = Pair(frame).GetProperty("main");
        var mirror = json.RootElement.GetProperty("mainOpeningControls");
        TestAssert.Equal(main.GetProperty("revision").GetUInt64(), mirror.GetProperty("revision").GetUInt64());
        TestAssert.Equal(main.GetProperty("actions").GetRawText(), mirror.GetProperty("actions").GetRawText());
    }
}
