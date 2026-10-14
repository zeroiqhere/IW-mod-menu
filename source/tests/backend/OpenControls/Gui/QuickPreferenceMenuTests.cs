using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Disable readback belongs to the paired policy owner rather than appearance geometry
internal static class QuickPreferenceMenuTests {
    public static void DisableQuickPreferenceShowsCapturedValueAndActualOpeningAvailabilityGuidance() {
        var previous = QuickOpeningTestSupport.Policies();
        TestAssert.True(previous.TrySetQuickDisabled(previous.Revision, true, out var current, out _), "The local policy toggle must succeed");
        var view = QuickOpeningControlsViewTests.View(current, OpeningControlDraft.Default, displayed: previous);

        // The display remains OFF while the actual owner is already disabled
        var pending = Build(view);
        var pendingRow = QuickOpeningControlsViewTests.Row(pending.Rows, "Disable Quick Menu");
        var refreshed = Build(view with { DisplayPolicies = current });
        var refreshedRow = QuickOpeningControlsViewTests.Row(refreshed.Rows, "Disable Quick Menu");

        TestAssert.True(pendingRow.Enabled, "Verified paired configuration permits a real local disable toggle");
        TestAssert.Equal("OFF", pendingRow.Value);
        TestAssert.Equal(MenuActionKind.ChangePreference, pendingRow.Action.Kind);
        TestAssert.Equal((int)MenuPreference.DisableQuickMenu, pendingRow.Action.Slot);
        TestAssert.Equal("ON", refreshedRow.Value);
        TestAssert.True(pendingRow.Detail.Contains("opening is unavailable", StringComparison.Ordinal), "OFF cannot imply verified runtime eligibility");
        TestAssert.True(ReferenceEquals(current.Quick, previous.Quick), "Disabling cannot erase the configured Quick tuple");
    }

    public static void MissingPairedPolicyKeepsLegacyMainRowsAndDisableQuickUnavailable() {
        var policies = QuickOpeningTestSupport.Policies();
        // The old constructor cannot manufacture a paired Quick policy
        var legacy = new OpeningControlsView(policies.Main, policies.Main, OpeningControlDraft.Default,
            QuickOpeningControlsViewTests.Target(OpeningControlTarget.QuickMenu, 1),
            QuickOpeningTestSupport.AllCapabilities(), true);

        var rows = OpeningControlsMenuRows.Build(legacy);
        var preference = QuickOpeningControlsViewTests.Row(Build(legacy).Rows, "Disable Quick Menu");

        TestAssert.Equal("ON", QuickOpeningControlsViewTests.Row(rows, "Aim").Value);
        TestAssert.True(rows[0].Detail.Contains("Quick", StringComparison.Ordinal), "Unverified Quick configuration needs clear help");
        TestAssert.True(!preference.Enabled && preference.Value == "UNAVAILABLE", "Main-only fallback cannot fabricate a Quick configuration");
        TestAssert.True(!OpeningControlsMenuRows.AppliedSummary(legacy).Contains("Configured Quick chord", StringComparison.Ordinal), "Fallback cannot describe an unproved Quick tuple");
    }

    public static void PreviewAndBusyStatesKeepLocalQuickConfigurationAvailableWithoutGameCapabilities() {
        var view = QuickOpeningControlsViewTests.View(QuickOpeningTestSupport.Policies(), OpeningControlDraft.Default)
            with { SelectorControlsAvailable = false };

        // A local boolean needs no selector chord or connected game capability
        var snapshot = Build(view, preview: true, busy: true);
        var disable = QuickOpeningControlsViewTests.Row(snapshot.Rows, "Disable Quick Menu");
        var editor = OverlayMenuBuilder.Build(MenuPage.OpenControls, State(true, true), openingControls: view);

        TestAssert.True(disable.Enabled && disable.Value == "OFF", "A local boolean does not require slider bindings or a connected game");
        TestAssert.True(editor.Rows.Where(row => !row.IsSpacer).All(row => !row.Enabled), "Missing choice controls still disable editor selection");
        TestAssert.True(snapshot.Context.Contains("PREVIEW", StringComparison.Ordinal), "The preview context remains truthful");
        TestAssert.True(editor.Notice.Contains("Quick opening is unavailable", StringComparison.Ordinal), "Busy preview cannot promise a working Quick route");
    }

    public static void DisableQuickPreferenceCannotMutateAppearanceOrRenumberExistingPreferences() {
        var original = MenuAppearance.Default;

        TestAssert.Equal(13, (int)MenuPreference.StealthUI);
        TestAssert.Equal(14, (int)MenuPreference.DisableQuickMenu);
        TestAssert.True(!original.TryChange((int)MenuPreference.DisableQuickMenu, out var unchanged),
            "Paired configuration must not pass through the appearance toggle route");
        TestAssert.True(ReferenceEquals(original, unchanged), "A rejected route must retain the original appearance owner");
    }

    private static MenuSnapshot Build(OpeningControlsView? view, bool preview = false, bool busy = false) =>
        OverlayMenuBuilder.Build(MenuPage.Preferences, State(preview, busy), openingControls: view);

    private static OverlayMenuState State(bool preview, bool busy) =>
        new(preview, false, busy, false, false, "UNKNOWN", "", false, "UNKNOWN", "", false, false,
            Array.Empty<OverlayPlayer>(), -1, null, false, "UNKNOWN", "", false, "");
}
