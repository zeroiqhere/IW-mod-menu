using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal sealed record PreferenceUseRepeatFixtureProfile(string Name, string Graph, string Event,
    long ClockMilliseconds, long RelativeMilliseconds, MenuPage Page, int SelectedSourceIndex,
    StateDocument State, CommandEnvelope? OriginalCommand, PreferenceUseRepeatFixtureProfiles.CommandCapture? Capture,
    int SessionActionCount);

// Every packet comes from the controller that owns its matching source command
internal static class PreferenceUseRepeatFixtureProfiles {
    internal static readonly (string Prefix, string Label, int Index, MenuPreference Preference)[] Flags = {
        ("quick-exit", "Quick Exit", 4, MenuPreference.QuickExit),
        ("spotlight", "Spotlight Cursor", 6, MenuPreference.SpotlightCursor),
        ("colored", "Colored Cursor", 7, MenuPreference.ColoredCursor),
        ("large", "Large Cursor", 8, MenuPreference.LargeCursor),
        ("stealth", "Stealth UI", 10, MenuPreference.StealthUI)
    };
    private static readonly (string Suffix, string Event)[] Stages = {
        ("before", "before-select"),
        ("select0", "first-select"),
        ("wait179", "first-wait-179"),
        ("redraw180", "first-redraw-180"),
        ("wait429", "first-wait-429"),
        ("ready430", "first-ready-430"),
        ("second-select430", "second-select"),
        ("second-redraw610", "second-redraw-610"),
        ("second-ready860", "second-ready-860")
    };
    private static readonly (string Suffix, string Event)[] DesignStages = {
        ("before", "before-select"),
        ("select0", "first-select"),
        ("wait249", "first-wait-249"),
        ("ready250", "first-ready-250"),
        ("second-select250", "second-select"),
        ("wait499", "second-wait-499"),
        ("ready500", "second-ready-500")
    };
    private static readonly (string Suffix, string Event)[] ChangedDesignStages = {
        ("before", "before-select"),
        ("select0", "first-select"),
        ("wait49", "first-wait-49"),
        ("redraw50", "first-redraw-50"),
        ("wait249", "first-wait-249"),
        ("ready250", "first-ready-250"),
        ("second-select250", "second-select"),
        ("wait499", "second-wait-499"),
        ("ready500", "second-ready-500")
    };

    internal sealed record CommandCapture(ulong NavigationRevision, ulong OpeningControlsRevision,
        ulong AdmissionRevision, ulong MenuRevision, ulong LastSequence, int SelectedSourceIndex,
        string SelectedSourceLabel);

