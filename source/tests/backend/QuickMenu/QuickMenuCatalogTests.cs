using Apparition.Backend.QuickMenu;

namespace Backend.Tests;

internal static class QuickMenuCatalogTests {
    public static void LivingPlayerHasTheExactSourceOrderAndAllEntriesRemainUnavailable() {
        var rows = QuickMenuCatalog.Build(new(QuickPlayerLife.Living, false, false));
        string[] expected = ["God Mode", "Noclip", "Bind Noclip To [{+frag}]", "Unlimited Ammo", "Unlimited Equipment", "Modify Score", "Perk Menu", "No Target", "Reduced Spread", "Unlimited Sprint", "Respawn", "Revive"];

        TestAssert.Equal(string.Join('|', expected), string.Join('|', rows.Select(row => row.Label)));
        TestAssert.Equal(string.Join('|', Enumerable.Range(0, 12)), string.Join('|', rows.Select(row => (int)row.Id)));
        TestAssert.True(rows.All(row => !row.IsAvailable && !row.CanDispatch), "A declaration cannot claim an implemented IW operation");
        TestAssert.Equal(QuickMenuOptionKind.Choice, rows[3].Kind);
        TestAssert.Equal(QuickMenuOptionKind.Submenu, rows[6].Kind);
        TestAssert.Equal(QuickMenuOptionKind.Action, rows[10].Kind);
        TestAssert.Equal(QuickMenuOptionKind.Toggle, rows[9].Kind);
    }

    public static void DeadAndUnknownKeepOnlyTheUnconditionalRowWithoutGuessingLife() {
        foreach (var life in new[] { QuickPlayerLife.Dead, QuickPlayerLife.Unknown }) {
            var facts = new QuickMenuEligibility(life, null, null);
            var rows = QuickMenuCatalog.Build(facts);

            TestAssert.Equal(1, rows.Count);
            TestAssert.Equal(QuickMenuOptionId.Respawn, rows[0].Id);
            TestAssert.True(!rows[0].CanDispatch, "Respawn remains an unavailable declaration");
            TestAssert.Equal(life == QuickPlayerLife.Dead, facts.IsLifeKnown);
            TestAssert.Equal(life, facts.Life);
        }
    }

    public static void VerifiedHostOrDeveloperAppendsOnlyTheExactAuthorityOptions() {
        foreach (bool? host in new bool?[] { null, false, true }) {
            foreach (bool? developer in new bool?[] { null, false, true }) {
                var facts = new QuickMenuEligibility(QuickPlayerLife.Dead, host, developer);
                var rows = QuickMenuCatalog.Build(facts);
                bool authoritative = host == true || developer == true;

                TestAssert.Equal(authoritative ? 3 : 1, rows.Count);
                TestAssert.Equal(authoritative, facts.HasVerifiedAuthority);
                if (!authoritative) continue;
                TestAssert.Equal(QuickMenuOptionId.RestartGame, rows[1].Id);
                TestAssert.Equal("Restart Game", rows[1].Label);
                TestAssert.Equal("Full|Fast", string.Join('|', rows[1].Choices));
                TestAssert.Equal(QuickMenuOptionId.Disconnect, rows[2].Id);
                TestAssert.True(rows.All(row => !row.CanDispatch), "Verified source authority is not a native operation binding");
            }
        }
        TestAssert.Equal(14, QuickMenuCatalog.Build(new(QuickPlayerLife.Living, true, true)).Count);
    }

    public static void SourceChoiceValuesStayOrderedAndCannotBeMutated() {
        var rows = QuickMenuCatalog.Build(new(QuickPlayerLife.Living, true, false));
        var score = rows.Single(row => row.Id == QuickMenuOptionId.ModifyScore);
        string[] expected = ["1000000", "100000", "10000", "1000", "100", "10", "0", "-10", "-100", "-1000", "-10000", "-100000", "-1000000"];

        TestAssert.Equal(13, score.Choices.Count);
        TestAssert.Equal(string.Join('|', expected), string.Join('|', score.Choices));
        TestAssert.Equal("Continuous|Reload|Disable", string.Join('|', rows[3].Choices));
        TestAssert.Throws<NotSupportedException>(() => ((IList<string>)score.Choices)[0] = "changed");
        TestAssert.Throws<NotSupportedException>(() => ((IList<QuickMenuOption>)rows).Clear());
        TestAssert.Equal("1000000", QuickMenuCatalog.Build(new(QuickPlayerLife.Living, false, false))[5].Choices[0]);
        TestAssert.True(rows.All(row => row.Kind == QuickMenuOptionKind.Choice || row.Choices.Count == 0), "A non-slider borrowed source choices");
    }

    public static void UnknownLifeStaysExplicitAndInvalidFactValuesReject() {
        var unknown = new QuickMenuEligibility(QuickPlayerLife.Unknown, true, null);
        TestAssert.True(!unknown.IsLifeKnown, "A process or authority fact cannot establish alive state");
        TestAssert.Equal(3, QuickMenuCatalog.Build(unknown).Count);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new QuickMenuEligibility((QuickPlayerLife)99, false, false));
        TestAssert.Throws<ArgumentNullException>(() => QuickMenuCatalog.Build(null!));
    }
}
