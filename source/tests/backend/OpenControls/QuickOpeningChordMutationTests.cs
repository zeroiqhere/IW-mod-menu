using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class QuickOpeningChordMutationTests {
    public static void ConsecutiveEditsPreserveEarlierImmutableTuples() {
        var initial = QuickOpeningTestSupport.Quick(1, OpeningAction.Aim);
        var two = Commit(initial, 2, OpeningAction.SecondaryOffhand);
        var three = Commit(two, 3, OpeningAction.Use);
        var replaced = Commit(three, 1, OpeningAction.Jump);

        // Each earlier tuple remains usable after later immutable edits
        QuickOpeningTestSupport.Actions(initial.Actions, OpeningAction.Aim);
        QuickOpeningTestSupport.Actions(two.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        QuickOpeningTestSupport.Actions(three.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand, OpeningAction.Use);
        QuickOpeningTestSupport.Actions(replaced.Actions, OpeningAction.Jump, OpeningAction.SecondaryOffhand, OpeningAction.Use);
        TestAssert.Equal(4UL, replaced.Revision);
    }

    public static void StaleDuplicateAndMissingPreviousSlotEditsRejectWithoutMutation() {
        var one = QuickOpeningTestSupport.Quick(3, OpeningAction.Aim);
        Reject(one, 2, 2, OpeningAction.Use, OpeningControlFailure.StaleRevision);
        Reject(one, 4, 2, OpeningAction.Use, OpeningControlFailure.StaleRevision);
        Reject(one, 3, 1, OpeningAction.Aim, OpeningControlFailure.DuplicateAction);
        Reject(one, 3, 2, OpeningAction.Aim, OpeningControlFailure.DuplicateAction);
        Reject(one, 3, 3, OpeningAction.Use, OpeningControlFailure.MissingPreviousSlot);
        Reject(one, 3, 3, null, OpeningControlFailure.MissingPreviousSlot);
        foreach (int slot in new[] { int.MinValue, 0, 4, int.MaxValue }) {
            Reject(one, 3, slot, OpeningAction.Use, OpeningControlFailure.InvalidSlot);
        }
        Reject(one, 3, 2, (OpeningAction)13, OpeningControlFailure.UnknownAction);
    }

    public static void NoneTrimsLaterSlotsAndConsecutiveEmptySlotIsANoOp() {
        var initial = QuickOpeningTestSupport.Quick(1,
            OpeningAction.Aim, OpeningAction.SecondaryOffhand, OpeningAction.Use);
        var third = Commit(initial, 3, null);
        var second = Commit(initial, 2, null);
        // None removes the selected slot and every later slot together
        QuickOpeningTestSupport.Actions(third.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        QuickOpeningTestSupport.Actions(second.Actions, OpeningAction.Aim);
        Reject(initial, 1, 1, null, OpeningControlFailure.FirstSlotRequired);

        TestAssert.True(second.TrySetSlot(second.Revision, 2, null,
            QuickOpeningTestSupport.AllCapabilities(), out var unchanged, out var failure),
            "None at the first unused slot is accepted");
        TestAssert.True(ReferenceEquals(second, unchanged), "An accepted no-op retains its owner");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        QuickOpeningTestSupport.Actions(initial.Actions,
            OpeningAction.Aim, OpeningAction.SecondaryOffhand, OpeningAction.Use);
    }

    public static void SameSlotOffhandReplacementIsAllowedButPairingRejects() {
        foreach (var pair in new[] {
            (OpeningAction.Frag, OpeningAction.SecondaryOffhand),
            (OpeningAction.SecondaryOffhand, OpeningAction.Frag)
        }) {
            var initial = QuickOpeningTestSupport.Quick(1, OpeningAction.Aim, pair.Item1);
            Reject(initial, 1, 3, pair.Item2, OpeningControlFailure.ConflictingOffhands);
            var replaced = Commit(initial, 2, pair.Item2);
            QuickOpeningTestSupport.Actions(replaced.Actions, OpeningAction.Aim, pair.Item2);
        }
    }

    public static void CapabilityLossAndTupleRevisionExhaustionRejectAtomically() {
        var initial = QuickOpeningTestSupport.Quick(1, OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        var partial = QuickOpeningTestSupport.Capabilities(OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        Reject(initial, 1, 3, OpeningAction.Use, OpeningControlFailure.UnavailableAction, partial);
        var useOnly = QuickOpeningTestSupport.Capabilities(OpeningAction.Use);
        // A new candidate must also prove any actions retained from the old tuple
        Reject(initial, 1, 1, OpeningAction.Use, OpeningControlFailure.UnavailableAction, useOnly);
        TestAssert.True(!initial.TrySetSlot(1, 3, OpeningAction.Use, null, out var rejected, out var failure),
            "Missing capability evidence rejects before any commit");
        TestAssert.True(ReferenceEquals(initial, rejected), "Rejected capability evidence retains the tuple");
        TestAssert.Equal(OpeningControlFailure.InvalidCapabilities, failure);

        var penultimate = QuickOpeningTestSupport.Quick(ulong.MaxValue - 1, OpeningAction.Aim);
        var last = Commit(penultimate, 2, OpeningAction.Use);
        // The last positive revision is usable, but another changed tuple must reject
        TestAssert.Equal(ulong.MaxValue, last.Revision);
        Reject(last, ulong.MaxValue, 3, OpeningAction.Melee, OpeningControlFailure.RevisionExhausted);
        var exhaustedOne = QuickOpeningTestSupport.Quick(ulong.MaxValue, OpeningAction.Aim);
        TestAssert.True(exhaustedOne.TrySetSlot(ulong.MaxValue, 2, null,
            QuickOpeningTestSupport.AllCapabilities(), out var unchanged, out _),
            "An accepted no-op does not need a new tuple revision");
        TestAssert.True(ReferenceEquals(exhaustedOne, unchanged), "The exhausted no-op retains its owner");
    }

    private static QuickOpeningChord Commit(QuickOpeningChord current, int slot, OpeningAction? action) {
        TestAssert.True(current.TrySetSlot(current.Revision, slot, action,
            QuickOpeningTestSupport.AllCapabilities(), out var next, out var failure),
            "A valid Quick edit must commit");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        TestAssert.Equal(checked(current.Revision + 1), next.Revision);
        return next;
    }

    private static void Reject(QuickOpeningChord current, ulong revision, int slot,
        OpeningAction? action, OpeningControlFailure expected, VerifiedOpeningActions? capabilities = null) {
        var before = current.Actions.ToArray();
        TestAssert.True(!current.TrySetSlot(revision, slot, action,
            capabilities ?? QuickOpeningTestSupport.AllCapabilities(), out var next, out var failure),
            "An invalid Quick edit must reject");
        TestAssert.True(ReferenceEquals(current, next), "Rejection retains the immutable owner");
        TestAssert.Equal(expected, failure);
        QuickOpeningTestSupport.Actions(current.Actions, before);
    }
}
