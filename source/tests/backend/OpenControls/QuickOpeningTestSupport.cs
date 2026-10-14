using Apparition.Backend.OpenControls;

namespace Backend.Tests;

// All test capabilities are explicit logical actions, without physical input or game access
internal static class QuickOpeningTestSupport {
    internal static VerifiedOpeningActions Capabilities(params OpeningAction[] actions) {
        TestAssert.True(VerifiedOpeningActions.TryCreate(actions, out var result, out _),
            "The supplied test capabilities must be valid");
        return result!;
    }

    internal static VerifiedOpeningActions AllCapabilities() =>
        Capabilities(OpeningActionCatalog.Items.Select(item => item.Action).ToArray());

    internal static QuickOpeningChord Quick(ulong revision, params OpeningAction[] actions) {
        TestAssert.True(QuickOpeningChord.TryCreate(actions, revision, AllCapabilities(),
            out var result, out var failure), "A valid Quick tuple must be created");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        return result!;
    }

    internal static MainOpeningChord MainChord(ulong revision, params OpeningAction[] actions) {
        TestAssert.True(MainOpeningChord.TryCreate(actions, revision, AllCapabilities(),
            out var result, out _), "A valid Main tuple must be created");
        return result!;
    }

    internal static QuickOpeningPolicies Policies(ulong revision = 1) {
        var main = MainChord(1, OpeningAction.Aim, OpeningAction.Melee);
        var quick = Quick(1, OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        TestAssert.True(QuickOpeningPolicies.TryCreate(main, quick, revision, false,
            out var result, out _), "Both independently validated tuples must be accepted");
        return result!;
    }

    internal static void Actions(IReadOnlyList<OpeningAction> actual, params OpeningAction[] expected) {
        TestAssert.True(actual.SequenceEqual(expected), "The entire ordered tuple must match");
    }
}
