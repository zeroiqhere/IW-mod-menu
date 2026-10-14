namespace Apparition.Backend.Ipc;

// Keep existing callers on the single complete-packet validator
internal static class ChoiceBindingAvailability {
    public const int MaximumBytes = SavedBindingCapabilities.MaximumBytes;
    public static bool Parse(ReadOnlySpan<byte> bytes) => SavedBindingCapabilities.Parse(bytes).ChoiceControlsAvailable;
}
