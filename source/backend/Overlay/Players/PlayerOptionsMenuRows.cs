namespace syndishanx_iw_tool.Overlay.Players;

// Source Options keeps its inventory even when no current target can be browsed
internal static class PlayerOptionsMenuRows {
    private static readonly string[] Labels = { "Verification", "Basic Scripts", "Teleport Menu",
        "Profile Management", "Weaponry", "Bullet Menu", "Fun Scripts", "Model Manipulation",
        "Aimbot Menu", "Model Attachment", "Malicious Options", "Send Message", "Freeze", "Kick" };

    internal static bool HasMatchingTarget(OverlayMenuState state, PlayerMenuTarget? target) => target is not null
        && target.Equals(state.SelectedPlayerTarget) && target.Identity.Slot is >= 0 and <= 3
        && !string.IsNullOrWhiteSpace(target.Identity.Name)
        && target.Identity.Slot == state.SelectedSlot
        && StringComparer.Ordinal.Equals(target.Identity.Name, state.SelectedName);

    internal static IReadOnlyList<MenuRow> Build(OverlayMenuState state) {
        var rows = new List<MenuRow>(Labels.Length);
        var target = state.SelectedPlayerTarget;
        bool valid = HasMatchingTarget(state, target);
        foreach (string label in Labels) {
            if (label == "Basic Scripts" && valid) {
                // Browsing does not require permission to enable a new Godmode change
                rows.Add(new(label, ">", !state.Preview && state.Connected && !state.Busy,
                    new(MenuActionKind.OpenPage, MenuPage.SelectedBasicScripts) { ExpectedTarget = target },
                    "Basic Scripts for the captured player"));
            } else {
                rows.Add(new(label, "UNAVAILABLE", false, new(MenuActionKind.None),
                    label == "Basic Scripts" ? "Select a current player before browsing Basic Scripts"
                        : "This player callback is unavailable in the IW backend"));
            }
        }
        return rows.AsReadOnly();
    }
}
