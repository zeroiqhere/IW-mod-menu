using System.Reflection;
using System.Text;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Real controller commands bind each sleeping branch to its original copied visit
internal static class MenuMonitorContinuationTests {
    internal sealed class Rig {
        public MenuChromeFixture Visit { get; }
        public ulong Sequence { get; private set; } = 1000;
        public bool Focused { get; set; } = true;
        public Rig(MenuChromeFixture? visit = null, bool settleMain = true) { Visit = visit ?? new(); if (settleMain) Visit.SettleMain(); }
        public StateDocument Frame => Visit.Current();
        public void Send(OverlayInput input) {
            var before = Frame;
            bool mechanical = input is OverlayInput.NextChoice or OverlayInput.PreviousChoice or OverlayInput.Back
                or OverlayInput.Close or OverlayInput.JumpBack;
            Visit.Controller.Consume(new(++Sequence, 1, 42, input, true, before.NavigationRevision,
                input is OverlayInput.NextChoice or OverlayInput.PreviousChoice ? before.OpeningPolicies!.Revision : null,
                mechanical ? before.LocalMenuEligibility!.Revision : null, mechanical ? before.MenuRevision : null), () => Focused);
        }
        public void Highlight(string label) {
            int target = Frame.Rows.ToList().FindIndex(row => row.Label == label);
            TestAssert.True(target >= 0, "Missing actual row: " + label);
            for (int index = 0; index < Frame.Rows.Count && Frame.SelectedIndex != target; index++) Send(OverlayInput.Down);
            TestAssert.Equal(target, Frame.SelectedIndex);
        }
        public void Preferences() { Visit.Preferences(); Visit.Highlight("Design"); }
        public void Wait(int milliseconds) => Visit.Wait(milliseconds);
    }
    internal static MenuMonitorContinuation Receipt(StateDocument frame) {
        TestAssert.True(frame.MenuMonitorContinuation is not null, "The actual admitted mechanical branch must retain its causal receipt");
        return frame.MenuMonitorContinuation!;
    }
    private static void None(StateDocument frame) => TestAssert.True(frame.MenuMonitorContinuation is null,
        "Canceled or legacy monitor waits cannot become causal ready receipts");
    internal static OverlayNavigation Navigation(Rig rig) => (OverlayNavigation)typeof(BackendController)
        .GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Visit.Controller)!;

    public static void ScopedChoiceAndNonsliderWaitsRetainOriginalSequenceThroughNormalExpiry() {
        var rig = new Rig(); rig.Preferences();
        foreach (var entry in new[] { ("Design", "choice", 180), ("Quick Exit", "choiceNoop", 50) }) {
            rig.Highlight(entry.Item1); var before = rig.Frame; rig.Send(OverlayInput.NextChoice);
            var started = rig.Frame; var receipt = Receipt(started);
            TestAssert.Equal(entry.Item2, receipt.Kind); TestAssert.Equal(rig.Sequence, receipt.Sequence);
            TestAssert.Equal(before.LocalMenuEligibility!.Revision, receipt.AdmissionRevision);
            TestAssert.Equal(started.MenuMonitorWait!.Revision, receipt.Revision);
            rig.Send(OverlayInput.Down); TestAssert.True(rig.Sequence > receipt.Sequence, "An ignored later ACK must not replace the captured sequence");
            rig.Wait(entry.Item3 - 1); TestAssert.Equal(receipt, Receipt(rig.Frame));
            rig.Wait(1); var ready = rig.Frame; TestAssert.True(ready.MenuMonitorWait is null, "Normal expiry alone finishes the sleeping branch");
            TestAssert.Equal(receipt, Receipt(ready)); rig.Send(OverlayInput.PreviousChoice);
            TestAssert.Equal(receipt.Revision + 1, Receipt(rig.Frame).Revision); rig.Wait(entry.Item3);
        }
        rig.Visit.NoGameWrites();
    }
    public static void OwnBackBindsFreshParentAndClosedRootWithoutAssumingOneNavigationIncrement() {
        var rig = new Rig(); rig.Preferences(); var child = rig.Frame; rig.Send(OverlayInput.Back);
        var parent = rig.Frame; var receipt = Receipt(parent);
        TestAssert.True(parent.Title != child.Title && parent.NavigationRevision > child.NavigationRevision,
            "Back publishes only its own newly drawn parent");
        rig.Wait(249); TestAssert.Equal(receipt, Receipt(rig.Frame)); rig.Wait(1);
        TestAssert.Equal(receipt, Receipt(rig.Frame));
        rig.Send(OverlayInput.Back); rig.Wait(250); rig.Send(OverlayInput.Back);
        var closed = rig.Frame; TestAssert.True(!closed.IsOpen && closed.IsRootMenu, "The source root closes without opening another menu");
        var final = Receipt(closed); rig.Wait(250); TestAssert.Equal(final, Receipt(rig.Frame));
        var reopening = rig.Frame;
        rig.Visit.Controller.Consume(new(rig.Sequence + 1, 1, 42, OverlayInput.OpenMain, true,
            reopening.NavigationRevision, reopening.OpeningPolicies!.Revision, reopening.LocalMenuEligibility!.Revision), () => true);
        rig.Visit.Controller.StartQueuedAction();
        TestAssert.True(SpinWait.SpinUntil(() => { rig.Visit.Controller.ObserveCompletion(); return !rig.Visit.Controller.Busy; }, TimeSpan.FromSeconds(3)), "Fresh opening validation must finish");
        TestAssert.True(rig.Frame.IsOpen, "A separately authorized fresh opening may follow the final wait"); None(rig.Frame);
        // The retained parent cursor may be outside the new row count and require another epoch
        var clamped = new Rig(); clamped.Preferences(); var navigation = Navigation(clamped);
        var cursors = (Dictionary<MenuPage, int>)typeof(OverlayNavigation).GetField("cursors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(navigation)!;
        cursors[MenuPage.Customization] = 99;
        ulong original = clamped.Frame.NavigationRevision; clamped.Send(OverlayInput.Back);
        var actual = clamped.Frame; TestAssert.Equal(original + 3, actual.NavigationRevision);
        Receipt(actual); clamped.Wait(250); Receipt(clamped.Frame); clamped.Visit.NoGameWrites();
    }
    public static void CloseAndDeadQuickJumpCaptureOnlyTheirOwnActualClosedResult() {
        var main = new Rig(); main.Preferences(); main.Visit.Highlight("Quick Exit"); main.Visit.Send(OverlayInput.Select); main.Wait(430);
        main.Send(OverlayInput.Close); var closed = main.Frame;
        TestAssert.True(!closed.IsOpen, "Close retains the source-only body transition"); Receipt(closed); main.Wait(250); Receipt(main.Frame);
        var visit = new MenuChromeFixture(); visit.Session.Change(LocalPlayerLife.Dead);
        visit.Open(OverlayInput.OpenQuick); visit.Wait(550);
        var before = visit.Current(); visit.Controller.Consume(new(1000, 1, 42, OverlayInput.JumpBack, true,
            before.NavigationRevision, null, before.LocalMenuEligibility!.Revision, before.MenuRevision), () => true);
        var quick = visit.Current(); TestAssert.True(!quick.IsOpen && quick.MenuSurface == "quick", "Dead Jump exits only its actual Quick family");
        Receipt(quick); visit.Wait(250); Receipt(visit.Current()); visit.NoGameWrites();
    }
    public static void IndependentTopologyOwnerFocusAndCompetingInvocationRetireCausalHistory() {
        foreach (string fault in new[] { "topology", "target", "cohort", "owner", "life", "permission", "focus", "shutdown", "select" }) {
            var rig = new Rig(); rig.Preferences(); rig.Send(OverlayInput.NextChoice); rig.Wait(180); Receipt(rig.Frame);
            switch (fault) {
                case "topology": var rows = Navigation(rig).Rows.ToArray(); rows[0] = rows[0] with { SourceChoiceRole = MenuSourceChoiceRole.Unknown }; Navigation(rig).SetRows(rows); break;
                case "target": var targetRows = Navigation(rig).Rows.ToArray();
                    targetRows[0] = targetRows[0] with { Action = targetRows[0].Action with {
                        ExpectedTarget = SourcePlayerInventoryTests.State().SelectedPlayerTarget } };
                    Navigation(rig).SetRows(targetRows); break;
                case "cohort": var cohortRows = Navigation(rig).Rows.ToArray();
                    cohortRows[0] = cohortRows[0] with { Action = cohortRows[0].Action with {
                        ExpectedCohort = CohortGodmodeMenuFixture.Target() } };
                    Navigation(rig).SetRows(cohortRows); break;
                case "owner": rig.Visit.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); break;
                case "life": rig.Visit.Session.Change(LocalPlayerLife.Unknown); break;
                case "permission": rig.Visit.Session.Change(LocalPlayerLife.Alive, hasMenu: false); break;
                case "focus": rig.Focused = false; break;
                case "shutdown": rig.Visit.Controller.RequestShutdown(); break;
                case "select": rig.Highlight("Bool Display"); rig.Send(OverlayInput.Select); break;
            }
            None(rig.Frame); rig.Visit.NoGameWrites();
        }
        // A metadata-only personal target has no writer action to carry its selection epoch
        var scoped = new Rig(); var actor = ScopedOpeningFixture.Actor;
        var originalTarget = new PlayerMenuTarget(actor, PlayerMenuRoute.Personal, scoped.Visit.Session.Admission.OwnerToken, 1);
        scoped.Visit.Session.State = scoped.Visit.Session.State with { SelectedSlot = actor.Slot, SelectedName = actor.Name,
            SelectedPlayerTarget = originalTarget, PersonalGodmodeIdentity = null };
        typeof(BackendController).GetField("mainPlayerTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scoped.Visit.Controller, originalTarget);
        Navigation(scoped).OpenPage(MenuPage.SelectedBasicScripts);
        TestAssert.Equal(MenuActionKind.None, NavigationAfterSnapshot(scoped).Rows[0].Action.Kind);
        scoped.Send(OverlayInput.NextChoice); scoped.Wait(50); Receipt(scoped.Frame);
        scoped.Visit.Session.State = scoped.Visit.Session.State with {
            SelectedPlayerTarget = new PlayerMenuTarget(actor, PlayerMenuRoute.Personal, originalTarget.OwnerToken, 2) };
        None(scoped.Frame); scoped.Visit.NoGameWrites();
    }
    private static OverlayNavigation NavigationAfterSnapshot(Rig rig) { _ = rig.Frame; return Navigation(rig); }
    public static void CapturedMenuAdmissionAndNavigationRejectForeignMechanicalCommandsBeforeMutation() {
        foreach (string fault in new[] { "menu", "admission", "navigation" }) {
            var rig = new Rig(); rig.Preferences(); var before = rig.Frame;
            var command = new CommandEnvelope(1001, 1, 42, OverlayInput.NextChoice, true,
                fault == "navigation" ? before.NavigationRevision + 1 : before.NavigationRevision,
                before.OpeningPolicies!.Revision, fault == "admission" ? before.LocalMenuEligibility!.Revision + 1 : before.LocalMenuEligibility!.Revision,
                fault == "menu" ? before.MenuRevision + 1 : before.MenuRevision);
            rig.Visit.Controller.Consume(command, () => true); var after = rig.Frame;
            TestAssert.Equal(before.Rows[before.SelectedIndex].Value, after.Rows[after.SelectedIndex].Value);
            TestAssert.True(after.MenuMonitorWait is null, "Stale captured mechanical scope cannot create a wait"); None(after);
        }
    }
    public static void RollbackRestartsWaitingWithoutTreatingCancellationAsNormalReadyExpiry() {
        var rig = new Rig(); rig.Preferences(); rig.Send(OverlayInput.NextChoice); var receipt = Receipt(rig.Frame);
        rig.Wait(100); rig.Visit.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(90));
        TestAssert.Equal(receipt, Receipt(rig.Frame)); rig.Wait(179);
        TestAssert.True(rig.Frame.MenuMonitorWait is not null, "Clock recovery must retain its full finite wait");
        rig.Wait(1); TestAssert.True(rig.Frame.MenuMonitorWait is null, "Only the recovered normal deadline makes the invocation ready"); Receipt(rig.Frame);
        var faultClock = new MonitorWaitFaultClock(); var failure = new Rig(new(clock: faultClock)); failure.Preferences();
        var before = failure.Frame; faultClock.FailTimestamp = true;
        TestAssert.Throws<IOException>(() => failure.Send(OverlayInput.Back)); faultClock.FailTimestamp = false;
        TestAssert.Equal(before.NavigationRevision, failure.Frame.NavigationRevision); None(failure.Frame);
        var counter = typeof(BackendController).GetField("monitorWaitRevision", BindingFlags.Instance | BindingFlags.NonPublic)!;
        counter.SetValue(failure.Visit.Controller, ulong.MaxValue); TestAssert.Throws<OverflowException>(() => failure.Send(OverlayInput.Back));
        TestAssert.Equal(before.NavigationRevision, failure.Frame.NavigationRevision); None(failure.Frame);
        var heldClock = new MonitorWaitFaultClock(); var waiting = new Rig(new(clock: heldClock)); waiting.Preferences();
        waiting.Send(OverlayInput.NextChoice); var waitingReceipt = Receipt(waiting.Frame);
        heldClock.FailTimestamp = true; TestAssert.Throws<IOException>(() => { _ = waiting.Frame; }); heldClock.FailTimestamp = false;
        TestAssert.Equal(waitingReceipt, Receipt(waiting.Frame));
        TestAssert.True(waiting.Frame.MenuMonitorWait is not null, "A failed clock read cannot turn the pending branch into ready authority");
        waiting.Wait(180); TestAssert.Equal(waitingReceipt, Receipt(waiting.Frame));
    }
    public static void FailedFirstResultPublicationCannotSpendOrRebindTheCapturedBackReceipt() {
        var rig = new Rig(); rig.Preferences(); rig.Send(OverlayInput.Back);
        var field = typeof(BackendController).GetField("instructionHud", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = (LocalInstructionHudLifetime)field.GetValue(rig.Visit.Controller)!;
        // A malformed captured color reaches the real DTO factory after the fresh rows commit
        field.SetValue(rig.Visit.Controller, original with { Created = original.Created! with { CreationAccent = "#GG0000" } });
        TestAssert.Throws<InvalidOperationException>(() => { _ = rig.Frame; });
        rig.WaitWithoutSnapshot(250);
        field.SetValue(rig.Visit.Controller, original); var recovered = rig.Frame; Receipt(recovered);
        TestAssert.True(recovered.MenuMonitorWait is null, "The result may bind after a skipped waiting publication");
        var independent = new Rig(); independent.Preferences(); independent.Send(OverlayInput.Back);
        var second = (LocalInstructionHudLifetime)field.GetValue(independent.Visit.Controller)!;
        field.SetValue(independent.Visit.Controller, second with { Created = second.Created! with { CreationAccent = "#GG0000" } });
        TestAssert.Throws<InvalidOperationException>(() => { _ = independent.Frame; });
        Navigation(independent).OpenPage(MenuPage.ThemeColors); field.SetValue(independent.Visit.Controller, second); None(independent.Frame);
        independent.Visit.NoGameWrites();
    }
    public static void CommandParserRequiresTheCompleteMechanicalTrioAndExactUnsignedEpochs() {
        foreach (string input in new[] { "nextChoice", "previousChoice", "back", "close", "jumpBack" }) {
            string json = "{\"version\":1,\"sequence\":9,\"processId\":42,\"input\":\"" + input + "\",\"navigationRevision\":0,\"localAdmissionRevision\":18446744073709551615,\"menuRevision\":0}";
            string escaped = json.Replace("\"menuRevision\"", "\"\\u006denuRevision\"");
            TestAssert.True(CommandEnvelope.Parse(Encoding.UTF8.GetBytes(escaped)) is { Valid: true }, "Decoded field spelling preserves the exact epoch");
            var parsed = CommandEnvelope.Parse(Encoding.UTF8.GetBytes(json))!;
            TestAssert.True(parsed.Valid, "The exact mechanical trio must parse for " + input); TestAssert.Equal<ulong?>(0, parsed.MenuRevision);
            foreach (string bad in new[] { json.Replace(",\"navigationRevision\":0", ""), json.Replace(",\"localAdmissionRevision\":18446744073709551615", ""),
                json.Replace("\"menuRevision\":0", "\"menuRevision\":null"), json.Replace("\"menuRevision\":0", "\"menuRevision\":0,\"menuRevision\":1"),
                json.Replace("\"menuRevision\":0", "\"menuRevision\":0,\"\\u006denuRevision\":1"),
                json.Replace("\"menuRevision\":0", "\"menuRevision\":18446744073709551616") })
                TestAssert.True(CommandEnvelope.Parse(Encoding.UTF8.GetBytes(bad)) is not { Valid: true }, "Malformed mechanical scopes must be rejected");
        }
    }
    public static void WireRequiresExactWaitSequenceProofAndLeavesLegacyOmissionsUnchanged() {
        var rig = new Rig(); rig.Preferences(); rig.Send(OverlayInput.NextChoice); var frame = rig.Frame; var receipt = Receipt(frame);
        foreach (var bad in new[] { receipt with { Revision = 0 }, receipt with { Sequence = 0 }, receipt with { AdmissionRevision = 0 },
            receipt with { Kind = "plain" }, receipt with { Sequence = frame.LastSequence + 1 }, receipt with { AdmissionRevision = receipt.AdmissionRevision + 1 },
            receipt with { Revision = receipt.Revision + 1 }, receipt with { Kind = "back" } })
            TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuMonitorContinuation = bad }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { IsOpen = false }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { Rows = Array.Empty<StateRow>() }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuOptionCallback = new(1, "plain", "cooldown") }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuUseContinuation = new(1, 1, receipt.AdmissionRevision, "plain") }).Serialize());
        var closed = frame with { IsOpen = false, ChromeTheme = null, MenuMonitorWait = null, MenuMonitorContinuation = receipt with { Kind = "back" } };
        TestAssert.Throws<InvalidOperationException>(() => (closed with { SelectedIndex = closed.Rows.Count }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (closed with { Rows = new[] { new StateRow("", "", false, true) }, SelectedIndex = 0 }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (closed with { Rows = Array.Empty<StateRow>(), SelectedIndex = 1 }).Serialize());
        (closed with { Rows = Array.Empty<StateRow>(), SelectedIndex = 0 }).Serialize();
        None(StateFocusProjection.Apply(frame, false));
        var legacy = new Rig(); legacy.Preferences(); legacy.Visit.Send(OverlayInput.NextChoice);
        TestAssert.True(legacy.Frame.MenuMonitorWait is not null, "Omitted capture epochs retain the old mechanical wait"); None(legacy.Frame);
        var maximum = frame with { InstructionHud = null, InstructionTheme = null, ChromeTheme = null, LastSequence = ulong.MaxValue, MenuMonitorWait = new(ulong.MaxValue, "choice"),
            LocalMenuEligibility = frame.LocalMenuEligibility! with { Revision = ulong.MaxValue },
            MenuMonitorContinuation = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, "choice") }; maximum.Serialize();
    }
    private static void WaitWithoutSnapshot(this Rig rig, int milliseconds) => rig.Visit.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
}
