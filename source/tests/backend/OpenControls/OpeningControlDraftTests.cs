using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class OpeningControlDraftTests {
    public static void SelectorDraftsRemainIndependentAndQuickMenuCannotCommitMainPolicy() {
        var chord = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee);
        var initial = OpeningControlDraft.Default;
        TestAssert.Equal(1, initial.Slot);
        TestAssert.Equal(OpeningControlTarget.Main, initial.Target);
        TestAssert.True(initial.TryStageSlot(3, out var slotDraft, out _), "A valid slot candidate must stage");
        TestAssert.True(slotDraft.TryStageTarget(OpeningControlTarget.QuickMenu, out var quickDraft, out _), "The source target can be represented without enabling its mode");
        TestAssert.Equal(3, quickDraft.Slot);
        TestAssert.Equal(OpeningControlTarget.QuickMenu, quickDraft.Target);
        TestAssert.Equal(1, initial.Slot);
        TestAssert.Equal(OpeningControlTarget.Main, initial.Target);
        TestAssert.True(!quickDraft.TryApply(chord, chord.Revision, OpeningAction.Use, MainOpeningChordTests.AllActions(), out var rejected, out var failure), "Quick Menu has no applied policy in this core");
        TestAssert.Equal(OpeningControlFailure.UnsupportedTarget, failure);
        TestAssert.True(ReferenceEquals(chord, rejected), "An unsupported target cannot change the main chord");
        TestAssert.True(quickDraft.TryStageTarget(OpeningControlTarget.Main, out var mainDraft, out _), "Returning to the main target retains the staged slot");
        TestAssert.True(mainDraft.TryApply(chord, chord.Revision, OpeningAction.Use, MainOpeningChordTests.AllActions(), out var updated, out failure), "Only an explicit main action application can commit");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        MainOpeningChordTests.EqualActions(updated, OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use);
        MainOpeningChordTests.EqualActions(chord, OpeningAction.Aim, OpeningAction.Melee);
    }

    public static void InvalidSelectorsRejectWithoutChangingEitherDraftOrAppliedChord() {
        var initial = OpeningControlDraft.Default;
        foreach (int slot in new[] { int.MinValue, 0, 4, int.MaxValue }) {
            TestAssert.True(!initial.TryStageSlot(slot, out var rejected, out var failure), "Out-of-range slots must reject");
            TestAssert.True(ReferenceEquals(initial, rejected), "A failed stage must return the original draft");
            TestAssert.Equal(OpeningControlFailure.InvalidSlot, failure);
        }
        foreach (var target in new[] { (OpeningControlTarget)(-1), (OpeningControlTarget)2 }) {
            TestAssert.True(!initial.TryStageTarget(target, out var rejected, out var failure), "Unknown targets must reject");
            TestAssert.True(ReferenceEquals(initial, rejected), "An unknown target cannot create a partial draft");
            TestAssert.Equal(OpeningControlFailure.UnknownTarget, failure);
        }
        var chord = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee);
        TestAssert.True(!initial.TryApply(chord, 2, OpeningAction.Use, MainOpeningChordTests.AllActions(), out var unchanged, out var reason), "A selector cannot bypass policy revision admission");
        TestAssert.True(ReferenceEquals(chord, unchanged), "A stale selector application must keep the applied owner");
        TestAssert.Equal(OpeningControlFailure.StaleRevision, reason);
        // Reselecting a draft is harmless; reselecting an applied action is a separate rejection
        TestAssert.True(initial.TryStageSlot(1, out var same, out _), "An unchanged slot candidate is valid");
        TestAssert.True(ReferenceEquals(initial, same), "An unchanged draft needs no replacement");
    }
}
