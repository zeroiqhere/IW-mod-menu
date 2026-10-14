using System.Security.Cryptography;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Five real source BOOL rows keep their original caller refresh boundaries
internal static class PreferenceUseRepeatFixtureTests {
    public static void ActualPreferenceUseRepeatGraphsCaptureFiveSourceRowsAndBothRefreshBoundaries() {
        var profiles = PreferenceUseRepeatFixtureProfiles.Build();
        TestAssert.Equal(69, profiles.Count);
        TestAssert.Equal(69, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        TestAssert.Equal(14, profiles.Count(profile => profile.OriginalCommand is not null));

        PreferenceUseRepeatFixtureProfile Profile(string name) => profiles.Single(profile => profile.Name == name + ".json");
        StateDocument Frame(string name) => Profile(name).State;
        foreach (var entry in PreferenceUseRepeatFixtureProfiles.Flags) {
            var before = Profile(entry.Prefix + "-before");
            TestAssert.Equal(MenuPage.Preferences, before.Page);
            TestAssert.Equal(entry.Index, before.State.SelectedIndex);
            TestAssert.Equal(entry.Index, before.SelectedSourceIndex);
            TestAssert.Equal(entry.Label, before.State.Rows[entry.Index].Label);
            AssertPhase(before.State, null);

            bool original = Value(before.State.Appearance!, entry.Preference);
            string originalValue = original ? "ON" : "OFF";
            string changedValue = original ? "OFF" : "ON";
            var firstSelect = Profile(entry.Prefix + "-select0");
            AssertSelect(before, firstSelect);
            AssertPhase(firstSelect.State, "pending");
            TestAssert.Equal(originalValue, firstSelect.State.Rows[entry.Index].Value);
            AssertPhase(Frame(entry.Prefix + "-wait179"), "pending");
            TestAssert.Equal(originalValue, Frame(entry.Prefix + "-wait179").Rows[entry.Index].Value);
            AssertPhase(Frame(entry.Prefix + "-redraw180"), "cooldown");
            TestAssert.Equal(changedValue, Frame(entry.Prefix + "-redraw180").Rows[entry.Index].Value);
            AssertPhase(Frame(entry.Prefix + "-wait429"), "cooldown");
            var ready = Profile(entry.Prefix + "-ready430");
            AssertPhase(ready.State, null);
            TestAssert.Equal(changedValue, ready.State.Rows[entry.Index].Value);

            var secondSelect = Profile(entry.Prefix + "-second-select430");
            AssertSelect(ready, secondSelect);
            AssertPhase(secondSelect.State, "pending");
            TestAssert.Equal(changedValue, secondSelect.State.Rows[entry.Index].Value);
            AssertPhase(Frame(entry.Prefix + "-second-redraw610"), "cooldown");
            TestAssert.Equal(originalValue, Frame(entry.Prefix + "-second-redraw610").Rows[entry.Index].Value);
            var secondReady = Frame(entry.Prefix + "-second-ready860");
            AssertPhase(secondReady, null);
            TestAssert.Equal(originalValue, secondReady.Rows[entry.Index].Value);

            foreach (string suffix in new[] { "before", "select0", "wait179", "redraw180", "wait429", "ready430", "second-select430", "second-redraw610", "second-ready860" }) {
                var profile = Profile(entry.Prefix + "-" + suffix);
                TestAssert.Equal(0, profile.SessionActionCount);
                TestAssert.Equal(profile.RelativeMilliseconds, RelativeTime(suffix));
            }
        }

        TestAssert.Equal("main", Frame("quick-exit-before").MenuSurface);
        TestAssert.True(!Frame("quick-exit-before").IsRootMenu, "The row source is the ordinary Preferences child page");

        var designBefore = Profile("design-same-before");
        TestAssert.Equal(MenuPage.Preferences, designBefore.Page);
        TestAssert.Equal(0, designBefore.SelectedSourceIndex);
        TestAssert.Equal("Design", designBefore.State.Rows[0].Label);
        TestAssert.Equal("Apparition", designBefore.State.Appearance!.Design);
        TestAssert.True(designBefore.State.MenuOptionCallback is null && designBefore.State.MenuUseContinuation is null,
            "The same-design graph starts without a callback or receipt");

        var designFirst = Profile("design-same-select0");
        AssertSelect(designBefore, designFirst);
        AssertPlainUse(designFirst.State);
        TestAssert.Equal("Apparition", designFirst.State.Appearance!.Design);
        TestAssert.Equal(designBefore.State.MenuRevision, designFirst.State.MenuRevision);
        TestAssert.Equal(designBefore.State.NavigationRevision, designFirst.State.NavigationRevision);
        var designReceipt = designFirst.State.MenuUseContinuation!;
        foreach (string name in new[] { "design-same-wait249", "design-same-ready250" }) {
            var frame = Frame(name);
            TestAssert.Equal(designReceipt, frame.MenuUseContinuation);
            TestAssert.True(frame.MenuDesignTransition is null, "The same-design callback cannot keep a second transition timer");
            TestAssert.Equal("Apparition", frame.Appearance!.Design);
            TestAssert.Equal(designBefore.State.MenuRevision, frame.MenuRevision);
            TestAssert.Equal(designBefore.State.NavigationRevision, frame.NavigationRevision);
        }
        TestAssert.Equal("cooldown", Frame("design-same-wait249").MenuOptionCallback!.Phase);
        TestAssert.True(Frame("design-same-ready250").MenuOptionCallback is null,
            "The same-design callback becomes ready after250ms");

        var designSecond = Profile("design-same-second-select250");
        AssertSelect(Profile("design-same-ready250"), designSecond);
        AssertPlainUse(designSecond.State);
        TestAssert.Equal(designReceipt.Revision + 1, designSecond.State.MenuUseContinuation!.Revision);
        TestAssert.True(designSecond.State.MenuUseContinuation.Sequence > designReceipt.Sequence,
            "A repeated same-design Select owns a fresh command sequence");
        TestAssert.Equal(designBefore.State.Appearance, designSecond.State.Appearance);
        TestAssert.Equal(designBefore.State.MenuRevision, designSecond.State.MenuRevision);
        TestAssert.Equal(designBefore.State.NavigationRevision, designSecond.State.NavigationRevision);
        TestAssert.Equal(designSecond.State.MenuUseContinuation, Frame("design-same-wait499").MenuUseContinuation);
        TestAssert.Equal("cooldown", Frame("design-same-wait499").MenuOptionCallback!.Phase);
        TestAssert.True(Frame("design-same-ready500").MenuOptionCallback is null,
            "The second same-design callback becomes ready at500ms");
        TestAssert.Equal(designSecond.State.MenuUseContinuation, Frame("design-same-ready500").MenuUseContinuation);
        foreach (string name in new[] { "design-nav-moved249", "design-owner-replaced0", "design-focus-hidden0",
            "design-focus-return250" }) {
            TestAssert.True(Frame(name).MenuUseContinuation is null,
                "A changed source scope cannot retain a same-design receipt: " + name);
        }
        TestAssert.Equal(1, Profile("design-nav-moved249").SelectedSourceIndex);
        TestAssert.True(Profile("design-nav-moved249").State.NavigationRevision > designBefore.State.NavigationRevision,
            "The navigation cancellation changes the captured source visit");
        TestAssert.True(Frame("design-owner-replaced0").LocalMenuEligibility!.Revision
            > designBefore.State.LocalMenuEligibility!.Revision,
            "The actor cancellation changes the captured owner admission");
        TestAssert.True(Frame("design-nav-moved249").MenuOptionCallback is null,
            "A changed cursor retires the original callback with its receipt");
        TestAssert.True(Frame("design-owner-replaced0").MenuOptionCallback is null,
            "A replacement actor retires the original callback with its receipt");
        TestAssert.True(Frame("design-same-select0").MenuUseContinuation is not null,
            "The focus cancellation starts with an actual same-design receipt");
        TestAssert.True(Frame("design-focus-hidden0").MenuUseContinuation is null
            && Frame("design-focus-hidden0").MenuOptionCallback is null,
            "A hidden focus projection carries no callback or continuation authority");
        TestAssert.True(!Frame("design-focus-hidden0").IsOpen && Frame("design-focus-return250").IsOpen,
            "The focus graph returns to an open panel after its original callback deadline");
        TestAssert.True(Frame("design-focus-return250").MenuUseContinuation is null
            && Frame("design-focus-return250").MenuOptionCallback is null,
            "A focus-returned frame cannot restore the canceled no-op receipt");

        var changedBefore = Profile("design-changed-classic-before");
        TestAssert.Equal(MenuPage.Preferences, changedBefore.Page);
        TestAssert.Equal(0, changedBefore.SelectedSourceIndex);
        TestAssert.Equal("Design", changedBefore.State.Rows[0].Label);
        TestAssert.Equal("Classic", changedBefore.State.Rows[0].Value);
        TestAssert.Equal("Apparition", changedBefore.State.Appearance!.Design);
        TestAssert.True(changedBefore.State.MenuOptionCallback is null && changedBefore.State.MenuUseContinuation is null
            && changedBefore.State.MenuDesignTransition is null,
            "A staged changed Design has no callback before its actual Select");

        var changedFirst = Profile("design-changed-classic-select0");
        AssertSelect(changedBefore, changedFirst);
        AssertPlainUseDuringDesign(changedFirst.State, "rebuild");
        TestAssert.Equal("Apparition", changedFirst.State.Appearance!.Design);
        TestAssert.Equal(changedBefore.State.MenuRevision, changedFirst.State.MenuRevision);
        TestAssert.Equal(changedBefore.State.NavigationRevision, changedFirst.State.NavigationRevision);
        var changedReceipt = changedFirst.State.MenuUseContinuation!;
        foreach (string name in new[] { "design-changed-classic-wait49", "design-changed-classic-redraw50", "design-changed-classic-wait249" }) {
            var profile = Profile(name);
            TestAssert.Equal(changedReceipt, profile.State.MenuUseContinuation);
            TestAssert.Equal("plain", profile.State.MenuOptionCallback!.Kind);
            TestAssert.Equal("cooldown", profile.State.MenuOptionCallback.Phase);
            TestAssert.Equal(changedBefore.State.NavigationRevision, profile.State.NavigationRevision);
            TestAssert.Equal(12, profile.State.Rows.Count);
            TestAssert.Equal("Design", profile.State.Rows[0].Label);
            TestAssert.Equal("Classic", profile.State.Rows[0].Value);
            TestAssert.Equal(0, profile.State.SelectedIndex);
        }
        TestAssert.Equal("rebuild", Frame("design-changed-classic-wait49").MenuDesignTransition!.Phase);
        TestAssert.Equal(changedBefore.State.MenuRevision, Frame("design-changed-classic-wait49").MenuRevision);
        var changedRedraw = Frame("design-changed-classic-redraw50");
        TestAssert.Equal("cooldown", changedRedraw.MenuDesignTransition!.Phase);
        TestAssert.Equal("Classic", changedRedraw.Appearance!.Design);
        TestAssert.Equal(changedBefore.State.MenuRevision!.Value + 1, changedRedraw.MenuRevision);
        TestAssert.Equal(changedBefore.State.NavigationRevision, changedRedraw.NavigationRevision);
        TestAssert.True(changedRedraw.MenuOpening is { ElapsedMilliseconds: 0 },
            "The changed-design redraw starts its independent row opening");
        var changedWait249 = Frame("design-changed-classic-wait249");
        TestAssert.Equal("Classic", changedWait249.Appearance!.Design);
        TestAssert.True(changedWait249.MenuOpening is { ElapsedMilliseconds: 199 },
            "The row opening advances independently of the still-active Design callback");
        var changedReady = Frame("design-changed-classic-ready250");
        TestAssert.Equal(changedReceipt, changedReady.MenuUseContinuation);
        TestAssert.True(changedReady.MenuOptionCallback is null && changedReady.MenuDesignTransition is null,
            "The changed Design callback becomes ready at250ms while row opening continues");
        TestAssert.Equal("Classic", changedReady.Appearance!.Design);
        TestAssert.Equal(changedBefore.State.MenuRevision!.Value + 1, changedReady.MenuRevision);
        TestAssert.Equal(changedBefore.State.NavigationRevision, changedReady.NavigationRevision);
        TestAssert.True(changedReady.MenuOpening is { ElapsedMilliseconds: 200 },
            "A ready Design callback does not extend the visible row opening");

        var changedSecond = Profile("design-changed-classic-second-select250");
        AssertSelect(Profile("design-changed-classic-ready250"), changedSecond);
        AssertPlainUse(changedSecond.State);
        TestAssert.Equal("Classic", changedSecond.State.Appearance!.Design);
        TestAssert.Equal(changedReceipt.Revision + 1, changedSecond.State.MenuUseContinuation!.Revision);
        TestAssert.True(changedSecond.State.MenuUseContinuation.Sequence > changedReceipt.Sequence,
            "Held Use writes a fresh Select after the changed Design is ready");
        TestAssert.Equal(changedBefore.State.MenuRevision!.Value + 1, changedSecond.State.MenuRevision);
        TestAssert.Equal(changedBefore.State.NavigationRevision, changedSecond.State.NavigationRevision);
        TestAssert.Equal(changedSecond.State.MenuUseContinuation, Frame("design-changed-classic-wait499").MenuUseContinuation);
        TestAssert.True(Frame("design-changed-classic-wait499").MenuOptionCallback is { Kind: "plain", Phase: "cooldown" },
            "The repeated same-value Design selection owns its own plain wait");
        TestAssert.Equal(changedSecond.State.MenuUseContinuation, Frame("design-changed-classic-ready500").MenuUseContinuation);
        TestAssert.True(Frame("design-changed-classic-ready500").MenuOptionCallback is null,
            "The repeated same-value Design selection becomes ready at500ms");

        var changedNavigation = Profile("design-changed-classic-navigation-moved49");
        TestAssert.Equal(changedFirst.State.LastSequence, changedNavigation.State.LastSequence);
        TestAssert.True(changedNavigation.State.MenuUseContinuation is null && changedNavigation.State.MenuOptionCallback is null,
            "Cursor movement cancels the active changed-design callback and receipt");
        TestAssert.True(changedNavigation.State.NavigationRevision > changedBefore.State.NavigationRevision,
            "Navigation cancellation advances the cursor epoch, independently of redraw");
        TestAssert.Equal(1, changedNavigation.SelectedSourceIndex);

        var changedOwner = Profile("design-changed-classic-owner-replaced0");
        TestAssert.Equal(changedFirst.State.LastSequence, changedOwner.State.LastSequence);
        TestAssert.True(changedOwner.State.MenuUseContinuation is null && changedOwner.State.MenuOptionCallback is null,
            "Actor replacement cancels the active changed-design callback and receipt");
        TestAssert.True(changedOwner.State.LocalMenuEligibility!.Revision
            > changedBefore.State.LocalMenuEligibility!.Revision,
            "Actor replacement changes the admission owner");

        var changedHidden = Frame("design-changed-classic-focus-hidden0");
        var changedReturn = Frame("design-changed-classic-focus-return250");
        TestAssert.True(changedHidden.MenuUseContinuation is null && changedHidden.MenuOptionCallback is null
            && !changedHidden.IsOpen,
            "A focus-hidden changed-design frame carries no callback or receipt");
        TestAssert.True(changedReturn.IsOpen && changedReturn.MenuUseContinuation is null
            && changedReturn.MenuOptionCallback is null,
            "Focus return cannot revive a canceled changed-design receipt");
        foreach (string name in new[] { "design-changed-classic-navigation-moved49", "design-changed-classic-owner-replaced0",
            "design-changed-classic-focus-hidden0", "design-changed-classic-focus-return250" }) {
            TestAssert.Equal(changedFirst.State.LastSequence, Frame(name).LastSequence);
        }
    }

    public static void ActualProgramExportsPacketsAndCommandManifestWithoutReplacingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-preference-use-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(1, Program.Main(new[] { "--export-preference-use-repeat-fixtures" }));
            string relative = "preference-use-repeat-relative-" + Guid.NewGuid().ToString("N");
            TestAssert.Equal(1, Program.Main(new[] { "--export-preference-use-repeat-fixtures", relative }));
            TestAssert.True(!Directory.Exists(relative) && !File.Exists(relative), "Invalid relative output cannot create a local directory");
            TestAssert.Equal(0, Program.Main(new[] { "--export-preference-use-repeat-fixtures", destination }));

            var profiles = PreferenceUseRepeatFixtureProfiles.Build();
            TestAssert.Equal(70, Directory.GetFiles(destination).Length);
            foreach (var profile in profiles) {
                byte[] expected = profile.State.Serialize();
                byte[] actual = File.ReadAllBytes(Path.Combine(destination, profile.Name));
                TestAssert.True(actual.SequenceEqual(expected), "The exporter preserves actual controller bytes: " + profile.Name);
            }
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(destination, "preference-use-manifest.json")));
            var root = manifest.RootElement;
            TestAssert.True(new FileInfo(Path.Combine(destination, "preference-use-manifest.json")).Length <= 32768,
                "The compact provenance manifest fits the native bounded reader");
            TestAssert.Equal("preference-use-repeat", root.GetProperty("graph").GetString());
            TestAssert.Equal("Menu/base.gsc", root.GetProperty("source").GetProperty("file").GetString());
            TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", root.GetProperty("source").GetProperty("commit").GetString());
            var designUsePath = root.GetProperty("source").GetProperty("designUsePath");
            TestAssert.Equal(85, designUsePath.GetProperty("sliderDispatchLine").GetInt32());
            TestAssert.Equal(99, designUsePath.GetProperty("callerWaitLine").GetInt32());
            TestAssert.Equal(168, designUsePath.GetProperty("outerMonitorWaitLine").GetInt32());
            TestAssert.Equal(200, designUsePath.GetProperty("callerWaitMilliseconds").GetInt32());
            TestAssert.Equal(50, designUsePath.GetProperty("outerMonitorWaitMilliseconds").GetInt32());
            TestAssert.Equal(250, designUsePath.GetProperty("nominalUseCycleMilliseconds").GetInt32());
            TestAssert.Equal("Menu/menu_customization.gsc", designUsePath.GetProperty("handlerFile").GetString());
            TestAssert.Equal("29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da",
                designUsePath.GetProperty("handlerSha256").GetString());
            TestAssert.Equal("450-474", designUsePath.GetProperty("handlerLines").GetString());
            TestAssert.Equal(453, designUsePath.GetProperty("unchangedValueReturnLine").GetInt32());
            TestAssert.Equal(50, root.GetProperty("timing").GetProperty("changedDesign")
                .GetProperty("controllerRedrawMilliseconds").GetInt32());
            TestAssert.Equal(250, root.GetProperty("timing").GetProperty("changedDesign")
                .GetProperty("controllerReadyMilliseconds").GetInt32());
            TestAssert.True(!root.GetProperty("session").GetProperty("processMemoryWriterUsed").GetBoolean(), "The fake session has no process memory writer");
            var commands = root.GetProperty("commands").EnumerateArray().ToArray();
            TestAssert.Equal(14, commands.Length);
            foreach (var command in commands) {
                TestAssert.Equal("select", command.GetProperty("input").GetString());
                TestAssert.True(command.GetProperty("sequence").GetUInt64() > command.GetProperty("capture").GetProperty("lastSequence").GetUInt64(),
                    "The captured Select is fresh for its source visit");
                TestAssert.Equal(command.GetProperty("capture").GetProperty("navigationRevision").GetUInt64(),
                    command.GetProperty("wire").GetProperty("navigationRevision").GetUInt64());
                TestAssert.Equal(command.GetProperty("capture").GetProperty("openingControlsRevision").GetUInt64(),
                    command.GetProperty("wire").GetProperty("openingControlsRevision").GetUInt64());
                TestAssert.True(command.GetProperty("capture").GetProperty("admissionRevision").GetUInt64() > 0,
                    "The inert session admission is retained separately from the Select wire fields");
            }
            TestAssert.True(root.GetProperty("frames").EnumerateArray().All(frame => frame.GetProperty("sessionActionCount").GetInt32() == 0),
                "Preference toggles do not dispatch session gameplay actions");
            TestAssert.Equal(69, root.GetProperty("frames").GetArrayLength());

            var hashes = Directory.GetFiles(destination).ToDictionary(path => Path.GetFileName(path)!,
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-preference-use-repeat-fixtures", destination }));
            foreach (string path in Directory.GetFiles(destination))
                TestAssert.Equal(hashes[Path.GetFileName(path)!], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        } finally { Directory.Delete(parent, recursive: true); }
    }

    private static void AssertPhase(StateDocument frame, string? expected) {
        TestAssert.True(frame.MenuPreferenceRefresh is null || frame.MenuOptionCallback is null,
            "Only one source BOOL wait representation may own a frame");
        string? phase = frame.MenuOptionCallback?.Phase ?? frame.MenuPreferenceRefresh?.Phase;
        TestAssert.Equal(expected, phase);
        if (frame.MenuOptionCallback is not null) TestAssert.Equal("boolean", frame.MenuOptionCallback.Kind);
    }

    private static void AssertPlainUse(StateDocument frame) {
        TestAssert.True(frame.MenuOptionCallback is { Kind: "plain", Phase: "cooldown" },
            "Same-design selection owns the ordinary plain caller wait");
        TestAssert.True(frame.MenuUseContinuation is { Kind: "plain" },
            "Same-design selection carries its exact plain Use receipt");
        TestAssert.True(frame.MenuDesignTransition is null,
            "Same-design selection has one callback clock and no visual transition timer");
        TestAssert.Equal(frame.MenuOptionCallback!.Revision, frame.MenuUseContinuation!.Revision);
        TestAssert.Equal(frame.LastSequence, frame.MenuUseContinuation.Sequence);
        TestAssert.Equal(frame.LocalMenuEligibility!.Revision, frame.MenuUseContinuation.AdmissionRevision);
    }

    private static void AssertPlainUseDuringDesign(StateDocument frame, string expectedPhase) {
        TestAssert.True(frame.MenuOptionCallback is { Kind: "plain", Phase: "cooldown" },
            "A changed Design owns the ordinary plain caller wait");
        TestAssert.True(frame.MenuUseContinuation is { Kind: "plain" },
            "A changed Design carries its exact plain Use receipt while rebuilding");
        TestAssert.Equal(expectedPhase, frame.MenuDesignTransition?.Phase);
        TestAssert.Equal(frame.MenuOptionCallback!.Revision, frame.MenuUseContinuation!.Revision);
        TestAssert.Equal(frame.LastSequence, frame.MenuUseContinuation.Sequence);
        TestAssert.Equal(frame.LocalMenuEligibility!.Revision, frame.MenuUseContinuation.AdmissionRevision);
    }

    private static void AssertSelect(PreferenceUseRepeatFixtureProfile before, PreferenceUseRepeatFixtureProfile after) {
        var command = after.OriginalCommand!;
        var capture = after.Capture!;
        TestAssert.Equal(OverlayInput.Select, command.Input);
        TestAssert.Equal(before.State.LastSequence + 1, command.Sequence);
        TestAssert.Equal(command.Sequence, after.State.LastSequence);
        TestAssert.Equal(before.State.NavigationRevision, capture.NavigationRevision);
        TestAssert.Equal(before.State.OpeningPolicies!.Revision, capture.OpeningControlsRevision);
        TestAssert.Equal(before.State.LocalMenuEligibility!.Revision, capture.AdmissionRevision);
        TestAssert.Equal(before.State.MenuRevision!.Value, capture.MenuRevision);
        TestAssert.Equal(before.State.LastSequence, capture.LastSequence);
        TestAssert.Equal(capture.NavigationRevision, command.NavigationRevision!.Value);
        TestAssert.Equal(capture.OpeningControlsRevision, command.OpeningControlsRevision!.Value);
        TestAssert.True(command.LocalAdmissionRevision is null && command.MenuRevision is null,
            "The original Select keeps admission capture separate from the wire envelope");
    }

    private static bool Value(MenuAppearance appearance, MenuPreference preference) => preference switch {
        MenuPreference.QuickExit => appearance.QuickExit,
        MenuPreference.SpotlightCursor => appearance.SpotlightCursor,
        MenuPreference.ColoredCursor => appearance.ColoredCursor,
        MenuPreference.LargeCursor => appearance.LargeCursor,
        MenuPreference.StealthUI => appearance.StealthUI,
        _ => throw new ArgumentOutOfRangeException(nameof(preference))
    };

    private static long RelativeTime(string suffix) => suffix switch {
        "before" or "select0" => 0,
        "wait179" => 179,
        "redraw180" => 180,
        "wait429" => 429,
        "ready430" or "second-select430" => 430,
        "second-redraw610" => 610,
        "second-ready860" => 860,
        _ => throw new ArgumentOutOfRangeException(nameof(suffix))
    };
}
