using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class RejectedPreferencesLegacyControllerTests {
    public static void ActorlessStealthCounterRejectionKeepsLegacyRefreshWithoutTypedUseReceipt() {
        var rig = new MenuPreferenceRefreshTests.Rig();
        rig.Open();
        rig.Select("Stealth UI");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        var stealthReady = rig.Current();
        TestAssert.True(stealthReady.Appearance!.StealthUI, "The actorless setup must reach the source Stealth state");

        rig.Highlight("Option Counter");
        var before = rig.Current();
        var row = before.Rows.Single(item => item.Label == "Option Counter");
        TestAssert.True(row.Enabled, "The source BOOL stays selectable in an actorless preview");
        rig.Send(OverlayInput.Select);
        var pending = rig.Current();
        TestAssert.Equal(before.Appearance, pending.Appearance);
        TestAssert.Equal(row.Value, pending.Rows.Single(item => item.Label == "Option Counter").Value);
        TestAssert.True(pending.Notice.Contains("You Can't Use The Option Counter While Stealth UI Is Enabled", StringComparison.Ordinal),
            "The actorless rejection retains source feedback");
        TestAssert.Equal("pending", MenuPreferenceRefreshTests.Phase(pending));
        TestAssert.True(pending.MenuOptionCallback is null && pending.MenuUseContinuation is null,
            "Actorless input keeps the legacy refresh and cannot claim a typed owner receipt");

        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        TestAssert.Equal("pending", MenuPreferenceRefreshTests.Phase(rig.Current()));
        TestAssert.Equal(before.MenuRevision, rig.Current().MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var redraw180 = rig.Current();
        TestAssert.Equal("cooldown", MenuPreferenceRefreshTests.Phase(redraw180));
        TestAssert.Equal(before.MenuRevision!.Value + 1, redraw180.MenuRevision!.Value);
        TestAssert.Equal(before.Appearance, redraw180.Appearance);
        TestAssert.Equal(row.Value, redraw180.Rows.Single(item => item.Label == "Option Counter").Value);
        TestAssert.True(redraw180.MenuOptionCallback is null && redraw180.MenuUseContinuation is null,
            "The legacy redraw remains outside typed callback metadata");

        rig.Clock.Advance(TimeSpan.FromMilliseconds(249));
        TestAssert.Equal("cooldown", MenuPreferenceRefreshTests.Phase(rig.Current()));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var ready430 = rig.Current();
        TestAssert.True(MenuPreferenceRefreshTests.Phase(ready430) is null, "The legacy source refresh expires at430ms");
        TestAssert.Equal(before.Appearance, ready430.Appearance);
        TestAssert.True(ready430.MenuOptionCallback is null && ready430.MenuUseContinuation is null,
            "Expiry does not synthesize a typed callback or held-Use receipt");
        rig.NoGameWork();
    }
}
