using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Exercise the real view and adapter with supplied state, without a session or process
internal static class BasicScriptsMenuRowsTests {
    // Source order is exact; the unavailable grenade token uses one explicit binding-neutral caption
    private static readonly string[] ReferenceLabels = {
        "God Mode", "Demi-God", "Noclip", "Bind Noclip To Lethal Action", "UFO Mode", "Unlimited Ammo",
        "Unlimited Equipment", "Unlimited Special Weapon", "Modify Score", "Perk Menu", "Gobblegum Menu",
        "Movement Speed", "Third Person", "Invisibility", "Clone", "No Target", "Reduced Spread", "Multi-Jump",
        "Disable HUD", "Visual Effects", "Set Vision", "Zombie Charms", "Custom Crosshairs", "No Explosive Damage",
        "Character Model Index", "Random Character Model Index", "Shoot While Sprinting", "Unlimited Sprint",
        "Respawn", "Revive", "Death"
    };

    public static void SourceOrderedPersonalViewMatchesAllThirtyOneDeclarations() {
        var snapshot = OverlayMenuBuilder.Build(MenuPage.Personal, State());

        TestAssert.Equal("Basic Scripts", snapshot.Title);
        TestAssert.Equal(31, snapshot.Rows.Count);
        TestAssert.True(snapshot.Rows.Select(row => row.Label).SequenceEqual(ReferenceLabels),
            "The actual Personal page must preserve the pinned Basic Scripts declaration order");
        TestAssert.Equal("UNKNOWN", snapshot.Rows[0].Value);
        TestAssert.True(!snapshot.Rows[0].Enabled, "Disconnected personal Godmode cannot become available");
    }

