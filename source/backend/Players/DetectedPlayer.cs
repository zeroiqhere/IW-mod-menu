namespace syndishanx_iw_tool.Players;

// A display label is not permission to write a player's health
public sealed record DetectedPlayer(int Slot, string Name) {
    public string DisplayName => $"Player {Slot + 1} · {Name}";
}
