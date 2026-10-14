using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Real immutable policies drive these model checks without a session or input worker
internal static class QuickOpeningControlsViewTests {
    public static void AppliedQuickTargetShowsOnlyItsSelectedSlotAndIndependentMainSummary() {
        var policies = QuickOpeningTestSupport.Policies();
        var view = View(policies, Target(OpeningControlTarget.QuickMenu, 2));

        var rows = OpeningControlsMenuRows.Build(view);
        var summary = OpeningControlsMenuRows.AppliedSummary(view);

        TestAssert.Equal("ON", Row(rows, "Secondary Offhand").Value);
        TestAssert.Equal("OFF", Row(rows, "Melee").Value);
        TestAssert.Equal("OFF", Row(rows, "Aim").Value);
        TestAssert.True(Row(rows, "Aim").Enabled, "An action assigned elsewhere remains selectable for source duplicate feedback");
        TestAssert.True(summary.Contains("Applied main chord: Aim + Melee", StringComparison.Ordinal), "Main readback remains independent of the edited family");
        TestAssert.True(summary.Contains("Configured Quick chord: Aim + Secondary Offhand", StringComparison.Ordinal), "Quick readback identifies its actual independent tuple");
        TestAssert.True(summary.Contains("Quick opening is unavailable", StringComparison.Ordinal), "A configured tuple cannot promise working Quick opening");
    }

    public static void PendingQuickCheckboxesUseCapturedPoliciesWhileSummaryUsesActualPolicies() {
        var previous = QuickOpeningTestSupport.Policies();
        TestAssert.True(previous.TrySetQuickSlot(previous.Revision, 2, OpeningAction.Use,
            QuickOpeningTestSupport.AllCapabilities(), out var current, out _), "The real policy edit must succeed");
        // The previous owner represents checkbox paint before the source refresh
        var view = View(current, Target(OpeningControlTarget.QuickMenu, 2), displayed: previous);

        var rows = OpeningControlsMenuRows.Build(view);
        var summary = OpeningControlsMenuRows.AppliedSummary(view);

        TestAssert.Equal("ON", Row(rows, "Secondary Offhand").Value);
        TestAssert.Equal("OFF", Row(rows, "Use").Value);
        TestAssert.True(summary.Contains("Configured Quick chord: Aim + Use", StringComparison.Ordinal), "The summary must disclose the already applied input configuration");
        TestAssert.True(summary.Contains("Checkboxes refresh", StringComparison.Ordinal), "Captured row values need explicit delayed-readback guidance");
        TestAssert.True(summary.Contains("Editor applied: Quick Menu; slot: 2", StringComparison.Ordinal),
            "The applied editor target and slot remain readable when cooldown suppresses row details");
        TestAssert.True(ReferenceEquals(previous.Main, current.Main), "The Quick edit cannot replace Main");
    }

    public static void StagedMenuTargetDoesNotChangeAppliedCheckboxFamily() {
        var policies = QuickOpeningTestSupport.Policies();
        var applied = Target(OpeningControlTarget.Main, 2);
        // Staging another family and slot must not select its tuple yet
        var candidate = Target(OpeningControlTarget.QuickMenu, 3);
        var view = View(policies, applied, candidate);

        var rows = OpeningControlsMenuRows.Build(view);

        TestAssert.Equal("Quick Menu", rows[0].Value);
        TestAssert.Equal("3", rows[1].Value);
        TestAssert.Equal("ON", Row(rows, "Melee").Value);
        TestAssert.Equal("OFF", Row(rows, "Secondary Offhand").Value);
        TestAssert.True(rows[0].Detail.Contains("applied: Apparition", StringComparison.Ordinal), "Menu help distinguishes the staged target from the applied family");
        TestAssert.True(rows[1].Detail.Contains("applied slot: 2", StringComparison.Ordinal), "Bind Slot help distinguishes candidate and actual slots");
        var summary = OpeningControlsMenuRows.AppliedSummary(view);
        TestAssert.True(summary.Contains("Editor applied: Apparition; slot: 2", StringComparison.Ordinal),
            "The summary must identify the applied selector rather than its staged candidate");
        TestAssert.True(!summary.Contains("Editor applied: Quick Menu", StringComparison.Ordinal),
            "A staged Quick target cannot appear as already applied");
    }

    public static void QuickAppliedHelpAndNoneReadbackUseTheSelectedQuickSlot() {
        var policies = QuickOpeningTestSupport.Policies();
        var view = View(policies, Target(OpeningControlTarget.QuickMenu, 3));

        var rows = OpeningControlsMenuRows.Build(view);

        TestAssert.True(rows[0].Detail.Contains("applied: Quick Menu", StringComparison.Ordinal), "Applied Quick is a real editor target");
        TestAssert.True(!rows[0].Detail.Contains("not implemented", StringComparison.Ordinal), "Configuration help cannot reject the supported settings target");
        TestAssert.Equal("ON", Row(rows, "None").Value);
        TestAssert.True(rows.Where(row => row.OpeningAction is not null).All(row => row.Value == "OFF"), "An empty selected slot has no checked action");
        var firstSlot = OpeningControlsMenuRows.Build(View(policies, Target(OpeningControlTarget.QuickMenu, 1)));
        TestAssert.True(firstSlot.All(row => row.Label != "None"), "The required first slot never advertises None");
    }

    public static void VerifiedActionAvailabilityAndTypedSpacerRemainTruthfulInQuickEditor() {
        var policies = QuickOpeningTestSupport.Policies();
        // The two default tuples remain verified while optional Frag is absent
        var capabilities = QuickOpeningTestSupport.Capabilities(OpeningAction.Aim,
            OpeningAction.Melee, OpeningAction.SecondaryOffhand, OpeningAction.Use);
        var view = View(policies, Target(OpeningControlTarget.QuickMenu, 2)) with { Capabilities = capabilities };

        var rows = OpeningControlsMenuRows.Build(view);

        TestAssert.Equal("ON", Row(rows, "Secondary Offhand").Value);
        TestAssert.Equal("UNAVAILABLE", Row(rows, "Frag").Value);
        TestAssert.True(!Row(rows, "Frag").Enabled, "Unverified physical action groups cannot become selectable");
        TestAssert.Equal(MenuActionKind.ChangeMainOpeningAction, Row(rows, "Secondary Offhand").Action.Kind);
        TestAssert.Equal((int)OpeningAction.SecondaryOffhand, Row(rows, "Secondary Offhand").Action.Slot);
        TestAssert.True(rows[2].IsSpacer && rows[2].Label == "" && rows[2].Value == "" && !rows[2].Enabled,
            "The source blank remains distinct from an unavailable action");
        TestAssert.Equal(MenuActionKind.None, rows[2].Action.Kind);
    }

    internal static OpeningControlsView View(QuickOpeningPolicies policies, OpeningControlDraft applied,
        OpeningControlDraft? candidate = null, QuickOpeningPolicies? displayed = null) =>
        new(policies.Main, (displayed ?? policies).Main, applied, candidate ?? applied,
            QuickOpeningTestSupport.AllCapabilities(), true, policies, displayed ?? policies);

    internal static OpeningControlDraft Target(OpeningControlTarget target, int slot) {
        TestAssert.True(OpeningControlDraft.Default.TryStageTarget(target, out var draft, out _), "The target draft must be supported");
        TestAssert.True(draft.TryStageSlot(slot, out var result, out _), "The slot draft must be supported");
        return result;
    }

    internal static MenuRow Row(IReadOnlyList<MenuRow> rows, string label) => rows.Single(row => row.Label == label);
}
