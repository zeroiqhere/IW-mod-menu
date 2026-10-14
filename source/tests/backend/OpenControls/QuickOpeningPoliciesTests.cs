using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class QuickOpeningPoliciesTests {
    public static void DefaultCreatesIndependentSourceTuplesWithOneAdmissionEpoch() {
        TestAssert.True(QuickOpeningPolicies.TryCreateDefault(QuickOpeningTestSupport.AllCapabilities(),
            out var policies, out var failure), "Both verified source defaults must be created");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        TestAssert.Equal(1UL, policies!.Revision);
        TestAssert.Equal(1UL, policies.Main.Revision);
        TestAssert.Equal(1UL, policies.Quick.Revision);
        TestAssert.True(!policies.QuickDisabled, "Quick is not disabled by the default local preference");
        QuickOpeningTestSupport.Actions(policies.Main.Actions, OpeningAction.Aim, OpeningAction.Melee);
        QuickOpeningTestSupport.Actions(policies.Quick.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand);

        var mainOnly = QuickOpeningTestSupport.Capabilities(OpeningAction.Aim, OpeningAction.Melee);
        TestAssert.True(!QuickOpeningPolicies.TryCreateDefault(mainOnly, out var missing, out failure),
            "The dual owner cannot fabricate an unverified Quick tuple");
        TestAssert.True(missing is null, "Failed creation cannot expose a partial owner");
        TestAssert.Equal(OpeningControlFailure.UnavailableAction, failure);
        TestAssert.True(!QuickOpeningPolicies.TryCreate(policies.Main, policies.Quick, 0, false, out _, out failure),
            "The global epoch must be positive");
        TestAssert.Equal(OpeningControlFailure.InvalidRevision, failure);
        TestAssert.True(!QuickOpeningPolicies.TryCreate(null, policies.Quick, 1, false, out _, out failure),
            "An absent Main owner cannot form a pair");
        TestAssert.Equal(OpeningControlFailure.InvalidChord, failure);
        TestAssert.True(!QuickOpeningPolicies.TryCreate(policies.Main, null, 1, false, out _, out failure),
            "An absent Quick owner cannot form a pair");
        TestAssert.Equal(OpeningControlFailure.InvalidChord, failure);
    }

    public static void EditingEitherTupleAdvancesOnlyItsRevisionAndTheGlobalEpoch() {
        var initial = QuickOpeningTestSupport.Policies();
        var capabilities = QuickOpeningTestSupport.AllCapabilities();
        TestAssert.True(initial.TrySetMainSlot(1, 3, OpeningAction.Use, capabilities, out var mainChanged, out _),
            "An admitted Main mutation commits");
        TestAssert.Equal(2UL, mainChanged.Revision);
        TestAssert.Equal(2UL, mainChanged.Main.Revision);
        TestAssert.True(ReferenceEquals(initial.Quick, mainChanged.Quick), "Main edits retain the exact Quick owner");

        TestAssert.True(mainChanged.TrySetQuickSlot(2, 3, OpeningAction.Jump, capabilities, out var quickChanged, out _),
            "An admitted Quick mutation commits");
        TestAssert.Equal(3UL, quickChanged.Revision);
        TestAssert.Equal(2UL, quickChanged.Quick.Revision);
        TestAssert.True(ReferenceEquals(mainChanged.Main, quickChanged.Main), "Quick edits retain the exact Main owner");
        QuickOpeningTestSupport.Actions(initial.Main.Actions, OpeningAction.Aim, OpeningAction.Melee);
        QuickOpeningTestSupport.Actions(initial.Quick.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand);

        // A completion from either old family cannot edit the other family's newer policy
        TestAssert.True(!quickChanged.TrySetMainSlot(2, 3, OpeningAction.Stance, capabilities, out var rejected, out var failure),
            "The global admission epoch guards both families");
        TestAssert.True(ReferenceEquals(quickChanged, rejected), "A stale request retains the whole pair");
        TestAssert.Equal(OpeningControlFailure.StaleRevision, failure);
        TestAssert.True(!quickChanged.TrySetQuickSlot(1, 3, OpeningAction.Stance, capabilities, out rejected, out failure),
            "A stale Quick request also rejects");
        TestAssert.True(ReferenceEquals(quickChanged, rejected), "Neither family can partially commit");
        TestAssert.Equal(OpeningControlFailure.StaleRevision, failure);
    }

    public static void CrossFamilyOverlapIsAllowedButWithinTupleConflictsReject() {
        var initial = QuickOpeningTestSupport.Policies();
        var capabilities = QuickOpeningTestSupport.AllCapabilities();
        TestAssert.True(initial.TrySetMainSlot(1, 2, OpeningAction.Frag, capabilities, out var changed, out _),
            "Frag in Main may coexist with SecondaryOffhand in Quick");
        TestAssert.True(changed.TrySetQuickSlot(2, 2, OpeningAction.Frag, capabilities, out var shared, out _),
            "The same complete tuple may be configured independently in both families");
        QuickOpeningTestSupport.Actions(shared.Main.Actions, OpeningAction.Aim, OpeningAction.Frag);
        QuickOpeningTestSupport.Actions(shared.Quick.Actions, OpeningAction.Aim, OpeningAction.Frag);

        TestAssert.True(!shared.TrySetQuickSlot(3, 3, OpeningAction.SecondaryOffhand, capabilities, out var rejected, out var failure),
            "Offhand conflict still rejects inside Quick");
        TestAssert.True(ReferenceEquals(shared, rejected), "A conflict retains both families");
        TestAssert.Equal(OpeningControlFailure.ConflictingOffhands, failure);
        TestAssert.True(!shared.TrySetMainSlot(3, 3, OpeningAction.Aim, capabilities, out rejected, out failure),
            "Main duplicate rules remain unchanged");
        TestAssert.True(ReferenceEquals(shared, rejected), "A Main duplicate cannot affect Quick");
        TestAssert.Equal(OpeningControlFailure.DuplicateAction, failure);

        // A failed capability check cannot publish even a valid-looking changed tuple
        var partial = QuickOpeningTestSupport.Capabilities(OpeningAction.Aim, OpeningAction.Frag);
        TestAssert.True(!shared.TrySetQuickSlot(3, 3, OpeningAction.Use, partial, out rejected, out failure),
            "Quick changes still require exact verified action evidence");
        TestAssert.True(ReferenceEquals(shared, rejected), "Capability failure retains the entire paired owner");
        TestAssert.Equal(OpeningControlFailure.UnavailableAction, failure);
    }

    public static void DisablingQuickKeepsItsConfiguredTupleAndRejectsStaleAdmission() {
        var initial = QuickOpeningTestSupport.Policies();
        TestAssert.True(initial.TrySetQuickDisabled(1, true, out var disabled, out var failure),
            "The local disable preference commits separately from configuration");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        TestAssert.Equal(2UL, disabled.Revision);
        TestAssert.True(disabled.QuickDisabled, "The new owner records the disable preference");
        TestAssert.True(ReferenceEquals(initial.Main, disabled.Main), "Disabling does not replace Main");
        TestAssert.True(ReferenceEquals(initial.Quick, disabled.Quick), "Disabling does not discard Quick configuration");
        TestAssert.True(disabled.TrySetQuickDisabled(2, true, out var same, out _), "Reselecting the preference is a no-op");
        TestAssert.True(ReferenceEquals(disabled, same), "Preference no-op keeps the admission epoch");
        TestAssert.True(!disabled.TrySetQuickDisabled(1, false, out same, out failure), "Stale preference edits reject");
        TestAssert.True(ReferenceEquals(disabled, same), "Stale edits retain the disable preference");
        TestAssert.Equal(OpeningControlFailure.StaleRevision, failure);

        TestAssert.True(disabled.TrySetQuickSlot(2, 3, OpeningAction.Use,
            QuickOpeningTestSupport.AllCapabilities(), out var configured, out _),
            "Disabled Quick can still be configured without being enabled");
        TestAssert.True(configured.QuickDisabled, "Configuration never implicitly enables Quick");
        TestAssert.True(configured.TrySetQuickDisabled(3, false, out var enabled, out _), "Reenable changes only preference");
        TestAssert.True(ReferenceEquals(configured.Quick, enabled.Quick), "Reenable retains the configured tuple");
        TestAssert.True(!enabled.QuickDisabled, "The new preference is enabled");
    }

    public static void GlobalRevisionExhaustionCannotPartiallyReplaceEitherTupleOrPreference() {
        var capabilities = QuickOpeningTestSupport.AllCapabilities();
        var penultimate = QuickOpeningTestSupport.Policies(ulong.MaxValue - 1);
        TestAssert.True(penultimate.TrySetQuickSlot(ulong.MaxValue - 1, 3, OpeningAction.Use,
            capabilities, out var last, out var failure), "The last positive global epoch can commit");
        TestAssert.Equal(ulong.MaxValue, last.Revision);
        TestAssert.Equal(2UL, last.Quick.Revision);
        TestAssert.True(ReferenceEquals(penultimate.Main, last.Main), "The last commit still retains Main");
        TestAssert.True(!last.TrySetMainSlot(ulong.MaxValue, 3, OpeningAction.Use, capabilities,
            out var rejected, out failure), "The following global change must reject");
        AssertExhausted(last, rejected, failure);

        var initial = QuickOpeningTestSupport.Policies(ulong.MaxValue);
        TestAssert.True(!initial.TrySetMainSlot(ulong.MaxValue, 3, OpeningAction.Use, capabilities,
            out rejected, out failure), "A Main change cannot exceed the global epoch");
        AssertExhausted(initial, rejected, failure);
        TestAssert.True(!initial.TrySetQuickSlot(ulong.MaxValue, 3, OpeningAction.Use, capabilities,
            out rejected, out failure), "A Quick change cannot exceed the global epoch");
        AssertExhausted(initial, rejected, failure);
        TestAssert.True(!initial.TrySetQuickDisabled(ulong.MaxValue, true, out rejected, out failure),
            "The disable preference cannot exceed the global epoch");
        AssertExhausted(initial, rejected, failure);

        TestAssert.True(initial.TrySetMainSlot(ulong.MaxValue, 3, null, capabilities, out var same, out _),
            "An unchanged Main tuple does not need a new epoch");
        TestAssert.True(ReferenceEquals(initial, same), "The Main no-op keeps the paired owner");
        TestAssert.True(initial.TrySetQuickSlot(ulong.MaxValue, 3, null, capabilities, out same, out _),
            "An unchanged Quick tuple does not need a new epoch");
        TestAssert.True(ReferenceEquals(initial, same), "The Quick no-op keeps the paired owner");
        TestAssert.True(initial.TrySetQuickDisabled(ulong.MaxValue, false, out same, out _),
            "An unchanged preference does not need a new epoch");
        TestAssert.True(ReferenceEquals(initial, same), "The preference no-op keeps the paired owner");

        // Exhausting a family revision also rejects before the global owner is replaced
        var lastQuick = QuickOpeningTestSupport.Quick(ulong.MaxValue, OpeningAction.Aim);
        TestAssert.True(QuickOpeningPolicies.TryCreate(initial.Main, lastQuick, 4, false, out var tupleExhausted, out _),
            "Independent tuple revisions may differ from the global admission epoch");
        TestAssert.True(!tupleExhausted!.TrySetQuickSlot(4, 2, OpeningAction.Use, capabilities,
            out rejected, out failure), "An exhausted tuple cannot change under a fresh global epoch");
        AssertExhausted(tupleExhausted, rejected, failure);
    }

    private static void AssertExhausted(QuickOpeningPolicies original, QuickOpeningPolicies result,
        OpeningControlFailure failure) {
        TestAssert.Equal(OpeningControlFailure.RevisionExhausted, failure);
        TestAssert.True(ReferenceEquals(original, result), "Revision failure retains the complete owner");
        TestAssert.Equal(original.Main.Revision, result.Main.Revision);
        TestAssert.Equal(original.Quick.Revision, result.Quick.Revision);
        TestAssert.Equal(original.QuickDisabled, result.QuickDisabled);
    }
}
