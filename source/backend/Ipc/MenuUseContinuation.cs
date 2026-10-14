namespace Apparition.Backend.Ipc;

// This receipt identifies an admitted invocation, never a feature result or permission
public sealed record MenuUseContinuation(ulong Revision, ulong Sequence, ulong AdmissionRevision, string Kind) {
    public bool IsSupported() => Revision > 0 && Sequence > 0 && AdmissionRevision > 0 && Kind is "plain" or "boolean";
}
