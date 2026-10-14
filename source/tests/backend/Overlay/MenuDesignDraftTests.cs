using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignDraftTests
{
    public static void ExactDesignDomainAndIndependentDraftRejectUnsupportedValuesWithoutMutation()
    {
        foreach (string name in MenuDesignTests.Names)
        {
            var actual = MenuAppearance.Default with { Design = name, ColoredCursor = MenuDesignDraft.AllowsColoredCursor(name) };
            TestAssert.True(actual.IsSupported(), "Every source design has an exact supported name");
            // A restored applied design never changes the source slider's first candidate
            TestAssert.Equal("Apparition", new MenuDesignDraft().Candidate);
            TestAssert.True(!actual.TryChange((int)MenuPreference.Design, out var untouched), "Design requires its typed commit route");
            TestAssert.Equal(actual, untouched);
        }
        foreach (string name in new[] { "", "native", "Aio", "Modern", "Apparition " })
        {
            var invalid = MenuAppearance.Default with { Design = name };
            TestAssert.True(!invalid.IsSupported(), "Unknown or case-changed designs reject");
            TestAssert.True(!new MenuDesignDraft().TryCommit(invalid, out var untouched), "Invalid applied state cannot partially change");
            TestAssert.Equal(invalid, untouched);
        }
        foreach (int index in new[] { -1, 5, int.MinValue, int.MaxValue })
        {
            var invalid = new MenuDesignDraft(index);
            TestAssert.True(!invalid.TryMove(1, out var untouched), "Out-of-range candidates cannot wrap into valid state");
            TestAssert.Equal(invalid, untouched);
            TestAssert.True(!invalid.TryCommit(MenuAppearance.Default, out _), "Invalid candidates cannot commit");
        }
        foreach (int direction in new[] { 0, 2, int.MinValue, int.MaxValue })
        {
            var draft = new MenuDesignDraft(); TestAssert.True(!draft.TryMove(direction, out var untouched), "Only single source increments are allowed"); TestAssert.Equal(draft, untouched);
        }
    }

    public static void ForbiddenDesignFlagCombinationsRejectAtEveryPublicTypedBoundary()
    {
        foreach (string design in MenuDesignTests.Names.Skip(1))
        {
            var invalid = MenuAppearance.Default with { Design = design, OptionCounter = true };
            TestAssert.True(!invalid.IsSupported(), "The managed domain must match the native renderer contract");
            TestAssert.True(!invalid.TryChange((int)MenuPreference.QuickExit, out var untouched), "Invalid combinations cannot partially update"); TestAssert.Equal(invalid, untouched);
            TestAssert.True(!new MenuDesignDraft(Array.IndexOf(MenuDesignTests.Names, design)).TryCommit(invalid, out untouched), "A same-design action cannot authorize malformed flags"); TestAssert.Equal(invalid, untouched);
            TestAssert.Throws<ArgumentException>(() => OverlayMenuBuilder.Build(MenuPage.Preferences, new SessionBoundary().State, appearance: invalid));
            TestAssert.Throws<InvalidOperationException>(() => Apparition.Backend.Ipc.StateDocument.Create(42, 0, false,
                new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0, appearance: invalid));
        }
    }

    public static void IncompatibleFlagsCannotBypassDisabledRowsAndSameDesignDoesNotForceSavedFlags()
    {
        foreach (string design in MenuDesignTests.Names)
        {
            var actual = MenuAppearance.Default with
            {
                Design = design,
                ColoredCursor = MenuDesignDraft.AllowsColoredCursor(design),
                OptionCounter = MenuDesignDraft.AllowsOptionCounter(design)
            };
            TestAssert.Equal(MenuDesignDraft.AllowsColoredCursor(design), actual.TryChange((int)MenuPreference.ColoredCursor, out _));
            TestAssert.Equal(MenuDesignDraft.AllowsOptionCounter(design), actual.TryChange((int)MenuPreference.OptionCounter, out _));
            var draft = new MenuDesignDraft(Array.IndexOf(MenuDesignTests.Names, design));
            TestAssert.True(draft.TryCommit(actual, out var unchanged), "The source accepts the same applied candidate");
            TestAssert.Equal(actual, unchanged);
        }
    }
}
