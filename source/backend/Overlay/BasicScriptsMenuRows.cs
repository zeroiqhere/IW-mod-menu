namespace syndishanx_iw_tool.Overlay;

// Reference declarations remain visible without granting unsupported engine callbacks
internal static class BasicScriptsMenuRows {
    internal static IReadOnlyList<MenuRow> Build(OverlayMenuState state) {
        ArgumentNullException.ThrowIfNull(state);
        // Source order is fixed; only the existing personal Godmode capability has a handler
        var rows = new List<MenuRow>(31) {
            PersonalGodmode(state),
            Unavailable("Demi-God", "Partial damage protection is unavailable"),
            Unavailable("Noclip", "Per-player Noclip movement is unavailable"),
            // The source resolves its grenade token; an unavailable row needs no guessed physical key
            Unavailable("Bind Noclip To Lethal Action", "The grenade Noclip control is unavailable"),
            Unavailable("UFO Mode", "Per-player flying movement is unavailable"),
            Unavailable("Unlimited Ammo", "Personal per-player ammo is unavailable. The separate All Players Menu Infinite Ammo command affects the whole lobby"),
            Unavailable("Unlimited Equipment", "Per-player equipment refill is unavailable"),
            Unavailable("Unlimited Special Weapon", "Special weapon charge refill is unavailable"),
            Unavailable("Modify Score", "Per-player score changes are unavailable"),
            // These links require real map data and callbacks before they can become submenus
            Unavailable("Perk Menu", "IW perk controls are unavailable"),
            Unavailable("Gobblegum Menu", "This BO3 Gobblegum submenu is unavailable in IW"),
            Unavailable("Movement Speed", "Per-player movement speed is unavailable"),
            Unavailable("Third Person", "Per-player camera changes are unavailable"),
            Unavailable("Invisibility", "Per-player invisibility is unavailable"),
            Unavailable("Clone", "Player model cloning is unavailable"),
            Unavailable("No Target", "Per-player AI targeting changes are unavailable"),
            Unavailable("Reduced Spread", "Per-player weapon spread changes are unavailable"),
            Unavailable("Multi-Jump", "Per-player extra jumps are unavailable"),
            Unavailable("Disable HUD", "Game HUD visibility changes are unavailable"),
            Unavailable("Visual Effects", "Map visual effects are unavailable"),
            Unavailable("Set Vision", "Per-player vision effects are unavailable"),
            Unavailable("Zombie Charms", "BO3 Zombie Charms are unavailable in IW"),
            Unavailable("Custom Crosshairs", "Per-player custom crosshairs are unavailable"),
            Unavailable("No Explosive Damage", "Per-player explosive damage protection is unavailable"),
            Unavailable("Character Model Index", "Player character model changes are unavailable"),
            Unavailable("Random Character Model Index", "Random player character models are unavailable"),
            Unavailable("Shoot While Sprinting", "Shooting while sprinting is unavailable"),
            Unavailable("Unlimited Sprint", "Per-player unlimited sprint is unavailable"),
            Unavailable("Respawn", "Player respawning is unavailable"),
            Unavailable("Revive", "Player revival is unavailable"),
            Unavailable("Death", "Player down and kill actions are unavailable")
        };
        // Unsupported sliders keep their source branch shape without any applied choice value
        for (int index = 0; index < rows.Count; index++)
            rows[index] = rows[index] with { SourceChoiceRole = MenuSourceChoiceRoles.ForBasicScripts(index) };
        // Each view receives its own bounded read-only storage
        return rows.AsReadOnly();
    }

    internal static MenuRow PersonalGodmode(OverlayMenuState state) {
        ArgumentNullException.ThrowIfNull(state);
        // Existing session guards stay intact, independent of a selected teammate or lobby state
        string value = state.Preview || !state.Connected || state.Busy ? "UNKNOWN" : state.PersonalGodmodeValue;
        bool recovery = !state.CanPersonalGodmode && state.CanRestorePersonalGodmode;
        bool enabled = !state.Preview && !state.Busy && state.Connected
            && (state.CanPersonalGodmode || recovery);
        // A recovery selection is a pure OFF request, never an ambiguous toggle
        return new("God Mode", value, enabled,
            new(recovery ? MenuActionKind.RestorePersonalGodmode : MenuActionKind.TogglePersonalGodmode),
            (recovery ? "Retry turning personal Godmode OFF. " : "Your personal Godmode. ") + state.PersonalGodmodeStatus) { SourceChoiceRole = MenuSourceChoiceRole.NonSlider };
    }

    // Unsupported booleans and choices have no invented applied value or dispatch intent
    private static MenuRow Unavailable(string label, string detail) =>
        new(label, "UNAVAILABLE", false, new(MenuActionKind.None), detail);
}
