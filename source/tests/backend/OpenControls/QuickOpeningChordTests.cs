using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class QuickOpeningChordTests {
    public static void DefaultUsesOnlyVerifiedAimAndSecondaryOffhand() {
        var capabilities = QuickOpeningTestSupport.Capabilities(
            OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        TestAssert.True(QuickOpeningChord.TryCreateDefault(capabilities, out var quick, out var failure),
            "The exact verified Quick default must be created without requiring Main's Melee");
        QuickOpeningTestSupport.Actions(quick!.Actions, OpeningAction.Aim, OpeningAction.SecondaryOffhand);
        TestAssert.Equal(1UL, quick.Revision);
        TestAssert.Equal(OpeningControlFailure.None, failure);

        // A valid Main default does not prove the missing Quick offhand
        var mainOnly = QuickOpeningTestSupport.Capabilities(OpeningAction.Aim, OpeningAction.Melee);
        TestAssert.True(MainOpeningChord.TryCreateDefault(mainOnly, out _, out _), "Main remains independent");
        TestAssert.True(!QuickOpeningChord.TryCreateDefault(mainOnly, out quick, out failure),
            "A missing exact SecondaryOffhand must reject instead of substituting a key");
        TestAssert.True(quick is null, "Rejected creation has no owner");
        TestAssert.Equal(OpeningControlFailure.UnavailableAction, failure);
        TestAssert.True(!QuickOpeningChord.TryCreateDefault(null, out _, out failure), "Missing evidence rejects");
        TestAssert.Equal(OpeningControlFailure.InvalidCapabilities, failure);
    }

    public static void CreationCopiesActionsAndRejectsMalformedUnverifiedAndConflictingTuples() {
        var input = new[] { OpeningAction.Use, OpeningAction.Aim, OpeningAction.Jump };
        var capabilities = QuickOpeningTestSupport.AllCapabilities();
        TestAssert.True(QuickOpeningChord.TryCreate(input, 7, capabilities, out var quick, out _),
            "Three verified actions form a valid tuple");
        input[0] = OpeningAction.Frag;
        QuickOpeningTestSupport.Actions(quick!.Actions, OpeningAction.Use, OpeningAction.Aim, OpeningAction.Jump);
        TestAssert.Throws<NotSupportedException>(() => ((IList<OpeningAction>)quick.Actions)[0] = OpeningAction.Frag);

        Reject(null, 1, capabilities, OpeningControlFailure.InvalidChord);
        Reject(Array.Empty<OpeningAction>(), 1, capabilities, OpeningControlFailure.InvalidChord);
        Reject(new[] { OpeningAction.Use, OpeningAction.Aim, OpeningAction.Jump, OpeningAction.Melee }, 1,
            capabilities, OpeningControlFailure.InvalidChord);
        Reject(new[] { OpeningAction.Aim }, 0, capabilities, OpeningControlFailure.InvalidRevision);
        Reject(new[] { (OpeningAction)(-1) }, 1, capabilities, OpeningControlFailure.UnknownAction);
        Reject(new[] { (OpeningAction)13 }, 1, capabilities, OpeningControlFailure.UnknownAction);
        Reject(new[] { OpeningAction.Aim, OpeningAction.Aim }, 1, capabilities, OpeningControlFailure.DuplicateAction);
        Reject(new[] { OpeningAction.Frag, OpeningAction.SecondaryOffhand }, 1, capabilities,
            OpeningControlFailure.ConflictingOffhands);
        Reject(new[] { OpeningAction.Use }, 1, QuickOpeningTestSupport.Capabilities(OpeningAction.Aim),
            OpeningControlFailure.UnavailableAction);
        Reject(new[] { OpeningAction.Use }, 1, null, OpeningControlFailure.InvalidCapabilities);
    }

    private static void Reject(IReadOnlyList<OpeningAction>? actions, ulong revision,
        VerifiedOpeningActions? capabilities, OpeningControlFailure expected) {
        TestAssert.True(!QuickOpeningChord.TryCreate(actions, revision, capabilities, out var chord, out var failure),
            "Invalid creation must reject");
        TestAssert.True(chord is null, "Invalid creation cannot expose an owner");
        TestAssert.Equal(expected, failure);
    }
}