    public static IReadOnlyList<PreferenceUseRepeatFixtureProfile> Build() {
        var profiles = new List<PreferenceUseRepeatFixtureProfile>(Flags.Length * Stages.Length);
        foreach (var flag in Flags) {
            var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
            LocalOptionCallbackControllerTests.SelectIndex(rig, flag.Index);
            long origin = Now(rig);
            Add(profiles, flag.Prefix, Stages[0], rig, origin);
            AddSelect(profiles, flag.Prefix, Stages[1], rig, origin);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[2], rig, origin, 179);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[3], rig, origin, 1);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[4], rig, origin, 249);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[5], rig, origin, 1);
            AddSelect(profiles, flag.Prefix, Stages[6], rig, origin);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[7], rig, origin, 180);
            AdvanceAndAdd(profiles, flag.Prefix, Stages[8], rig, origin, 250);
        }
        AddSameDesignProfiles(profiles);
        AddChangedDesignProfiles(profiles);
        return profiles.AsReadOnly();
    }

    private static void AddChangedDesignProfiles(ICollection<PreferenceUseRepeatFixtureProfile> profiles) {
        var changed = ChangedClassic();
        long origin = Now(changed);
        Add(profiles, "design-changed-classic", ChangedDesignStages[0], changed, origin);
        AddSelect(profiles, "design-changed-classic", ChangedDesignStages[1], changed, origin);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[2], changed, origin, 49);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[3], changed, origin, 1);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[4], changed, origin, 199);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[5], changed, origin, 1);
        AddSelect(profiles, "design-changed-classic", ChangedDesignStages[6], changed, origin);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[7], changed, origin, 249);
        AdvanceAndAdd(profiles, "design-changed-classic", ChangedDesignStages[8], changed, origin, 1);

        // These scope changes begin with the same accepted Design Select as the main graph
        var navigation = ChangedClassic();
        origin = Now(navigation);
        ConsumeSelect(navigation);
        navigation.Clock.Advance(TimeSpan.FromMilliseconds(49));
        navigation.Navigation.SelectIndex(1);
        Add(profiles, "design-changed-classic", ("navigation-moved49", "cursor-moved-before-redraw"), navigation, origin);

        var owner = ChangedClassic();
        origin = Now(owner);
        ConsumeSelect(owner);
        owner.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add(profiles, "design-changed-classic", ("owner-replaced0", "owner-replaced"), owner, origin);

        var focus = ChangedClassic();
        origin = Now(focus);
        ConsumeSelect(focus);
        focus.Focus = false;
        Add(profiles, "design-changed-classic", ("focus-hidden0", "focus-hidden"), focus, origin,
            StateFocusProjection.Apply(focus.Controller.Snapshot(), false));
        focus.Clock.Advance(TimeSpan.FromMilliseconds(250));
        focus.Focus = true;
        Add(profiles, "design-changed-classic", ("focus-return250", "focus-return"), focus, origin);
    }

    private static ScopedOpeningFixture ChangedClassic() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(rig, 0);
        rig.StageChoice(OverlayInput.NextChoice);
        return rig;
    }

    private static void AddSameDesignProfiles(ICollection<PreferenceUseRepeatFixtureProfile> profiles) {
        var design = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(design, 0);
        long origin = Now(design);
        Add(profiles, "design-same", DesignStages[0], design, origin);
        AddSelect(profiles, "design-same", DesignStages[1], design, origin);
        AdvanceAndAdd(profiles, "design-same", DesignStages[2], design, origin, 249);
        AdvanceAndAdd(profiles, "design-same", DesignStages[3], design, origin, 1);
        AddSelect(profiles, "design-same", DesignStages[4], design, origin);
        AdvanceAndAdd(profiles, "design-same", DesignStages[5], design, origin, 249);
        AdvanceAndAdd(profiles, "design-same", DesignStages[6], design, origin, 1);

        // Each cancellation graph starts from its own actual Design Select
        var navigation = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(navigation, 0);
        origin = Now(navigation);
        ConsumeSelect(navigation);
        navigation.Clock.Advance(TimeSpan.FromMilliseconds(249));
        navigation.Navigation.SelectIndex(1);
        Add(profiles, "design-nav", ("moved249", "cursor-moved"), navigation, origin);

        var owner = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(owner, 0);
        origin = Now(owner);
        ConsumeSelect(owner);
        owner.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add(profiles, "design-owner", ("replaced0", "owner-replaced"), owner, origin);

        var focus = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(focus, 0);
        origin = Now(focus);
        ConsumeSelect(focus);
        focus.Focus = false;
        Add(profiles, "design-focus", ("hidden0", "focus-hidden"), focus, origin,
            StateFocusProjection.Apply(focus.Controller.Snapshot(), false));
        focus.Clock.Advance(TimeSpan.FromMilliseconds(250));
        focus.Focus = true;
        Add(profiles, "design-focus", ("return250", "focus-return"), focus, origin);
    }

    private static void AdvanceAndAdd(ICollection<PreferenceUseRepeatFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin, int elapsed) {
        rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
        Add(profiles, graph, stage, rig, origin);
    }

    private static void AddSelect(ICollection<PreferenceUseRepeatFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin) {
        var (command, capture) = ConsumeSelect(rig);
        Add(profiles, graph, stage, rig, origin, command, capture);
    }

    private static (CommandEnvelope Command, CommandCapture Capture) ConsumeSelect(ScopedOpeningFixture rig) {
        StateDocument before = rig.Controller.Snapshot();
        if (rig.Navigation.Page != MenuPage.Preferences || before.SelectedIndex < 0
            || before.SelectedIndex >= before.Rows.Count) {
            throw new InvalidOperationException("A Select must capture the actual Preferences source row");
        }
        ulong policy = before.OpeningPolicies?.Revision ?? before.MainOpeningControls?.Revision
            ?? throw new InvalidOperationException("The source Select requires its captured opening policy");
        ulong admission = before.LocalMenuEligibility?.Revision
            ?? throw new InvalidOperationException("The source Select requires its observed local admission");
        ulong menu = before.MenuRevision
            ?? throw new InvalidOperationException("The source Select requires its actual menu revision");
        // The wire carries visit and policy scope while admission remains a separate source capture
        var capture = new CommandCapture(before.NavigationRevision, policy, admission, menu,
            before.LastSequence, before.SelectedIndex, before.Rows[before.SelectedIndex].Label);
        var command = new CommandEnvelope(before.LastSequence + 1, 1, 42, OverlayInput.Select, true,
            before.NavigationRevision, policy);
        rig.Controller.Consume(command, () => rig.Focus);
        return (command, capture);
    }

    private static void Add(ICollection<PreferenceUseRepeatFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin,
        CommandEnvelope? command = null, CommandCapture? capture = null) {
        Add(profiles, graph, stage, rig, origin, rig.Controller.Snapshot(), command, capture);
    }

    private static void Add(ICollection<PreferenceUseRepeatFixtureProfile> profiles, string graph,
        (string Suffix, string Event) stage, ScopedOpeningFixture rig, long origin, StateDocument state,
        CommandEnvelope? command = null, CommandCapture? capture = null) {
        if (rig.Session.Executed.Count != 0) throw new InvalidOperationException("A local preference must not dispatch a session action");
        profiles.Add(new(graph + "-" + stage.Suffix + ".json", graph, stage.Event,
            Now(rig), Now(rig) - origin, rig.Navigation.Page, rig.Navigation.SelectedIndex,
            state, command, capture, rig.Session.Executed.Count));
    }

    private static long Now(ScopedOpeningFixture rig) =>
        rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;
}
