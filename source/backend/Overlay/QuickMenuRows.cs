using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.Overlay;

// This view consumes supplied facts and does not read or authorize a game operation
public static class QuickMenuRows {
    public static IReadOnlyList<MenuRow> Build(OverlayMenuState state, QuickMenuEligibility facts,
        LocalPlayerObservation observation, PlayerIdentity? selectedLocalIdentity, int expectedProcessId) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(observation);
        // Cached shape and identity checks are not authentication or fresh command authorization
        // The caller remains responsible for a verified owner-bound observation and authority source
        QuickMenuEligibility declaredFacts = HasMatchingLocalBinding(facts, observation,
            selectedLocalIdentity, expectedProcessId)
            ? facts : new(QuickPlayerLife.Unknown, null, null);
        var rows = new List<MenuRow>(14);
        foreach (QuickMenuOption option in QuickMenuCatalog.Build(declaredFacts)) {
            if (option.Id == QuickMenuOptionId.GodMode) {
                // New feature owners supply their exact target instead of assuming the host is player zero
                // Legacy supplied views retain their older path; production capture failures cannot use it
                bool matchesPersonalTarget = state.PersonalGodmodeIdentity is { } target
                    ? target == selectedLocalIdentity : !state.PersonalGodmodeRequiresVerifiedTarget
                        && selectedLocalIdentity?.Slot == 0;
                if (!matchesPersonalTarget) {
                    rows.Add(new("God Mode", "UNAVAILABLE", false, new(MenuActionKind.None),
                        "Personal Godmode cannot target this player yet") { SourceChoiceRole = MenuSourceChoiceRole.NonSlider });
                    continue;
                }
                // The supported living target keeps the existing personal capability, value and action guards
                var personal = BasicScriptsMenuRows.PersonalGodmode(state);
                // Known declarations remain visible during a pause, but cannot dispatch its action
                rows.Add(personal with { Enabled = personal.Enabled && observation.ControlsDisabled == false });
                continue;
            }
            string label = option.Id == QuickMenuOptionId.BindNoclip
                ? "Bind Noclip To Lethal Action" : option.Label;
            rows.Add(new(label, "UNAVAILABLE", false, new(MenuActionKind.None), DetailFor(option.Id)) {
                SourceChoiceRole = MenuSourceChoiceRoles.ForQuickOption(option.Id) });
        }
        // Source declarations stay bounded and each view owns its read-only storage
        return rows.AsReadOnly();
    }

    private static bool HasMatchingLocalBinding(QuickMenuEligibility facts, LocalPlayerObservation observation,
        PlayerIdentity? selectedLocalIdentity, int expectedProcessId) {
        if (!observation.IsSupported() || expectedProcessId <= 0 || observation.ProcessId != expectedProcessId
            || selectedLocalIdentity is null || observation.Identity != selectedLocalIdentity
            || observation.HasMenu != true) return false;
        // Neither connection nor writable health can establish the source living or dead branch
        return facts.Life switch {
            QuickPlayerLife.Living => observation.Life == LocalPlayerLife.Alive,
            QuickPlayerLife.Dead => observation.Life == LocalPlayerLife.Dead,
            _ => false
        };
    }

    // Server authority declarations are still inert until their own operations are supported
    private static string DetailFor(QuickMenuOptionId option) => option switch {
        QuickMenuOptionId.Noclip => "Per-player Noclip movement is unavailable",
        QuickMenuOptionId.BindNoclip => "The grenade Noclip control is unavailable",
        QuickMenuOptionId.UnlimitedAmmo => "Personal per-player ammo is unavailable. The separate All Players Menu Infinite Ammo command affects the whole lobby",
        QuickMenuOptionId.UnlimitedEquipment => "Per-player equipment refill is unavailable",
        QuickMenuOptionId.ModifyScore => "Per-player score changes are unavailable",
        QuickMenuOptionId.PerkMenu => "IW perk controls are unavailable",
        QuickMenuOptionId.NoTarget => "Per-player AI targeting changes are unavailable",
        QuickMenuOptionId.ReducedSpread => "Per-player weapon spread changes are unavailable",
        QuickMenuOptionId.UnlimitedSprint => "Per-player unlimited sprint is unavailable",
        QuickMenuOptionId.Respawn => "Player respawning is unavailable",
        QuickMenuOptionId.Revive => "Player revival is unavailable",
        QuickMenuOptionId.RestartGame => "Restarting the match is unavailable",
        QuickMenuOptionId.Disconnect => "Disconnecting through the Quick Menu is unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };
}
