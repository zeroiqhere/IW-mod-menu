using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Public commands and copied provider changes drive every frame in the separate opening graph
internal static class ClosedLevelOpeningFixtureProfiles {
    internal sealed record CommandScope(ulong NavigationRevision, ulong? PolicyRevision, ulong AdmissionRevision,
        ulong MenuRevision, ulong LastSequence);
    internal sealed class Rig {
        internal ScopedOpeningFixture Visit { get; } = new(choiceControlsAvailable: true);
        internal StateDocument Frame => Visit.Controller.Snapshot();
        internal CommandEnvelope? Command { get; private set; }
        internal CommandScope? Scope { get; private set; }
        private ulong sequence = 1000;
        internal long Now => Visit.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;
        internal Rig() { Visit.Session.RequiresLocalMenuAdmission = true; }
        internal void Send(OverlayInput input, StateDocument? captured = null) {
            var source = captured ?? Frame;
            bool opening = input is OverlayInput.OpenMain or OverlayInput.OpenQuick;
            bool mechanical = input is OverlayInput.Back or OverlayInput.Close or OverlayInput.JumpBack;
            ulong admission = source.LocalMenuEligibility?.Revision ?? Visit.Session.Admission.Revision;
            Scope = new(source.NavigationRevision, source.OpeningPolicies?.Revision ?? source.MainOpeningControls?.Revision,
                admission, source.MenuRevision ?? throw new InvalidOperationException("The actual source frame requires a menu revision"), source.LastSequence);
            Command = new(++sequence, 1, 42, input, true,
                opening || mechanical || input == OverlayInput.Select ? source.NavigationRevision : null,
                opening || input == OverlayInput.Select ? Scope.PolicyRevision : null,
                opening || mechanical ? admission : null, mechanical ? source.MenuRevision : null);
            Visit.Controller.Consume(Command, () => Visit.Focus);
        }
        internal void Finish() { Visit.Controller.StartQueuedAction(); Visit.Finish(); }
        internal void Wait(int milliseconds) { Visit.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds)); _ = Frame; }
        internal void OpenMain() {
            Send(OverlayInput.OpenMain); Finish(); Wait(50); Wait(550);
        }
        internal void Highlight(string label) {
            int target = Frame.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "The actual source row is missing: " + label);
            for (int attempt = 0; attempt < Frame.Rows.Count && Frame.SelectedIndex != target; attempt++) Send(OverlayInput.Down);
            TestAssert.Equal(target, Frame.SelectedIndex);
        }
        internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
        internal void DisableQuick() {
            OpenMain(); Select("Menu Customization"); Select("Menu Preferences"); Select("Disable Quick Menu"); Wait(430);
            TestAssert.True(Frame.OpeningPolicies!.QuickDisabled, "The real preference action must disable Quick");
            // Every own parent/root Back observes its existing source250 caller wait
            while (Frame.IsOpen) { Send(OverlayInput.Back); Wait(250); }
        }
    }

    internal static IReadOnlyList<ClosedLevelOpeningFixtureExporter.Profile> Build() {
        var profiles = new List<ClosedLevelOpeningFixtureExporter.Profile>();
        var origins = new Dictionary<string, long>(StringComparer.Ordinal);
        void Add(string name, string graph, Rig rig, bool dispatched = false) {
            if (!origins.TryGetValue(graph, out long origin)) { origin = rig.Now; origins.Add(graph, origin); }
            TestAssert.Equal(0, rig.Visit.Session.Executed.Count);
            profiles.Add(new(name + ".json", graph, rig.Now, rig.Now - origin, StateFocusProjection.Apply(rig.Frame, rig.Visit.Focus),
                dispatched ? rig.Command : null, dispatched ? rig.Scope : null, rig.Visit.Focus));
        }
        var main = new Rig(); main.OpenMain(); Add("main-before0", "main", main);
        main.Send(OverlayInput.Back); Add("main-back0", "main", main, true);
        main.Wait(249); Add("main-back249", "main", main); main.Wait(1); Add("main-back250", "main", main);
        main.Send(OverlayInput.OpenMain); Add("main-pending250", "main", main, true); main.Finish(); Add("main-rebuild250", "main", main);
        main.Wait(49); Add("main-rebuild299", "main", main); main.Wait(1); Add("main-drawn300", "main", main);
        main.Wait(549); Add("main-wait849", "main", main); main.Wait(1); Add("main-ready850", "main", main);

        var quick = new Rig(); Add("quick-before0", "quick", quick);
        quick.Send(OverlayInput.OpenQuick); Add("quick-pending0", "quick", quick, true); quick.Finish(); Add("quick-drawn0", "quick", quick);
        quick.Wait(549); Add("quick-wait549", "quick", quick); quick.Wait(1); Add("quick-ready550", "quick", quick);

        var dead = new Rig(); dead.Visit.Session.Change(LocalPlayerLife.Dead); Add("dead-before0", "dead", dead);
        dead.Send(OverlayInput.OpenQuick); Add("dead-pending0", "dead", dead, true); dead.Finish(); Add("dead-drawn0", "dead", dead);
        dead.Wait(549); Add("dead-wait549", "dead", dead); dead.Wait(1); Add("dead-ready550", "dead", dead);
        dead.Send(OverlayInput.JumpBack); Add("dead-jump550", "dead", dead, true);
        dead.Wait(249); Add("dead-jump799", "dead", dead); dead.Wait(1); Add("dead-jump800", "dead", dead);
        dead.Send(OverlayInput.OpenQuick); Add("dead-reopen-pending800", "dead", dead, true); dead.Finish(); Add("dead-reopened800", "dead", dead);

        var permission = new Rig(); permission.Visit.Session.Change(LocalPlayerLife.Alive, hasMenu: false); Add("permission-before0", "permission", permission);
        permission.Send(OverlayInput.OpenQuick); Add("permission-denied0", "permission", permission, true);
        TestAssert.Equal(0, permission.Visit.Session.Validations);
        permission.Visit.Session.Change(LocalPlayerLife.Alive); Add("permission-recovered0", "permission", permission);
        permission.Send(OverlayInput.OpenQuick); Add("permission-pending0", "permission", permission, true);
        permission.Finish(); Add("permission-drawn0", "permission", permission);

        var disabled = new Rig(); disabled.DisableQuick(); Add("disabled-before0", "disabled", disabled);
        disabled.Send(OverlayInput.OpenQuick); Add("disabled-denied0", "disabled", disabled, true);
        disabled.Send(OverlayInput.OpenMain); Add("disabled-main-pending0", "disabled", disabled, true);
        disabled.Finish(); Add("disabled-main-rebuild0", "disabled", disabled); disabled.Wait(50); Add("disabled-main50", "disabled", disabled);

        var unknown = new Rig(); unknown.Visit.Session.Change(LocalPlayerLife.Unknown); Add("unknown-before0", "unknown", unknown);
        unknown.Send(OverlayInput.OpenQuick); Add("unknown-denied0", "unknown", unknown, true); TestAssert.Equal(0, unknown.Visit.Session.Validations);
        var missing = new Rig(); var missingCapture = missing.Frame; missing.Visit.Session.AdmissionUnavailable = true;
        Add("missing-before0", "missing", missing); missing.Send(OverlayInput.OpenQuick, missingCapture); Add("missing-denied0", "missing", missing, true);
        TestAssert.Equal(0, missing.Visit.Session.Validations);

        // Stale commands use an earlier real complete snapshot, never a synthetic mixed scope
        var staleNav = new Rig(); var navCapture = staleNav.Frame; Add("stale-navigation-capture0", "stale-navigation", staleNav);
        staleNav.Send(OverlayInput.OpenQuick); staleNav.Finish(); staleNav.Wait(550); staleNav.Send(OverlayInput.Back); staleNav.Wait(250);
        Add("stale-navigation-current800", "stale-navigation", staleNav);
        staleNav.Send(OverlayInput.OpenQuick, navCapture); Add("stale-navigation-denied800", "stale-navigation", staleNav, true);
        var stalePolicy = new Rig(); var policyCapture = stalePolicy.Frame; Add("stale-policy-capture0", "stale-policy", stalePolicy);
        stalePolicy.DisableQuick(); Add("stale-policy-current1780", "stale-policy", stalePolicy);
        stalePolicy.Send(OverlayInput.OpenMain, policyCapture); Add("stale-policy-denied1780", "stale-policy", stalePolicy, true);
        var staleAdmission = new Rig(); var admissionCapture = staleAdmission.Frame; Add("stale-admission-capture0", "stale-admission", staleAdmission);
        staleAdmission.Visit.Session.Change(LocalPlayerLife.Alive); Add("stale-admission-current0", "stale-admission", staleAdmission);
        staleAdmission.Send(OverlayInput.OpenQuick, admissionCapture); Add("stale-admission-denied0", "stale-admission", staleAdmission, true);

        foreach (string cause in new[] { "focus", "admission", "owner" }) {
            var rig = new Rig(); string graph = "queued-" + cause; Add(graph + "-before0", graph, rig);
            var original = rig.Visit.Session.Admission;
            rig.Visit.Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Send(OverlayInput.OpenQuick); rig.Visit.Controller.StartQueuedAction();
            TestAssert.True(SpinWait.SpinUntil(() => Volatile.Read(ref rig.Visit.Session.Validations) == 1, TimeSpan.FromSeconds(3)),
                "The actual retained validation did not start");
            Add(graph + "-pending0", graph, rig, true);
            if (cause == "focus") rig.Visit.Focus = false;
            else rig.Visit.Session.Change(LocalPlayerLife.Alive, replaceOwner: cause == "owner");
            rig.Visit.Session.Pending.SetResult(original); rig.Visit.Finish(); Add(graph + "-retired0", graph, rig);
        }
        if (profiles.Count != ClosedLevelOpeningFixtureExporter.ProfileCount)
            throw new InvalidOperationException("The actual closed opening inventory is incomplete");
        return profiles.AsReadOnly();
    }
}
