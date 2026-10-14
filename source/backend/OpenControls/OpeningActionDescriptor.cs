namespace Apparition.Backend.OpenControls;

// Source tokens identify actions, not guessed keyboard or mouse bindings
public sealed record OpeningActionDescriptor(OpeningAction Action, string SourceToken);
