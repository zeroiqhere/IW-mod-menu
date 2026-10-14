namespace Apparition.Backend.OpenControls;

// This fixed inventory keeps source order separate from verified availability
public static class OpeningActionCatalog {
    private static readonly IReadOnlyList<OpeningActionDescriptor> items = Array.AsReadOnly(new[] {
        new OpeningActionDescriptor(OpeningAction.ActionSlot1, "+actionslot 1"),
        new OpeningActionDescriptor(OpeningAction.ActionSlot2, "+actionslot 2"),
        new OpeningActionDescriptor(OpeningAction.ActionSlot3, "+actionslot 3"),
        new OpeningActionDescriptor(OpeningAction.ActionSlot4, "+actionslot 4"),
        new OpeningActionDescriptor(OpeningAction.Melee, "+melee"),
        new OpeningActionDescriptor(OpeningAction.Aim, "+speed_throw"),
        new OpeningActionDescriptor(OpeningAction.Attack, "+attack"),
        new OpeningActionDescriptor(OpeningAction.Sprint, "+breath_sprint"),
        new OpeningActionDescriptor(OpeningAction.Use, "+activate"),
        new OpeningActionDescriptor(OpeningAction.Frag, "+frag"),
        new OpeningActionDescriptor(OpeningAction.SecondaryOffhand, "+smoke"),
        new OpeningActionDescriptor(OpeningAction.Stance, "+stance"),
        new OpeningActionDescriptor(OpeningAction.Jump, "+gostand")
    });

    public static IReadOnlyList<OpeningActionDescriptor> Items => items;

    public static bool TryFind(OpeningAction action, out OpeningActionDescriptor? descriptor) {
        int index = (int)action;
        // Check the full ID before indexing or shifting a capability bit
        if (index < 0 || index >= items.Count) {
            descriptor = null;
            return false;
        }
        descriptor = items[index];
        return true;
    }
}
