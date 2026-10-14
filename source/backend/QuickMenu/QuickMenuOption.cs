namespace Apparition.Backend.QuickMenu;

// These IDs identify source declarations and never identify a game callback
public enum QuickMenuOptionId { GodMode, Noclip, BindNoclip, UnlimitedAmmo, UnlimitedEquipment, ModifyScore, PerkMenu, NoTarget, ReducedSpread, UnlimitedSprint, Respawn, Revive, RestartGame, Disconnect }
public enum QuickMenuOptionKind { Toggle, Choice, Submenu, Action }
public sealed class QuickMenuOption {
    public QuickMenuOptionId Id { get; }
    public string Label { get; }
    public QuickMenuOptionKind Kind { get; }
    public IReadOnlyList<string> Choices { get; }
    public bool IsAvailable => false;
    public bool CanDispatch => false;
    internal QuickMenuOption(QuickMenuOptionId id, string label, QuickMenuOptionKind kind, params string[] choices) {
        Id = id;
        Label = label;
        Kind = kind;
        // No array supplied to a descriptor can later change its source choices
        Choices = Array.AsReadOnly((string[])choices.Clone());
    }
}
