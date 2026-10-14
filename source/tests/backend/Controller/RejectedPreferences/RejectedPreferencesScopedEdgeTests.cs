using System.Reflection;
using Apparition.Backend.Controller;
using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static partial class RejectedPreferencesControllerTests {
    public static void RejectedCounterClockFailureLeavesFeedbackReceiptAppearanceAndStoredRecordUntouched() =>
        AssertRejectedPreflight("clock");

    public static void RejectedCounterMenuEpochFailureLeavesFeedbackReceiptAppearanceAndStoredRecordUntouched() =>
        AssertRejectedPreflight("menu");

    public static void RejectedCounterCallbackEpochFailureLeavesFeedbackReceiptAppearanceAndStoredRecordUntouched() =>
        AssertRejectedPreflight("callback");

    public static void UnrelatedBooleanPaintExhaustionDoesNotBlockRejectedPreferenceWait() {
        var rig = ReadyStealthCounter(ulong.MaxValue, out _);
        var before = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select);
        var pending = rig.Controller.Snapshot();
        var receipt = AssertRejectedBoolean(pending, before.LastSequence + 1);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw = rig.Controller.Snapshot();
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal(before.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
        TestAssert.Equal(receipt, redraw.MenuUseContinuation);
    }

    public static void OneRemainingMenuEpochAllowsRejectedCounterRedrawAt180Milliseconds() {
        var rig = ReadyStealthCounter(0, out _);
        SetMenuRevision(rig, ulong.MaxValue - 1);
        var before = rig.Controller.Snapshot();
        rig.Send(OverlayInput.Select);
        var pending = rig.Controller.Snapshot();
        var receipt = AssertRejectedBoolean(pending, before.LastSequence + 1);
        TestAssert.Equal(ulong.MaxValue - 1, pending.MenuRevision!.Value);

        rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw = rig.Controller.Snapshot();
        TestAssert.Equal(ulong.MaxValue, redraw.MenuRevision!.Value);
        TestAssert.Equal("cooldown", redraw.MenuOptionCallback!.Phase);
        TestAssert.Equal(receipt, redraw.MenuUseContinuation);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var ready = rig.Controller.Snapshot();
        TestAssert.True(ready.MenuOptionCallback is null, "A rejected counter becomes ready after its reserved single redraw epoch");
        TestAssert.Equal(receipt, ready.MenuUseContinuation);
    }

    public static void RejectedBooleanReceiptIsWithdrawnByUnknownOwnerReplacementFocusLossAndShutdown() {
        foreach (string change in new[] { "unknown", "owner", "focus", "shutdown" }) {
            var rig = ReadyStealthCounter(0, out _);
            rig.Send(OverlayInput.Select);
            var started = rig.Controller.Snapshot();
            AssertRejectedBoolean(started, started.LastSequence);

            if (change == "unknown") rig.Session.AdmissionUnavailable = true;
            if (change == "owner") rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
            if (change == "focus") rig.Focus = false;
            if (change == "shutdown") rig.Controller.RequestShutdown();

            var withdrawn = rig.Controller.Snapshot();
            TestAssert.True(withdrawn.MenuUseContinuation is null,
                "A rejected callback cannot lend its ready receipt across " + change);
            if (change is "unknown" or "owner" or "shutdown") {
                TestAssert.True(withdrawn.MenuOptionCallback is null,
                    "A rejected callback cannot outlive " + change);
            }
            if (change == "focus") rig.Focus = true;
            if (change == "unknown") rig.Session.AdmissionUnavailable = false;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
            TestAssert.True(rig.Controller.Snapshot().MenuUseContinuation is null,
                "Expiry cannot restore the canceled held-Use receipt after " + change);
            TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void StealthFeedbackTakesPriorityWhenDesignAlsoRejectsTheCounter() {
        var rig = ReadyStealthCounter(0, out _, design: "AIO", stealth: true);
        rig.Send(OverlayInput.Select);
        var rejected = rig.Controller.Snapshot();
        TestAssert.True(rejected.Notice.Contains("You Can't Use The Option Counter While Stealth UI Is Enabled", StringComparison.Ordinal),
            "Stealth is the first source guard when both source restrictions apply");
        TestAssert.True(!rejected.Notice.Contains("You Can't Use The Option Counter With This Design", StringComparison.Ordinal),
            "The later design guard does not replace the earlier Stealth reason");
        TestAssert.True(rejected.MenuOptionCallback is not null && rejected.MenuUseContinuation is not null,
            "The priority rejection remains a normal connected source BOOL callback");
    }

    private static void AssertRejectedPreflight(string failure) {
        LocalOptionCallbackControllerTests.SwitchableClock? switchable = failure == "clock"
            ? new LocalOptionCallbackControllerTests.SwitchableClock() : null;
        var rig = ReadyStealthCounter(0, out var scope, clock: switchable);
        var before = rig.Controller.Snapshot();
        CommittedMenuPreferences storedBefore = ReadStored(scope);
        if (failure == "menu") SetMenuRevision(rig, ulong.MaxValue);
        if (failure == "callback") typeof(BackendController).GetField("optionCallbackRevision",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(rig.Controller, ulong.MaxValue);
        if (failure == "clock") switchable!.Fail = true;

        if (failure == "clock") TestAssert.Throws<IOException>(() => rig.Send(OverlayInput.Select));
        else TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        if (switchable is not null) switchable.Fail = false;

        var after = rig.Controller.Snapshot();
        TestAssert.Equal(before.Appearance, after.Appearance);
        TestAssert.Equal(before.Notice, after.Notice);
        TestAssert.Equal(before.Rows.Single(row => row.Label == "Option Counter").Value,
            after.Rows.Single(row => row.Label == "Option Counter").Value);
        TestAssert.True(after.MenuOptionCallback is null && after.MenuUseContinuation is null,
            "A rejected BOOL preflight fault commits no callback or receipt");
        TestAssert.True(ReferenceEquals(storedBefore, ReadStored(scope)),
            "A rejected BOOL preflight fault cannot replace the immutable saved record");
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    private static ScopedOpeningFixture ReadyStealthCounter(ulong paintRevision, out LocalMenuPreferenceScope scope,
        TimeProvider? clock = null, string? design = null, bool stealth = true) {
        scope = new LocalMenuPreferenceScope((ulong)Interlocked.Increment(ref nextAccount), new object());
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, paintRevision, clock, scope);
        if (design is not null) SelectDesign(rig, design);
        if (stealth) {
            LocalOptionCallbackControllerTests.SelectIndex(rig, 10);
            rig.Send(OverlayInput.Select);
            Advance(rig, clock, 430);
        }
        LocalOptionCallbackControllerTests.SelectIndex(rig, 9);
        TestAssert.True(rig.Controller.Snapshot().Rows.Single(row => row.Label == "Option Counter").Enabled,
            "The source counter remains selectable before its in-handler rejection");
        return rig;
    }

    private static void Advance(ScopedOpeningFixture rig, TimeProvider? clock, int milliseconds) {
        if (clock is LocalOptionCallbackControllerTests.SwitchableClock external) external.Advance(milliseconds);
        else rig.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        rig.Controller.Snapshot();
    }

    private static void SetMenuRevision(ScopedOpeningFixture rig, ulong revision) =>
        typeof(OverlayNavigation).GetProperty(nameof(OverlayNavigation.MenuRevision))!
            .GetSetMethod(nonPublic: true)!.Invoke(rig.Navigation, [revision]);

    private static CommittedMenuPreferences ReadStored(LocalMenuPreferenceScope scope) {
        var store = (ICommittedMenuPreferencesStore)typeof(BackendController).GetField("processPreferenceStore",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        TestAssert.True(store.TryRead(scope, out var stored) && stored is not null,
            "The connected preference scope has a committed immutable record");
        return stored!;
    }
}
