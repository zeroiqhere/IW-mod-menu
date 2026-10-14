namespace Apparition.Backend.Controller;

// A source chrome instance survives row redraws and belongs to its admitted actor
internal sealed record LocalMenuChromeState(ulong Revision = 0, object? Owner = null,
    ulong? ReservedRevision = null, object? ReservedOwner = null, bool Ready = false) {
    public LocalMenuChromeState Prepare(object? owner) => owner is null ? Cancel()
        : this with { ReservedRevision = checked(Revision + 1), ReservedOwner = owner, Ready = false };

    // Clock readiness cannot publish an instance before the actual rows commit
    public LocalMenuChromeState MarkReady() => ReservedRevision is null ? this : this with { Ready = true };
    public LocalMenuChromeState ClearReadiness() => this with { Ready = false };
    public LocalMenuChromeState Commit() => Ready && ReservedRevision is ulong revision
        ? new(revision, ReservedOwner) : this;
    public LocalMenuChromeState Cancel() => this with { ReservedRevision = null, ReservedOwner = null, Ready = false };

    public ulong? Read(bool isOpen, bool preview, object? owner) => Revision > 0 && Owner is not null && !preview
        && (!isOpen || ReferenceEquals(Owner, owner)) ? Revision : null;
}
