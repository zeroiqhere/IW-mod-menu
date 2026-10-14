using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal sealed record RejectedPreferencesFixtureProfile(string Name, string Graph, string Event,
    long ClockMilliseconds, long RelativeMilliseconds, MenuPage Page, int SelectedSourceIndex,
    StateDocument State, CommandEnvelope? Command, RejectedPreferencesFixtureProfiles.CommandCapture? Capture,
    int SessionActionCount);

// Each row is captured from its own connected controller visit and preference owner
internal static class RejectedPreferencesFixtureProfiles {
    internal static readonly (string Prefix, string RowLabel, int SourceIndex, string Design, bool Stealth)[] Scenarios = {
        ("stealth-counter", "Option Counter", 9, "Apparition", true),
        ("aio-counter", "Option Counter", 9, "AIO", false),
        ("basic-counter", "Option Counter", 9, "Basic", false),
        ("classic-colored", "Colored Cursor", 7, "Classic", false),
        ("native-colored", "Colored Cursor", 7, "Native", false)
    };

    private static readonly (string Suffix, string Event)[] RejectionStages = {
        ("before", "before-select"),
        ("select0", "first-select"),
        ("wait179", "first-wait-179"),
        ("redraw180", "first-redraw-180"),
        ("wait429", "first-wait-429"),
        ("ready430", "first-ready-430")
    };
    private static readonly (string Suffix, string Event)[] RepeatStages = {
        ("second-select430", "second-select"),
        ("second-wait609", "second-wait-609"),
        ("second-redraw610", "second-redraw-610"),
        ("second-wait859", "second-wait-859"),
        ("second-ready860", "second-ready-860")
    };
    private static readonly (string Suffix, string Event)[] RebuildStages = {
        ("before", "before-select"),
        ("select0", "counter-select"),
        ("wait49", "counter-wait-49"),
        ("redraw50", "counter-redraw-50"),
        ("wait249", "counter-wait-249"),
        ("ready250", "counter-ready-250")
    };
    private static long nextAccount = 1_100_000;

    internal sealed record CommandCapture(string File, ulong NavigationRevision, ulong OpeningControlsRevision,
        ulong AdmissionRevision, ulong MenuRevision, ulong LastSequence, int SelectedSourceIndex,
        string SelectedSourceLabel);

    public static IReadOnlyList<RejectedPreferencesFixtureProfile> Build() {
        var profiles = new List<RejectedPreferencesFixtureProfile>(41);
        foreach (var scenario in Scenarios) {
            var rig = ReadyPreferences();
            PrepareScenario(rig, scenario);
            LocalOptionCallbackControllerTests.SelectIndex(rig, scenario.SourceIndex);
            long origin = Now(rig);
            Add(profiles, scenario.Prefix, RejectionStages[0], rig, origin);
            AddSelect(profiles, scenario.Prefix, RejectionStages[1], rig, origin);
            AdvanceAndAdd(profiles, scenario.Prefix, RejectionStages[2], rig, origin, 179);
            AdvanceAndAdd(profiles, scenario.Prefix, RejectionStages[3], rig, origin, 1);
            AdvanceAndAdd(profiles, scenario.Prefix, RejectionStages[4], rig, origin, 249);
            AdvanceAndAdd(profiles, scenario.Prefix, RejectionStages[5], rig, origin, 1);

            if (scenario.Prefix == "stealth-counter") {
                AddSelect(profiles, scenario.Prefix, RepeatStages[0], rig, origin);
                AdvanceAndAdd(profiles, scenario.Prefix, RepeatStages[1], rig, origin, 179);
                AdvanceAndAdd(profiles, scenario.Prefix, RepeatStages[2], rig, origin, 1);
                AdvanceAndAdd(profiles, scenario.Prefix, RepeatStages[3], rig, origin, 249);
                AdvanceAndAdd(profiles, scenario.Prefix, RepeatStages[4], rig, origin, 1);
            }
        }

        var ordinary = ReadyPreferences();
        LocalOptionCallbackControllerTests.SelectIndex(ordinary, 9);
        long ordinaryOrigin = Now(ordinary);
        Add(profiles, "counter-rebuild", RebuildStages[0], ordinary, ordinaryOrigin);
        AddSelect(profiles, "counter-rebuild", RebuildStages[1], ordinary, ordinaryOrigin);
        AdvanceAndAdd(profiles, "counter-rebuild", RebuildStages[2], ordinary, ordinaryOrigin, 49);
        AdvanceAndAdd(profiles, "counter-rebuild", RebuildStages[3], ordinary, ordinaryOrigin, 1);
        AdvanceAndAdd(profiles, "counter-rebuild", RebuildStages[4], ordinary, ordinaryOrigin, 199);
        AdvanceAndAdd(profiles, "counter-rebuild", RebuildStages[5], ordinary, ordinaryOrigin, 1);
        if (profiles.Count != 41) throw new InvalidOperationException("Rejected preference graph must contain its fixed 41 controller frames");
        return profiles.AsReadOnly();
    }

