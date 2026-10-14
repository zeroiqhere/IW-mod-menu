using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class VerifiedOpeningActionsTests {
    public static void CapabilitiesCopyOnlyExplicitKnownDistinctActionsAndAllowNoActions() {
        OpeningAction[] supplied = { OpeningAction.Melee, OpeningAction.Aim };
        TestAssert.True(VerifiedOpeningActions.TryCreate(supplied, out var verified, out var failure), "Explicit actions must be accepted");
        TestAssert.Equal(OpeningControlFailure.None, failure);
        supplied[0] = OpeningAction.Jump;
        TestAssert.True(verified!.Contains(OpeningAction.Melee) && !verified.Contains(OpeningAction.Jump), "Capabilities must own a copy");
        TestAssert.True(!verified.Contains((OpeningAction)65536), "An unknown ID must not alias a known bit");
        TestAssert.Equal(OpeningAction.Melee, verified.Actions[0]);
        TestAssert.Equal(OpeningAction.Aim, verified.Actions[1]);
        TestAssert.Throws<NotSupportedException>(() => ((IList<OpeningAction>)verified.Actions).Clear());

        TestAssert.True(VerifiedOpeningActions.TryCreate(Array.Empty<OpeningAction>(), out var empty, out _), "No proved bindings is a valid unavailable capability set");
        TestAssert.Equal(0, empty!.Actions.Count);
        Reject(null, OpeningControlFailure.InvalidCapabilities);
        Reject(new[] { OpeningAction.Aim, OpeningAction.Aim }, OpeningControlFailure.DuplicateAction);
        Reject(new[] { (OpeningAction)13 }, OpeningControlFailure.UnknownAction);
        Reject(new OpeningAction[14], OpeningControlFailure.InvalidCapabilities);
    }

    private static void Reject(IReadOnlyList<OpeningAction>? actions, OpeningControlFailure expected) {
        TestAssert.True(!VerifiedOpeningActions.TryCreate(actions, out var result, out var failure), "Invalid capability input must reject");
        TestAssert.True(result is null, "A failed capability factory must not publish a partial set");
        TestAssert.Equal(expected, failure);
    }
}
