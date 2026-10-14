using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class MainOpeningChordTests {
    public static void DefaultRequiresVerifiedAimAndMeleeAndCopiesItsInputs() {
        var capabilities = AllActions();
        TestAssert.True(MainOpeningChord.TryCreateDefault(capabilities, out var chord, out var failure), "The verified default must be available");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        TestAssert.Equal(1UL, chord!.Revision);
        EqualActions(chord, OpeningAction.Aim, OpeningAction.Melee);
        OpeningAction[] supplied = { OpeningAction.Use };
        TestAssert.True(MainOpeningChord.TryCreate(supplied, 42, capabilities, out var copied, out _), "A verified single action must be accepted");
        supplied[0] = OpeningAction.Jump;
        EqualActions(copied!, OpeningAction.Use);
        TestAssert.Throws<NotSupportedException>(() => ((IList<OpeningAction>)copied!.Actions).Clear());
        TestAssert.True(VerifiedOpeningActions.TryCreate(new[] { OpeningAction.Aim }, out var aimOnly, out _), "A partial capability set is valid");
        TestAssert.True(!MainOpeningChord.TryCreateDefault(aimOnly, out var absent, out failure), "No fallback may replace missing verified Melee");
        TestAssert.True(absent is null, "A missing default must not create a policy");
        TestAssert.Equal(OpeningControlFailure.UnavailableAction, failure);
    }

    public static void CreationRejectsInvalidWidthsActionsCapabilitiesConflictsAndNonpositiveRevision() {
        var all = AllActions();
        Reject(null, 1, all, OpeningControlFailure.InvalidChord);
        Reject(Array.Empty<OpeningAction>(), 1, all, OpeningControlFailure.InvalidChord);
        Reject(new[] { OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use, OpeningAction.Jump }, 1, all, OpeningControlFailure.InvalidChord);
        Reject(new[] { (OpeningAction)13 }, 1, all, OpeningControlFailure.UnknownAction);
        Reject(new[] { OpeningAction.Aim, OpeningAction.Aim }, 1, all, OpeningControlFailure.DuplicateAction);
        Reject(new[] { OpeningAction.Frag, OpeningAction.SecondaryOffhand }, 1, all, OpeningControlFailure.ConflictingOffhands);
        Reject(new[] { OpeningAction.Use }, 0, all, OpeningControlFailure.InvalidRevision);
        Reject(new[] { OpeningAction.Use }, 1, null, OpeningControlFailure.InvalidCapabilities);
        TestAssert.True(VerifiedOpeningActions.TryCreate(Array.Empty<OpeningAction>(), out var empty, out _), "An empty capability set is explicit");
        Reject(new[] { OpeningAction.Use }, 1, empty, OpeningControlFailure.UnavailableAction);
        // The last revision can be imported, but no later policy change can be admitted
        TestAssert.True(MainOpeningChord.TryCreate(new[] { OpeningAction.Use }, ulong.MaxValue, all, out _, out _), "A positive maximum revision is a valid immutable owner");
    }

    internal static VerifiedOpeningActions AllActions() {
        var ids = OpeningActionCatalog.Items.Select(item => item.Action).ToArray();
        TestAssert.True(VerifiedOpeningActions.TryCreate(ids, out var result, out _), "The explicit catalog must form a valid capability set");
        return result!;
    }

    internal static MainOpeningChord Create(params OpeningAction[] actions) {
        TestAssert.True(MainOpeningChord.TryCreate(actions, 1, AllActions(), out var result, out _), "The test chord must be valid");
        return result!;
    }

    internal static void EqualActions(MainOpeningChord chord, params OpeningAction[] expected) {
        TestAssert.True(chord.Actions.SequenceEqual(expected), "The committed tuple must match the expected ordered actions");
    }

    private static void Reject(IReadOnlyList<OpeningAction>? actions, ulong revision, VerifiedOpeningActions? capabilities, OpeningControlFailure expected) {
        TestAssert.True(!MainOpeningChord.TryCreate(actions, revision, capabilities, out var result, out var failure), "Invalid chord input must reject");
        TestAssert.True(result is null, "A failed chord factory must not publish a partial owner");
        TestAssert.Equal(expected, failure);
    }
}
