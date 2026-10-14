namespace Apparition.Backend.Ipc;

// Mechanical source waits describe input admission, never a feature result or permission grant
public sealed record MenuMonitorWait(ulong Revision, string Kind) {
    public bool IsSupported() => Revision > 0 && Kind is "choice" or "choiceNoop" or "back";
}
