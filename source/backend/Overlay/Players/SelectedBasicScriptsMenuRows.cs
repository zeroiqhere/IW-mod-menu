namespace syndishanx_iw_tool.Overlay.Players;

// Only the Godmode row changes target; the thirty unsupported source rows stay shared
internal static class SelectedBasicScriptsMenuRows {
    internal static IReadOnlyList<MenuRow> Build(OverlayMenuState state) {
        var rows = BasicScriptsMenuRows.Build(state).ToArray();
        var target = state.SelectedPlayerTarget;
        if (!PlayerOptionsMenuRows.HasMatchingTarget(state, target)) {
            rows[0] = Unavailable("A current captured player is required");
        } else if (target!.Route == PlayerMenuRoute.Personal) {
            // A metadata-only self receipt cannot borrow a retained writer for another full identity
            var personal = BasicScriptsMenuRows.PersonalGodmode(state);
            rows[0] = state.PersonalGodmodeIdentity == target.Identity
                ? personal with {
                    Action = personal.Action with {
                        Page = MenuPage.SelectedBasicScripts, Slot = target.Identity.Slot,
                        ExpectedName = target.Identity.Name, ExpectedTarget = target } }
                : Unavailable("The captured personal flags owner is unavailable for this target");
        } else {
            string value = state.Preview || !state.Connected || state.Busy ? "UNKNOWN" : state.TeammateGodmodeValue;
            rows[0] = new("God Mode", value,
                !state.Preview && state.Connected && !state.Busy && state.CanTeammateGodmode,
                new(MenuActionKind.ToggleTeammateGodmode, MenuPage.SelectedBasicScripts,
                    target.Identity.Slot, target.Identity.Name) { ExpectedTarget = target }, state.TeammateGodmodeStatus);
        }
        // Replacing the supported target leaf does not replace its source declaration type
        rows[0] = rows[0] with { SourceChoiceRole = MenuSourceChoiceRole.NonSlider };
        return Array.AsReadOnly(rows);
    }

    private static MenuRow Unavailable(string detail) => new("God Mode", "UNKNOWN", false,
        new(MenuActionKind.None), detail);
}
