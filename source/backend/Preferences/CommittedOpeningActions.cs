using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Preferences;

// Storage validates tuple structure without inventing verified physical bindings
internal static class CommittedOpeningActions {
    internal static IReadOnlyList<OpeningAction>? Copy(IReadOnlyList<OpeningAction>? input, string parameterName) {
        if (input is null) return null;
        int count = input.Count;
        // Check the bound before using caller indexes or enumerators
        if (count is < 1 or > 3) throw new ArgumentException("A committed opening tuple needs one to three actions", parameterName);
        var copy = new OpeningAction[count];
        ushort seen = 0;
        for (int index = 0; index < count; index++) {
            var action = input[index];
            if (!OpeningActionCatalog.TryFind(action, out _))
                throw new ArgumentException("Unknown committed opening action", parameterName);
            ushort bit = (ushort)(1 << (int)action);
            if ((seen & bit) != 0) throw new ArgumentException("Duplicate committed opening action", parameterName);
            seen |= bit;
            copy[index] = action;
        }
        // The two offhand actions cannot occupy the same source opening combination
        ushort offhands = (1 << (int)OpeningAction.Frag) | (1 << (int)OpeningAction.SecondaryOffhand);
        if ((seen & offhands) == offhands)
            throw new ArgumentException("Conflicting committed offhand actions", parameterName);
        return Array.AsReadOnly(copy);
    }
}
