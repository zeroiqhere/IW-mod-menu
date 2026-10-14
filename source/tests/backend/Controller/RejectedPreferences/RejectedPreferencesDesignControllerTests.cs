using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static partial class RejectedPreferencesControllerTests {
    public static void AioDesignKeepsOptionCounterSelectableAndReportsTheSourceRejection() =>
        AssertDesignRejection("AIO", "Option Counter", "You Can't Use The Option Counter With This Design");

    public static void BasicDesignKeepsOptionCounterSelectableAndReportsTheSourceRejection() =>
        AssertDesignRejection("Basic", "Option Counter", "You Can't Use The Option Counter With This Design");

    public static void ClassicDesignKeepsColoredCursorSelectableAndReportsTheSourceRejection() =>
        AssertDesignRejection("Classic", "Colored Cursor", "You Can't Use Colored Cursor With This Design");

    public static void NativeDesignKeepsColoredCursorSelectableAndReportsTheSourceRejection() =>
        AssertDesignRejection("Native", "Colored Cursor", "You Can't Use Colored Cursor With This Design");

    private static void AssertDesignRejection(string design, string label, string sourceReason) {
        var scope = new LocalMenuPreferenceScope((ulong)Interlocked.Increment(ref nextAccount), new object());
        var rig = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences, preferenceScope: scope);
        SelectDesign(rig, design);
        int index = label == "Option Counter" ? 9 : 7;
        LocalOptionCallbackControllerTests.SelectIndex(rig, index);
        var before = rig.Controller.Snapshot();
        var row = before.Rows.Single(item => item.Label == label);
        TestAssert.True(row.Enabled, "The source BOOL stays selectable under the rejected design");
        var persistedBefore = ReloadAppearance(rig);

        rig.Send(OverlayInput.Select);
        var rejected = rig.Controller.Snapshot();
        TestAssert.Equal(before.Appearance, rejected.Appearance);
        TestAssert.Equal(row.Value, rejected.Rows.Single(item => item.Label == label).Value);
        TestAssert.True(rejected.Notice.Contains(sourceReason, StringComparison.Ordinal),
            "The rejected BOOL reports the exact source design rule");
        TestAssert.True(rejected.MenuOptionCallback is not null && rejected.MenuUseContinuation is not null,
            "The rejected source BOOL still owns its callback and Use receipt");
        TestAssert.Equal(persistedBefore, ReloadAppearance(rig));
        TestAssert.Equal(0, rig.Session.Executed.Count);
    }

    private static void SelectDesign(ScopedOpeningFixture rig, string design) {
        int target = Array.IndexOf(new[] { "Apparition", "Classic", "Native", "AIO", "Basic" }, design);
        TestAssert.True(target > 0, "The tested source design is supported");
        LocalOptionCallbackControllerTests.SelectIndex(rig, 0);
        for (int step = 0; step < target; step++) rig.StageChoice(OverlayInput.NextChoice);
        TestAssert.Equal(design, rig.Controller.Snapshot().Rows[0].Value);
        rig.Send(OverlayInput.Select);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        TestAssert.Equal(design, rig.Controller.Snapshot().Appearance!.Design);
    }
}
