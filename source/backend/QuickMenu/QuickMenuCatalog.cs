namespace Apparition.Backend.QuickMenu;

// This inert declaration catalog has no delegates, transports or feature ownership
public static class QuickMenuCatalog {
    private static readonly QuickMenuOption[] Living = [
        new(QuickMenuOptionId.GodMode, "God Mode", QuickMenuOptionKind.Toggle),
        new(QuickMenuOptionId.Noclip, "Noclip", QuickMenuOptionKind.Toggle),
        // Keep the source token until an exact saved-binding caption adapter is available
        new(QuickMenuOptionId.BindNoclip, "Bind Noclip To [{+frag}]", QuickMenuOptionKind.Toggle),
        new(QuickMenuOptionId.UnlimitedAmmo, "Unlimited Ammo", QuickMenuOptionKind.Choice, "Continuous", "Reload", "Disable"),
        new(QuickMenuOptionId.UnlimitedEquipment, "Unlimited Equipment", QuickMenuOptionKind.Toggle),
        new(QuickMenuOptionId.ModifyScore, "Modify Score", QuickMenuOptionKind.Choice,
            "1000000", "100000", "10000", "1000", "100", "10", "0", "-10", "-100", "-1000", "-10000", "-100000", "-1000000"),
        new(QuickMenuOptionId.PerkMenu, "Perk Menu", QuickMenuOptionKind.Submenu),
        new(QuickMenuOptionId.NoTarget, "No Target", QuickMenuOptionKind.Toggle),
        new(QuickMenuOptionId.ReducedSpread, "Reduced Spread", QuickMenuOptionKind.Toggle),
        new(QuickMenuOptionId.UnlimitedSprint, "Unlimited Sprint", QuickMenuOptionKind.Toggle)
    ];
    private static readonly QuickMenuOption Respawn = new(QuickMenuOptionId.Respawn, "Respawn", QuickMenuOptionKind.Action);
    private static readonly QuickMenuOption Revive = new(QuickMenuOptionId.Revive, "Revive", QuickMenuOptionKind.Action);
    private static readonly QuickMenuOption[] Authority = [
        new(QuickMenuOptionId.RestartGame, "Restart Game", QuickMenuOptionKind.Choice, "Full", "Fast"),
        new(QuickMenuOptionId.Disconnect, "Disconnect", QuickMenuOptionKind.Action)
    ];

    public static IReadOnlyList<QuickMenuOption> Build(QuickMenuEligibility facts) {
        ArgumentNullException.ThrowIfNull(facts);
        var rows = new List<QuickMenuOption>(14);
        if (facts.Life == QuickPlayerLife.Living) rows.AddRange(Living);
        // Respawn is declared outside both alive branches; this does not grant permission to open
        rows.Add(Respawn);
        if (facts.Life == QuickPlayerLife.Living) rows.Add(Revive);
        if (facts.HasVerifiedAuthority) rows.AddRange(Authority);
        return Array.AsReadOnly(rows.ToArray());
    }
}
