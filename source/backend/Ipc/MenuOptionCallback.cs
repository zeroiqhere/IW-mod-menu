namespace Apparition.Backend.Ipc;

// This descriptor describes the monitor wait and never confirms a gameplay result
public sealed record MenuOptionCallback(ulong Revision, string Kind, string Phase) {
    public bool IsSupported() => Revision > 0 && (Kind == "boolean" && Phase is "pending" or "cooldown"
        || Kind == "plain" && Phase == "cooldown");
}
