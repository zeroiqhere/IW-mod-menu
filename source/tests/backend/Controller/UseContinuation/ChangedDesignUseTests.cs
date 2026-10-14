using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// A design redraw replaces the HUD while the same logical slider caller keeps waiting
internal static class ChangedDesignUseTests {
    private static ScopedOpeningFixture Ready(string name = "Classic") {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        for (int moves = 0; moves < MenuDesignTests.Names.Length && rig.Controller.Snapshot().Rows[0].Value != name; moves++)
            rig.StageChoice(OverlayInput.NextChoice);
        TestAssert.Equal(name, rig.Controller.Snapshot().Rows[0].Value);
        return rig;
    }

    private static MenuUseContinuation Receipt(StateDocument frame) {
        TestAssert.True(frame.MenuUseContinuation is not null, "A changed Design must keep its admitted caller receipt");
        return frame.MenuUseContinuation!;
    }

    public static void ChangedDesignKeepsOneExactCallerThroughRedrawAndRepeatsBeforeRowFadesFinish() {
        foreach (string name in MenuDesignTests.Names.Skip(1)) {
            var rig = Ready(name); var before = rig.Controller.Snapshot();
            rig.Send(OverlayInput.Select); var first = rig.Controller.Snapshot(); var receipt = Receipt(first);
            TestAssert.Equal(first.LastSequence, receipt.Sequence);
            TestAssert.Equal(first.LocalMenuEligibility!.Revision, receipt.AdmissionRevision);
            TestAssert.Equal(new MenuOptionCallback(receipt.Revision, "plain", "cooldown"), first.MenuOptionCallback);
            TestAssert.Equal("rebuild", first.MenuDesignTransition!.Phase);
            TestAssert.Equal("Apparition", first.Appearance!.Design);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(49));
            TestAssert.Equal(receipt, Receipt(rig.Controller.Snapshot()));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var redrawn = rig.Controller.Snapshot();
            TestAssert.Equal(receipt, Receipt(redrawn));
            TestAssert.Equal(first.MenuOptionCallback, redrawn.MenuOptionCallback);
            TestAssert.Equal("cooldown", redrawn.MenuDesignTransition!.Phase);
            TestAssert.Equal(name, redrawn.Appearance!.Design);
            TestAssert.Equal(before.MenuRevision + 1, redrawn.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, redrawn.NavigationRevision);
            TestAssert.Equal(0, redrawn.SelectedIndex);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(199));
            rig.Send(OverlayInput.Down); var waiting = rig.Controller.Snapshot();
            TestAssert.Equal(0, waiting.SelectedIndex);
            TestAssert.Equal(receipt, Receipt(waiting));
            rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Controller.Snapshot();
            TestAssert.True(ready.MenuOptionCallback is null && ready.MenuDesignTransition is null,
                "One design clock releases its callback view at 250 ms");
            TestAssert.True(ready.MenuOpening is not null, "Later row fades can continue after the caller wait");
            TestAssert.Equal(receipt, Receipt(ready));
            rig.Send(OverlayInput.Select); var second = rig.Controller.Snapshot();
            TestAssert.Equal(receipt.Revision + 1, Receipt(second).Revision);
            TestAssert.Equal(second.LastSequence, Receipt(second).Sequence);
            TestAssert.True(second.MenuDesignTransition is null, "The repeated applied value takes the ordinary no-op path");
            TestAssert.Equal(redrawn.MenuRevision, second.MenuRevision);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null, "The second caller owns its own finite wait");
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void ChangedDesignReceiptWithdrawsOnCursorTopologyNavigationActorLifeFocusAndShutdown() {
        foreach (string change in new[] { "cursor", "topology", "navigation", "actor", "life", "focus", "shutdown" }) {
            var rig = Ready(); rig.Send(OverlayInput.Select); Receipt(rig.Controller.Snapshot());
            if (change == "cursor") rig.Navigation.SelectIndex(1);
            if (change == "topology") {
                var rows = rig.Navigation.Rows.ToArray(); rows[0] = rows[0] with { Label = "Changed source row" };
                rig.Navigation.SetRows(rows);
            }
            if (change == "navigation") rig.Navigation.OpenPage(MenuPage.Instructions);
            if (change == "actor") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "life") rig.Session.Change(LocalPlayerLife.Unknown);
            if (change == "focus") rig.Focus = false;
            if (change == "shutdown") rig.Controller.RequestShutdown();
            TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                "A changed source scope cannot retain a Design caller: " + change);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(500)); rig.Focus = true;
            TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                "A cancelled Design caller cannot become ready after its former deadline");
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void DesignClockRollbackKeepsItsReceiptAndDoesNotRepeatTheCompletedRedraw() {
        var rig = Ready(); rig.Send(OverlayInput.Select); var receipt = Receipt(rig.Controller.Snapshot());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); var redrawn = rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(100)); rig.Controller.Snapshot();
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.Equal(receipt, Receipt(rig.Controller.Snapshot()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(199));
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is not null, "Recovery preserves the remaining 200 ms design stage");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1)); var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null && ready.MenuDesignTransition is null, "Recovered design wait remains finite");
        TestAssert.Equal(receipt, Receipt(ready));
        TestAssert.Equal(redrawn.MenuRevision, ready.MenuRevision);
        TestAssert.Equal("Classic", ready.Appearance!.Design);
    }

    public static void ExhaustedDesignCallbackAdmissionCannotCommitAppearanceOrArmEitherWait() {
        var rig = Ready(); var before = rig.Controller.Snapshot();
        typeof(BackendController).GetField("optionCallbackRevision",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(rig.Controller, ulong.MaxValue);
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        var after = rig.Controller.Snapshot();
        TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.MenuRevision, after.MenuRevision);
        TestAssert.True(after.MenuUseContinuation is null && after.MenuOptionCallback is null && after.MenuDesignTransition is null,
            "Failed caller epoch admission cannot spend the design mutation or presentation wait");
    }

    public static void DesignWireRequiresAnExactPlainCallbackReceiptPairAndRejectsBorrowedPhases() {
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        rig.Send(OverlayInput.Select); var plain = rig.Controller.Snapshot() with {
            // Separate chrome construction has its own rebuild rules and is outside this receipt copy
            ChromeTheme = null, InstructionTheme = null, InstructionHud = null, MenuChromeRevision = null, MenuOpening = null
        };
        foreach (var transition in new[] { new MenuDesignTransition("rebuild", "Classic"), new MenuDesignTransition("cooldown") }) {
            var valid = plain with { MenuDesignTransition = transition };
            TestAssert.True(valid.Serialize().Length > 0, "A matching plain caller can accompany its Design presentation phase");
            foreach (var invalid in new[] {
                valid with { MenuUseContinuation = null }, valid with { MenuOptionCallback = null },
                valid with { MenuUseContinuation = valid.MenuUseContinuation! with { Revision = valid.MenuUseContinuation!.Revision + 1 } },
                valid with { MenuOptionCallback = new(valid.MenuOptionCallback!.Revision, "boolean", "pending") },
                valid with { MenuUseContinuation = valid.MenuUseContinuation! with { Kind = "boolean" } },
                valid with { LocalMenuEligibility = null }, valid with { MenuUnlock = "waiting" }
            }) TestAssert.Throws<InvalidOperationException>(() => invalid.Serialize());
        }
    }
}
