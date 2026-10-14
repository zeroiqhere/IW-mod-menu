namespace Apparition.Backend.Ipc;

// This copied receipt identifies a monitor invocation without granting gameplay permission
public sealed record MenuMonitorContinuation(ulong Revision, ulong Sequence, ulong AdmissionRevision, string Kind) {
    public bool IsSupported() => Revision > 0 && Sequence > 0 && AdmissionRevision > 0
        && Kind is "choice" or "choiceNoop" or "back";
}