    private static ScopedOpeningFixture ReadyPreferences() {
        var scope = new LocalMenuPreferenceScope((ulong)Interlocked.Increment(ref nextAccount), new object());
        return LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, preferenceScope: scope);
    }

    private static void PrepareScenario(ScopedOpeningFixture rig,
        (string Prefix, string RowLabel, int SourceIndex, string Design, bool Stealth) scenario) {
        if (scenario.Design != "Apparition") SelectDesign(rig, scenario.Design);
        if (!scenario.Stealth) return;

        LocalOptionCallbackControllerTests.SelectIndex(rig, 10);
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        rig.Controller.Snapshot();
    }

    private static void SelectDesign(ScopedOpeningFixture rig, string design) {
        string[] names = { "Apparition", "Classic", "Native", "AIO", "Basic" };
        int target = Array.IndexOf(names, design);
        if (target < 1) throw new InvalidOperationException("A rejected profile requires one supported changed design");
        LocalOptionCallbackControllerTests.SelectIndex(rig, 0);
        for (int step = 0; step < target; step++) rig.StageChoice(OverlayInput.NextChoice);
        if (rig.Controller.Snapshot().Rows[0].Value != design)
            throw new InvalidOperationException("The actual Design source row did not stage the requested candidate");
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        if (rig.Controller.Snapshot().Appearance?.Design != design)
            throw new InvalidOperationException("The actual source Design callback did not finish its ordinary rebuild");
    }

    private static void AdvanceAndAdd(ICollection<RejectedPreferencesFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin, int elapsed) {
        rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
        Add(profiles, graph, stage, rig, origin);
    }

    private static void AddSelect(ICollection<RejectedPreferencesFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin) {
        StateDocument before = rig.Controller.Snapshot();
        if (rig.Navigation.Page != MenuPage.Preferences || before.SelectedIndex < 0
            || before.SelectedIndex >= before.Rows.Count) {
            throw new InvalidOperationException("A source Select must capture the actual Preferences row");
        }
        ulong policy = before.OpeningPolicies?.Revision ?? before.MainOpeningControls?.Revision
            ?? throw new InvalidOperationException("A source Select requires its observed opening policy");
        ulong admission = before.LocalMenuEligibility?.Revision
            ?? throw new InvalidOperationException("A source Select requires its observed local admission");
        ulong menu = before.MenuRevision
            ?? throw new InvalidOperationException("A source Select requires its actual page revision");
        string captureFile = stage.Suffix == "second-select430"
            ? graph + "-ready430.json" : graph + "-before.json";
        var capture = new CommandCapture(captureFile, before.NavigationRevision, policy, admission, menu,
            before.LastSequence, before.SelectedIndex, before.Rows[before.SelectedIndex].Label);
        var command = new CommandEnvelope(before.LastSequence + 1, 1, 42, OverlayInput.Select, true,
            before.NavigationRevision, policy);
        rig.Controller.Consume(command, () => rig.Focus);
        Add(profiles, graph, stage, rig, origin, command, capture);
    }

    private static void Add(ICollection<RejectedPreferencesFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin,
        CommandEnvelope? command = null, CommandCapture? capture = null) {
        StateDocument state = rig.Controller.Snapshot();
        int actions = rig.Session.Executed.Count;
        if (actions != 0) throw new InvalidOperationException("A local source preference must not execute a session action");
        profiles.Add(new(graph + "-" + stage.Suffix + ".json", graph, stage.Event,
            Now(rig), Now(rig) - origin, rig.Navigation.Page, rig.Navigation.SelectedIndex,
            state, command, capture, actions));
    }

    private static long Now(ScopedOpeningFixture rig) =>
        rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;
}