    public static void PersonalGodmodeRetainsExistingCapabilityAndValueGuards() {
        var connected = State() with { Connected = true, CanPersonalGodmode = true,
            PersonalGodmodeValue = "ON", PersonalGodmodeStatus = "Owned personal state",
            SelectedSlot = 2, SelectedName = "Friend", CanTeammateGodmode = true, TeammateGodmodeValue = "OFF" };
        var cases = new[] {
            (connected, "ON", true),
            (connected with { PersonalGodmodeValue = "OFF" }, "OFF", true),
            (connected with { CanPersonalGodmode = false }, "ON", false),
            (connected with { Connected = false }, "UNKNOWN", false),
            (connected with { Preview = true }, "UNKNOWN", false),
            (connected with { Busy = true }, "UNKNOWN", false)
        };
        foreach (var (state, value, enabled) in cases) {
            var row = BasicScriptsMenuRows.Build(state)[0];

            TestAssert.Equal("God Mode", row.Label);
            TestAssert.Equal(value, row.Value);
            TestAssert.Equal(enabled, row.Enabled);
            TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, row.Action.Kind);
            TestAssert.True(row.Detail.Contains("Owned personal state", StringComparison.Ordinal),
                "The existing personal status must survive the source label change");
            TestAssert.True(row.Detail.Contains("personal", StringComparison.OrdinalIgnoreCase),
                "The source label must still identify the personal scope");
        }
    }

    public static void UnsupportedEntriesNeverExposeAppliedStatesOrActions() {
        var state = State() with { Connected = true, CanPersonalGodmode = true, PersonalGodmodeValue = "ON",
            CanLobbyAmmo = true, LobbyAmmoValue = "ON", CanTeammateGodmode = true, TeammateGodmodeValue = "ON" };
        var rows = BasicScriptsMenuRows.Build(state);

        TestAssert.Equal(31, rows.Count);
        foreach (var row in rows.Skip(1)) {
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.True(!row.Enabled, "A source declaration cannot invent a working IW callback");
            TestAssert.Equal(MenuActionKind.None, row.Action.Kind);
            TestAssert.True(!string.IsNullOrWhiteSpace(row.Detail), "Unavailable options need relevant explanatory help");
            TestAssert.True(!row.IsSpacer && row.OpeningAction is null, "Gameplay catalog rows cannot impersonate opening controls");
            foreach (string term in new[] { "validated", "binding", "callback", "clientfield", "entity linking" }) {
                TestAssert.True(!row.Detail.Contains(term, StringComparison.OrdinalIgnoreCase),
                    "Unavailable help must explain player-visible scope rather than implementation details");
            }
        }
        TestAssert.Throws<ArgumentNullException>(() => BasicScriptsMenuRows.Build(null!));
    }

    public static void PersonalAmmoCannotBorrowTheEnabledLobbyCapability() {
        var state = State() with { Connected = true, CanLobbyAmmo = true, LobbyAmmoValue = "ON" };
        var personal = OverlayMenuBuilder.Build(MenuPage.Personal, state);
        var lobby = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state);
        var ammo = personal.Rows.Single(row => row.Label == "Unlimited Ammo");

        TestAssert.Equal("UNAVAILABLE", ammo.Value);
        TestAssert.True(!ammo.Enabled && ammo.Action.Kind == MenuActionKind.None,
            "The lobby patch cannot serve the source per-player ammo row");
        TestAssert.True(ammo.Detail.Contains("personal", StringComparison.OrdinalIgnoreCase)
            && ammo.Detail.Contains("All Players", StringComparison.Ordinal)
            && ammo.Detail.Contains("lobby", StringComparison.OrdinalIgnoreCase),
            "Help must distinguish unavailable personal ammo from the separate lobby control");
        TestAssert.Equal("ON", lobby.Rows[11].Value);
        TestAssert.Equal(MenuActionKind.ToggleLobbyAmmo, lobby.Rows[11].Action.Kind);
    }

    public static void SubmenusAndBindingNeutralNoclipCaptionRemainInertReferenceRows() {
        var rows = BasicScriptsMenuRows.Build(State());
        var bind = rows.Single(row => row.Label == "Bind Noclip To Lethal Action");

        TestAssert.True(rows.All(row => !row.Label.Contains("[{", StringComparison.Ordinal)),
            "Unresolved source implementation tokens must not leak into product captions");
        TestAssert.True(!bind.Enabled && bind.OpeningAction is null,
            "An unavailable Noclip bind cannot claim a guessed physical key or opening action");
        foreach (string label in new[] { "Perk Menu", "Gobblegum Menu", "Visual Effects" }) {
            var submenu = rows.Single(row => row.Label == label);
            TestAssert.Equal("UNAVAILABLE", submenu.Value);
            TestAssert.Equal(MenuActionKind.None, submenu.Action.Kind);
            TestAssert.True(!submenu.Enabled, "Unimplemented source submenus cannot advertise navigation");
        }
    }

    public static void RowsAreStableCopiesAndCannotAlterLaterMenuBuilds() {
        var first = BasicScriptsMenuRows.Build(State() with { Connected = true,
            CanPersonalGodmode = true, PersonalGodmodeValue = "ON" });
        var later = BasicScriptsMenuRows.Build(State());

        TestAssert.Equal(31, first.Count);
        TestAssert.Throws<NotSupportedException>(() => ((IList<MenuRow>)first)[0] = first[1]);
        TestAssert.Equal("ON", first[0].Value);
        TestAssert.Equal("UNKNOWN", later[0].Value);
        TestAssert.True(first.Select(row => row.Label).SequenceEqual(later.Select(row => row.Label)),
            "Each build retains the same source inventory without sharing mutable row storage");
        TestAssert.Equal(900, MenuOptionCapacity.OpeningDurationMilliseconds(first.Count, MenuAppearance.Default));
        TestAssert.Equal(400, MenuOptionCapacity.OpeningDurationMilliseconds(first.Count,
            MenuAppearance.Default with { StealthUI = true, BooleanDisplay = "Text" }));
    }

    public static void DisabledRowsStayInspectableWithoutDispatchingSelection() {
        var navigation = new OverlayNavigation();
        navigation.SetRows(BasicScriptsMenuRows.Build(State()));
        navigation.Open();
        navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(BasicScriptsMenuRows.Build(State()));
        // Disabled rows remain selectable for help rather than being skipped as blanks
        navigation.Handle(OverlayInput.Down);

        TestAssert.Equal(1, navigation.SelectedIndex);
        TestAssert.Equal("Demi-God", navigation.Rows[navigation.SelectedIndex].Label);
        TestAssert.True(navigation.Handle(OverlayInput.Select) is null, "Unavailable selection must never return dispatch intent");
        navigation.Handle(OverlayInput.Back);
        navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(BasicScriptsMenuRows.Build(State()));
        TestAssert.Equal(1, navigation.SelectedIndex);
    }

    public static void SupportedGodmodeSelectionPreservesThePersonalActionIntent() {
        var navigation = new OverlayNavigation();
        var rows = BasicScriptsMenuRows.Build(State() with { Connected = true,
            CanPersonalGodmode = true, PersonalGodmodeValue = "OFF" });
        navigation.SetRows(rows);
        navigation.Open();
        navigation.OpenPage(MenuPage.Personal);
        navigation.SetRows(rows);

        var action = navigation.Handle(OverlayInput.Select);

        TestAssert.True(action is not null, "The existing supported personal action must remain selectable");
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, action!.Kind);
        TestAssert.True(rows.Skip(1).All(row => row.Action.Kind == MenuActionKind.None),
            "Catalog growth cannot introduce additional gameplay intents");
    }

    private static OverlayMenuState State() => new(false, false, false, true, false, "UNKNOWN", "Personal state unavailable",
        false, "UNKNOWN", "", false, false, Array.Empty<OverlayPlayer>(), -1, null, false, "UNKNOWN", "", false, "");
}
