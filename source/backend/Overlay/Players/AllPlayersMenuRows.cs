namespace syndishanx_iw_tool.Overlay.Players;

// Only the verified whole-cohort callback can activate a source all-player row
internal static class AllPlayersMenuRows {
    private const string MissingCohortFallback = "A complete verified lobby observation is required.";
    private const string IndividualGodmodeRoute =
        "Individual God Mode: Basic Scripts (self) or Players Menu > select player > Basic Scripts (teammate).";
    private static readonly string[] Labels = { "Verification", "Teleport", "Profile Management",
        "Model Manipulation", "Malicious Options", "God Mode", "Send Message", "Kick", "Down", "Revive", "Respawn" };

    internal static IReadOnlyList<MenuRow> Build(OverlayMenuState state) {
        var rows = new List<MenuRow>(Labels.Length + 1);
        foreach (string label in Labels) {
            if (label == "God Mode" && !state.Preview && state.Connected
                && state.AllPlayersGodmodeTarget is { } target) {
                // Polling or life ineligibility disables input without retiring the supported source row
                string godmodeValue = state.Busy ? "UNKNOWN" : state.AllPlayersGodmodeValue;
                if (godmodeValue is not ("ON" or "OFF" or "UNKNOWN")) godmodeValue = "UNKNOWN";
                rows.Add(new(label, godmodeValue, !state.Busy && state.CanAllPlayersGodmode
                    && godmodeValue is "ON" or "OFF",
                    new(MenuActionKind.ToggleAllPlayersGodmode, MenuPage.AllPlayers) { ExpectedCohort = target },
                    state.AllPlayersGodmodeStatus));
                continue;
            }
            rows.Add(new(label, "UNAVAILABLE", false, new(MenuActionKind.None),
                label == "God Mode" ? UnavailableGodmodeDetail(state)
                    : "This all-player callback is unavailable in the IW backend"));
        }
        // The existing lobby-wide IW patch remains visibly separate from source personal ammo
        string value = state.Preview || !state.Connected || state.Busy ? "UNKNOWN" : state.LobbyAmmoValue;
        rows.Add(new("Infinite Ammo (IW lobby)", value,
            !state.Preview && state.Connected && !state.Busy && state.CanLobbyAmmo,
            new(MenuActionKind.ToggleLobbyAmmo), state.LobbyAmmoStatus));
        return rows.AsReadOnly();
    }

    private static string UnavailableGodmodeDetail(OverlayMenuState state) {
        // A cached session reason is current only in a connected, unpaused gameplay view
        string status = !state.Preview && state.Connected && !state.Busy
            && !string.IsNullOrWhiteSpace(state.AllPlayersGodmodeStatus)
            ? state.AllPlayersGodmodeStatus : MissingCohortFallback;
        // Put the recovery route first so state publication limits cannot hide it
        return IndividualGodmodeRoute + "\n" + status;
    }
}
