using System.Reflection;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Empty source pages keep the real controller's navigation proof and cannot dispatch a row
internal static class EmptyPageMonitorContinuationTests {
    private static MenuChromeFixture EmptyMessages() {
        var visit = new MenuChromeFixture();
        visit.SettleMain();
        visit.Select("Messages");
        var frame = visit.Current();
        TestAssert.Equal("Messages", frame.Title);
        TestAssert.Equal(0, frame.Rows.Count);
        return visit;
    }

    private static CommandEnvelope SendScoped(MenuChromeFixture visit, OverlayInput input,
        bool focused = true, ulong? navigationRevision = null,
        ulong? admissionRevision = null, ulong? menuRevision = null) {
        var frame = visit.Current();
        var command = new CommandEnvelope(frame.LastSequence + 1, 1, 42, input, true,
            navigationRevision ?? frame.NavigationRevision, null,
            admissionRevision ?? frame.LocalMenuEligibility!.Revision,
            menuRevision ?? frame.MenuRevision);
        visit.Controller.Consume(command, () => focused);
        return command;
    }

    private static void OwnsBackWait(StateDocument frame, CommandEnvelope command) {
        TestAssert.Equal("back", frame.MenuMonitorWait?.Kind);
        TestAssert.True(frame.MenuMonitorContinuation is not null,
            "A scoped empty-page navigation keeps its own captured invocation");
        TestAssert.Equal(command.Sequence, frame.MenuMonitorContinuation!.Sequence);
        TestAssert.Equal(command.LocalAdmissionRevision!.Value, frame.MenuMonitorContinuation.AdmissionRevision);
        frame.Serialize();
    }

    public static void EmptyMessagesBackReturnsToItsParentAndRootBackCloses() {
        var visit = EmptyMessages();
        var back = SendScoped(visit, OverlayInput.Back);
        var parent = visit.Current();
        TestAssert.True(parent.IsOpen && parent.Title != "Messages",
            "Back on the empty Messages page returns through normal navigation");
        OwnsBackWait(parent, back);
        visit.Wait(250);

        var closeRoot = SendScoped(visit, OverlayInput.Back);
        var closed = visit.Current();
        TestAssert.True(!closed.IsOpen, "Back at the root closes the local menu");
        OwnsBackWait(closed, closeRoot);
        visit.NoGameWrites();
    }

    public static void EmptyMessagesCloseUsesTheExistingLocalQuickExitWait() {
        var visit = new MenuChromeFixture();
        visit.SettleMain();
        visit.Preferences();
        visit.Select("Quick Exit");
        visit.Wait(430);
        visit.Send(OverlayInput.Back);
        visit.Wait(250);
        visit.Send(OverlayInput.Back);
        visit.Wait(250);
        visit.Select("Messages");
        TestAssert.Equal(0, visit.Current().Rows.Count);

        var close = SendScoped(visit, OverlayInput.Close);
        var closed = visit.Current();
        TestAssert.True(!closed.IsOpen, "Quick Exit closes the empty page locally");
        OwnsBackWait(closed, close);
        visit.NoGameWrites();
    }

    public static void EmptyQuickMessagesJumpBackKeepsItsQuickParentAndOwner() {
        var visit = new MenuChromeFixture();
        visit.Session.Change(LocalPlayerLife.Dead);
        visit.Open(OverlayInput.OpenQuick);
        visit.Wait(550);
        var navigation = (OverlayNavigation)typeof(BackendController)
            .GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(visit.Controller)!;
        navigation.OpenPage(MenuPage.History);
        var empty = visit.Current();
        TestAssert.Equal("Messages", empty.Title);
        TestAssert.Equal(0, empty.Rows.Count);

        var jumpBack = SendScoped(visit, OverlayInput.JumpBack);
        var parent = visit.Current();
        TestAssert.True(parent.IsOpen && parent.MenuSurface == "quick" && parent.Title == "Quick Menu",
            "Dead Jump Back returns to its actual Quick parent");
        OwnsBackWait(parent, jumpBack);
        visit.NoGameWrites();
    }

    public static void EmptyPagesRejectStaleScopesFocusLossAndScopedRowChoices() {
        foreach (string fault in new[] { "navigation", "admission", "menu", "focus", "choice" }) {
            var visit = EmptyMessages();
            var before = visit.Current();
            switch (fault) {
                case "navigation":
                    SendScoped(visit, OverlayInput.Back, navigationRevision: before.NavigationRevision + 1);
                    break;
                case "admission":
                    SendScoped(visit, OverlayInput.Back, admissionRevision: before.LocalMenuEligibility!.Revision + 1);
                    break;
                case "menu":
                    SendScoped(visit, OverlayInput.Back, menuRevision: before.MenuRevision!.Value + 1);
                    break;
                case "focus":
                    SendScoped(visit, OverlayInput.Back, focused: false);
                    break;
                case "choice":
                    SendScoped(visit, OverlayInput.NextChoice);
                    break;
            }
            var after = visit.Current();
            TestAssert.Equal("Messages", after.Title);
            TestAssert.Equal(0, after.Rows.Count);
            TestAssert.True(after.MenuMonitorWait is null && after.MenuMonitorContinuation is null,
                "Rejected proof or a row choice cannot create a monitor invocation");
            visit.Send(OverlayInput.Select);
            TestAssert.Equal(0, visit.Current().Rows.Count);
            visit.NoGameWrites();
        }
    }
}
