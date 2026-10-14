
namespace Apparition.Backend.Ipc;

// Only a complete supported private packet can enable local presentation controls
internal sealed record SavedBindingCapabilities(bool ChoiceControlsAvailable, bool WidthEditorControlsAvailable,
    bool InstructionPositionEditorControlsAvailable = false, bool MenuPositionEditorControlsAvailable = false) {
    public static SavedBindingCapabilities Unavailable { get; } = new(false, false);
    public const int MaximumBytes = 4096;
    // A launcher request cannot replace the packet's exact saved-action proof
    public bool AllowMenuPositionEditor(bool requested) => requested && MenuPositionEditorControlsAvailable;
    public static SavedBindingCapabilities Parse(ReadOnlySpan<byte> bytes) => SavedControlCapabilities.Parse(bytes).Menu;
}
