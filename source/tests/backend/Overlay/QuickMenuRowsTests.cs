using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Supplied identities are test data, not evidence for a retail local-player binding
internal static class QuickMenuRowsTests {
    private static readonly PlayerIdentity Local = new(0, 0x10000, 0x20000, 0x30000,
        4, "Local player", "1234567890ABCDEF");
    private static readonly string[] LivingLabels = {
        "God Mode", "Noclip", "Bind Noclip To Lethal Action", "Unlimited Ammo",
        "Unlimited Equipment", "Modify Score", "Perk Menu", "No Target",
        "Reduced Spread", "Unlimited Sprint", "Respawn", "Revive"
    };

    public static void EligibleLivingSelfUsesTheSourceOrderedQuickDeclarations() {
        var rows = Build();

        TestAssert.Equal(12, rows.Count);
        TestAssert.True(rows.Select(row => row.Label).SequenceEqual(LivingLabels),
            "The living Quick view must preserve the source declaration order");
        TestAssert.Equal("ON", rows[0].Value);
        TestAssert.True(rows[0].Enabled, "Eligible personal Godmode keeps its existing capability");
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, rows[0].Action.Kind);
        TestAssert.True(rows[0].Detail.Contains("Personal state", StringComparison.Ordinal),
            "The actual personal status remains visible");
    }

    public static void DeadSelfShowsRespawnAndOnlyVerifiedAuthorityDeclarations() {
        // The unconditional Respawn declaration does not imply a working respawn operation
        var observation = Observation(LocalPlayerLife.Dead);
        var rows = Build(facts: new(QuickPlayerLife.Dead, false, false), observation: observation);
        var hostRows = Build(facts: new(QuickPlayerLife.Dead, true, null), observation: observation);

        TestAssert.True(rows.Select(row => row.Label).SequenceEqual(new[] { "Respawn" }),
            "The dead branch does not declare living callbacks or Revive");
        TestAssert.True(hostRows.Select(row => row.Label).SequenceEqual(new[] { "Respawn", "Restart Game", "Disconnect" }),
            "Supplied verified host facts append only the source authority declarations");
        TestAssert.True(hostRows.All(row => !row.Enabled && row.Action.Kind == MenuActionKind.None),
            "Authority declarations cannot grant an unimplemented callback");
    }

    public static void UnknownLifeAndPartialMenuFactsCannotExposeLivingActions() {
        var unknown = new QuickMenuEligibility(QuickPlayerLife.Unknown, true, true);
        var observations = new[] {
            LocalPlayerObservation.BindingUnavailable,
            Observation(LocalPlayerLife.Unknown),
            Observation() with { HasMenu = null },
            Observation() with { HasMenu = false }
        };
        foreach (var observation in observations) {
            AssertOnlyInertRespawn(Build(facts: new(QuickPlayerLife.Living, true, true), observation: observation));
        }
        AssertOnlyInertRespawn(Build(facts: unknown));
        // A known catalog survives a monitor pause without exposing an enabled gameplay action
        foreach (bool? disabled in new bool?[] { null, true }) {
            var paused = Build(observation: Observation() with { ControlsDisabled = disabled });
            TestAssert.Equal(12, paused.Count);
            TestAssert.True(paused.All(row => !row.Enabled), "A partial control fact cannot authorize an action");
        }
    }

    public static void MismatchedOrMalformedLocalSnapshotsRejectPositiveEligibility() {
        var observations = new[] {
            Observation() with { ProcessId = 52 },
            Observation(LocalPlayerLife.Dead),
            Observation() with { Identity = Local with { Guid = "0000000000000000" } },
            Observation() with { Identity = Local with { Slot = 3, ServerCount = 1 } },
            Observation() with { Status = "" },
            Observation() with { Life = (LocalPlayerLife)99 }
        };
        foreach (var observation in observations) AssertOnlyInertRespawn(Build(observation: observation));

        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), Facts(), Observation(), null, 51));
        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), Facts(), Observation(), Local, 0));
        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), Facts(), Observation(), Local, -1));
        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), Facts(), Observation(), Local with { Client = 0x40000 }, 51));
        // A living classification cannot substitute for the separate dead life branch
        AssertOnlyInertRespawn(Build(facts: new(QuickPlayerLife.Dead, true, false)));
    }

    public static void QuickGodmodePreservesPersonalPreviewConnectionBusyAndCapabilityGuards() {
        // Known supplied life still cannot bypass the existing personal feature guards
        var state = State();
        var cases = new[] {
            (state, "ON", true),
            (state with { PersonalGodmodeValue = "OFF" }, "OFF", true),
            (state with { CanPersonalGodmode = false }, "ON", false),
            (state with { Preview = true }, "UNKNOWN", false),
            (state with { Connected = false }, "UNKNOWN", false),
            (state with { Busy = true }, "UNKNOWN", false)
        };
        foreach (var (input, value, enabled) in cases) {
            var row = Build(state: input)[0];
            TestAssert.Equal("God Mode", row.Label);
            TestAssert.Equal(value, row.Value);
            TestAssert.Equal(enabled, row.Enabled);
            TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, row.Action.Kind);
        }
    }

    public static void UnsupportedQuickCallbacksCannotBorrowLobbyOrTeammateState() {
        var rows = Build(state: State() with { CanLobbyAmmo = true, LobbyAmmoValue = "ON",
            CanTeammateGodmode = true, TeammateGodmodeValue = "ON" }, facts: new(QuickPlayerLife.Living, null, true));

        TestAssert.Equal(14, rows.Count);
        foreach (var row in rows.Skip(1)) {
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.True(!row.Enabled && row.Action.Kind == MenuActionKind.None,
                "Unsupported source callbacks must stay inert even with other working features");
            TestAssert.True(!row.IsSpacer && row.OpeningAction is null,
                "Quick gameplay declarations are not opening-control rows");
            TestAssert.True(!string.IsNullOrWhiteSpace(row.Detail), "Unavailable declarations need useful help");
        }
        var ammo = rows.Single(row => row.Label == "Unlimited Ammo");
        TestAssert.True(ammo.Detail.Contains("personal", StringComparison.OrdinalIgnoreCase)
            && ammo.Detail.Contains("lobby", StringComparison.OrdinalIgnoreCase),
            "Quick personal ammo must remain distinct from the separate lobby patch");
    }

    public static void AuthorityFactsCannotBypassAnUnboundOrWrongSelectedSelf() {
        var authority = new QuickMenuEligibility(QuickPlayerLife.Living, true, true);
        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), authority,
            LocalPlayerObservation.BindingUnavailable, Local, 51));
        AssertOnlyInertRespawn(QuickMenuRows.Build(State(), authority,
            Observation(), Local with { Name = "Friend" }, 51));

        var withoutAuthority = Build(facts: new(QuickPlayerLife.Living, null, false));
        TestAssert.Equal(12, withoutAuthority.Count);
        TestAssert.True(withoutAuthority.All(row => row.Label != "Restart Game" && row.Label != "Disconnect"),
            "An unknown authority field does not authorize server declarations");
    }

    public static void QuickRowsAreBoundedReadOnlyCopiesWithNeutralNoclipCaption() {
        // Building another view cannot alter an already published value or its row storage
        var first = Build();
        var second = Build(state: State() with { PersonalGodmodeValue = "OFF" });
        TestAssert.Throws<NotSupportedException>(() => ((IList<MenuRow>)first)[0] = first[1]);

        TestAssert.True(first.Count <= 14 && second.Count <= 14, "The source catalog has a fixed small bound");
        TestAssert.Equal("ON", first[0].Value);
        TestAssert.Equal("OFF", second[0].Value);
        TestAssert.True(first.All(row => !row.Label.Contains("[{", StringComparison.Ordinal)),
            "Product rows must not expose an unresolved source key token");
        TestAssert.True(QuickMenuCatalog.Build(Facts())[2].Label.Contains("[{+frag}]", StringComparison.Ordinal),
            "The separate source audit catalog retains its original token");
    }

    public static void MissingRequiredBuilderInputsRejectInsteadOfGuessingFacts() {
        TestAssert.Throws<ArgumentNullException>(() => QuickMenuRows.Build(null!, Facts(), Observation(), Local, 51));
        TestAssert.Throws<ArgumentNullException>(() => QuickMenuRows.Build(State(), null!, Observation(), Local, 51));
        TestAssert.Throws<ArgumentNullException>(() => QuickMenuRows.Build(State(), Facts(), null!, Local, 51));
    }

    private static IReadOnlyList<MenuRow> Build(OverlayMenuState? state = null, QuickMenuEligibility? facts = null,
        LocalPlayerObservation? observation = null) =>
        QuickMenuRows.Build(state ?? State(), facts ?? Facts(), observation ?? Observation(), Local, 51);

    private static void AssertOnlyInertRespawn(IReadOnlyList<MenuRow> rows) {
        TestAssert.Equal(1, rows.Count);
        TestAssert.Equal("Respawn", rows[0].Label);
        TestAssert.Equal("UNAVAILABLE", rows[0].Value);
        TestAssert.True(!rows[0].Enabled && rows[0].Action.Kind == MenuActionKind.None,
            "The unconditional source declaration does not authorize opening or a respawn action");
    }

    private static QuickMenuEligibility Facts() => new(QuickPlayerLife.Living, false, false);
    private static LocalPlayerObservation Observation(LocalPlayerLife life = LocalPlayerLife.Alive) =>
        new(51, Local, life, true, false, "Supplied offline observation");
    private static OverlayMenuState State() => new(false, true, false, false, true, "ON", "Personal state",
        false, "UNKNOWN", "", false, false, Array.Empty<OverlayPlayer>(), -1, null, false, "UNKNOWN", "", false, "");
}
