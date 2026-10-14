using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class MainOpeningChordMutationTests {
    public static void OneTwoAndThreeActionsCommitWithCheckedRevisionsAndPreserveOlderOwners() {
        var capabilities = MainOpeningChordTests.AllActions();
        var initial = MainOpeningChordTests.Create(OpeningAction.Aim);
        var two = Commit(initial, 2, OpeningAction.Melee, capabilities);
        var three = Commit(two, 3, OpeningAction.Use, capabilities);
        var replaced = Commit(three, 1, OpeningAction.Jump, capabilities);
        MainOpeningChordTests.EqualActions(initial, OpeningAction.Aim);
        MainOpeningChordTests.EqualActions(two, OpeningAction.Aim, OpeningAction.Melee);
        MainOpeningChordTests.EqualActions(three, OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use);
        MainOpeningChordTests.EqualActions(replaced, OpeningAction.Jump, OpeningAction.Melee, OpeningAction.Use);
        TestAssert.Equal(4UL, replaced.Revision);
    }

    public static void DuplicatesIncludingReselectAndMissingPreviousSlotsRejectWithoutMutation() {
        var initial = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee);
        Reject(initial, initial.Revision, 1, OpeningAction.Aim, OpeningControlFailure.DuplicateAction);
        Reject(initial, initial.Revision, 3, OpeningAction.Melee, OpeningControlFailure.DuplicateAction);
        var one = MainOpeningChordTests.Create(OpeningAction.Use);
        Reject(one, one.Revision, 3, OpeningAction.Aim, OpeningControlFailure.MissingPreviousSlot);
        Reject(one, one.Revision, 3, null, OpeningControlFailure.MissingPreviousSlot);
        Reject(one, one.Revision, 0, OpeningAction.Aim, OpeningControlFailure.InvalidSlot);
        Reject(one, one.Revision, 4, OpeningAction.Aim, OpeningControlFailure.InvalidSlot);
        Reject(one, one.Revision, int.MinValue, OpeningAction.Aim, OpeningControlFailure.InvalidSlot);
        Reject(one, one.Revision, int.MaxValue, OpeningAction.Aim, OpeningControlFailure.InvalidSlot);
    }

    public static void NoneTrimsAllLaterSlotsAndAnAlreadyEmptyConsecutiveSlotKeepsItsRevision() {
        var capabilities = MainOpeningChordTests.AllActions();
        var initial = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use);
        var trimmedThird = Commit(initial, 3, null, capabilities);
        MainOpeningChordTests.EqualActions(trimmedThird, OpeningAction.Aim, OpeningAction.Melee);
        var trimmedSecond = Commit(initial, 2, null, capabilities);
        MainOpeningChordTests.EqualActions(trimmedSecond, OpeningAction.Aim);
        Reject(initial, initial.Revision, 1, null, OpeningControlFailure.FirstSlotRequired);
        TestAssert.True(trimmedSecond.TrySetSlot(trimmedSecond.Revision, 2, null, capabilities, out var unchanged, out var failure), "None at the first unused consecutive slot is accepted");
        TestAssert.True(ReferenceEquals(trimmedSecond, unchanged), "An accepted no-op must retain its policy owner and revision");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        MainOpeningChordTests.EqualActions(initial, OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use);
    }

    public static void FragAndSecondaryOffhandCannotCoexistButSameSlotReplacementIsAllowed() {
        var capabilities = MainOpeningChordTests.AllActions();
        foreach (var pair in new[] {
            (OpeningAction.Frag, OpeningAction.SecondaryOffhand),
            (OpeningAction.SecondaryOffhand, OpeningAction.Frag)
        }) {
            var initial = MainOpeningChordTests.Create(pair.Item1, OpeningAction.Aim);
            Reject(initial, initial.Revision, 2, pair.Item2, OpeningControlFailure.ConflictingOffhands);
            var replaced = Commit(initial, 1, pair.Item2, capabilities);
            MainOpeningChordTests.EqualActions(replaced, pair.Item2, OpeningAction.Aim);
        }
    }

    public static void StaleRevisionUnavailableActionAndRevisionExhaustionCannotPartiallyCommit() {
        var capabilities = MainOpeningChordTests.AllActions();
        var initial = MainOpeningChordTests.Create(OpeningAction.Aim, OpeningAction.Melee);
        Reject(initial, 0, 3, OpeningAction.Use, OpeningControlFailure.StaleRevision);
        Reject(initial, 2, 3, OpeningAction.Use, OpeningControlFailure.StaleRevision);
        Reject(initial, initial.Revision, 3, (OpeningAction)13, OpeningControlFailure.UnknownAction);
        TestAssert.True(VerifiedOpeningActions.TryCreate(new[] { OpeningAction.Aim, OpeningAction.Melee }, out var partial, out _), "A partial verified set is valid");
        Reject(initial, initial.Revision, 3, OpeningAction.Use, OpeningControlFailure.UnavailableAction, partial);
        TestAssert.True(!initial.TrySetSlot(initial.Revision, 3, OpeningAction.Use, null, out var rejected, out var failure), "A missing capability owner must reject");
        TestAssert.True(ReferenceEquals(initial, rejected), "Capability failure must retain the original policy");
        TestAssert.Equal(OpeningControlFailure.InvalidCapabilities, failure);
        // A lost previously verified action cannot silently survive a changed capability set
        Reject(initial, initial.Revision, 3, OpeningAction.Aim, OpeningControlFailure.DuplicateAction, partial);
        TestAssert.True(VerifiedOpeningActions.TryCreate(new[] { OpeningAction.Use }, out var useOnly, out _), "The supplied set is explicit");
        Reject(initial, initial.Revision, 1, OpeningAction.Use, OpeningControlFailure.UnavailableAction, useOnly);
        TestAssert.True(MainOpeningChord.TryCreate(new[] { OpeningAction.Aim }, ulong.MaxValue - 1, capabilities, out var penultimate, out _), "The preceding revision may be imported");
        var lastCommitted = Commit(penultimate!, 2, OpeningAction.Use, capabilities);
        TestAssert.Equal(ulong.MaxValue, lastCommitted.Revision);
        Reject(lastCommitted, ulong.MaxValue, 3, OpeningAction.Melee, OpeningControlFailure.RevisionExhausted);
        MainOpeningChordTests.EqualActions(penultimate!, OpeningAction.Aim);
        TestAssert.True(MainOpeningChord.TryCreate(new[] { OpeningAction.Aim }, ulong.MaxValue, capabilities, out var exhausted, out _), "The last positive revision may be imported");
        Reject(exhausted!, ulong.MaxValue, 2, OpeningAction.Use, OpeningControlFailure.RevisionExhausted);
        TestAssert.True(exhausted!.TrySetSlot(ulong.MaxValue, 2, null, capabilities, out var noChange, out _), "An accepted no-op needs no new revision");
        TestAssert.True(ReferenceEquals(exhausted, noChange), "A no-op at the maximum revision keeps its immutable owner");
    }

    internal static MainOpeningChord Commit(MainOpeningChord chord, int slot, OpeningAction? action, VerifiedOpeningActions capabilities) {
        TestAssert.True(chord.TrySetSlot(chord.Revision, slot, action, capabilities, out var updated, out var failure), "A valid mutation must commit");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        TestAssert.Equal(checked(chord.Revision + 1), updated.Revision);
        return updated;
    }

    private static void Reject(MainOpeningChord chord, ulong expectedRevision, int slot, OpeningAction? action,
        OpeningControlFailure expected, VerifiedOpeningActions? capabilities = null) {
        var oldActions = chord.Actions.ToArray();
        var oldRevision = chord.Revision;
        var supplied = capabilities ?? MainOpeningChordTests.AllActions();
        TestAssert.True(!chord.TrySetSlot(expectedRevision, slot, action, supplied, out var result, out var failure), "The rejected mutation must fail");
        TestAssert.Equal(expected, failure);
        TestAssert.True(ReferenceEquals(chord, result), "Rejection must return the original owner");
        TestAssert.Equal(oldRevision, chord.Revision);
        TestAssert.True(chord.Actions.SequenceEqual(oldActions), "Rejection must leave the entire tuple unchanged");
    }
}
