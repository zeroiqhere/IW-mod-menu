using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ScrollAnimationDraftTests {
    public static void SourceUnitsFormatAsActualMillisecondsAndBothBoundsWrapWithoutApplying() {
        var draft = new ScrollAnimationDraft();
        TestAssert.True(draft.IsSupported(), "Default source units must be valid");
        TestAssert.Equal(12, draft.Units); TestAssert.Equal("120 ms", draft.DisplayValue);
        TestAssert.True(new ScrollAnimationDraft(10).TryMove(-1, out var previous), "Lower-bound movement must wrap");
        TestAssert.Equal(25, previous.Units);
        TestAssert.True(previous.TryMove(1, out var next), "Upper-bound movement must wrap");
        TestAssert.Equal(10, next.Units);
        foreach (int units in Enumerable.Range(10, 16)) {
            var current = new ScrollAnimationDraft(units);
            TestAssert.Equal(units * 10, current.Milliseconds);
            TestAssert.True(current.TryMove(1, out var moved), "Every source value must accept one positive step");
            TestAssert.Equal(units == 25 ? 10 : units + 1, moved.Units);
        }
    }

    public static void CommittedMillisecondsHaveAStrictDomainAndCannotBypassTheTypedDraftRoute() {
        TestAssert.Equal(10, (int)MenuPreference.AdaptiveInstructions);
        TestAssert.Equal(11, (int)MenuPreference.ScrollAnimationTime);
        foreach (int milliseconds in Enumerable.Range(10, 16).Select(units => units * 10)) {
            var original = MenuAppearance.Default with { ScrollAnimationMilliseconds = milliseconds };
            TestAssert.True(original.IsSupported(), "Every source duration must be accepted in actual milliseconds");
            var draft = ScrollAnimationDraft.FromAppearance(original);
            TestAssert.Equal(milliseconds / 10, draft.Units);
            TestAssert.True(draft.TryMove(1, out var candidate), "A valid candidate must move one source unit");
            TestAssert.True(candidate.TryCommit(original, out var committed), "A valid draft must commit through its typed route");
            TestAssert.Equal(original with { ScrollAnimationMilliseconds = candidate.Milliseconds }, committed);
            TestAssert.True(!original.TryChange((int)MenuPreference.ScrollAnimationTime, out var unchanged) && unchanged == original,
                "The boolean route cannot report success or change numeric timing");
        }
        foreach (int milliseconds in new[] { int.MinValue, 99, 101, 251, int.MaxValue }) {
            var invalid = MenuAppearance.Default with { ScrollAnimationMilliseconds = milliseconds };
            TestAssert.True(!invalid.IsSupported(), "Unsupported timing cannot enter a valid appearance");
            TestAssert.Throws<ArgumentException>(() => ScrollAnimationDraft.FromAppearance(invalid));
            TestAssert.True(!new ScrollAnimationDraft().TryCommit(invalid, out var unchanged) && unchanged == invalid,
                "A numeric commit cannot repair an invalid partial appearance");
        }
        TestAssert.Throws<ArgumentNullException>(() => ScrollAnimationDraft.FromAppearance(null!));
        var menu = OverlayMenuBuilder.Build(MenuPage.Preferences, new SessionBoundary().State,
            appearance: MenuAppearance.Default with { ScrollAnimationMilliseconds = 250 }, choiceControlsAvailable: true);
        TestAssert.Equal("250 ms", menu.Rows[3].Value);
        TestAssert.Throws<ArgumentException>(() => OverlayMenuBuilder.Build(MenuPage.Preferences, new SessionBoundary().State,
            scrollAnimationDraft: new(26)));
    }

    public static void UnsupportedValuesAndDirectionsCannotMoveCommitOrBypassTheBooleanRoute() {
        foreach (int units in new[] { int.MinValue, 9, 26, int.MaxValue }) {
            var invalid = new ScrollAnimationDraft(units);
            TestAssert.True(!invalid.IsSupported(), "Unsupported units must reject");
            TestAssert.True(!invalid.TryMove(1, out var unchanged) && unchanged == invalid, "Rejected moves preserve the original draft");
            TestAssert.True(!invalid.TryCommit(MenuAppearance.Default, out var appearance) && appearance == MenuAppearance.Default,
                "Invalid numeric candidates cannot commit");
        }
        foreach (int direction in new[] { int.MinValue, -2, 0, 2, int.MaxValue }) {
            var valid = new ScrollAnimationDraft();
            TestAssert.True(!valid.TryMove(direction, out var unchanged) && unchanged == valid, "Only exact single steps are supported");
        }
        foreach (int milliseconds in new[] { int.MinValue, 10, 99, 101, 251, int.MaxValue }) {
            TestAssert.True(!ScrollAnimationDraft.IsSupportedMilliseconds(milliseconds), "Duration bounds and ten-millisecond increments must be strict");
        }
        TestAssert.True(ScrollAnimationDraft.IsSupportedMilliseconds(100) && ScrollAnimationDraft.IsSupportedMilliseconds(250), "Both actual duration boundaries are supported");
    }
}
