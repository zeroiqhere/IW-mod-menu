using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

// Creation counts final owner-observed HUD instances, not unobserved GSC coroutine ticks
internal sealed record LocalInstructionHudLifetime(ulong Counter = 0, bool Declared = false,
    object? Owner = null, LocalPlayerLife PreviousLife = LocalPlayerLife.Unknown,
    InstructionHudState? Created = null, ulong? ReservedRevision = null, object? ReservedOwner = null,
    bool RecreationReady = false) {
    public LocalInstructionHudLifetime Request(object? owner, bool ready) {
        if (owner is null) return this;
        // Repeated preparation retains one future instance without spending its counter
        ulong revision = ReservedRevision is { } reserved && ReferenceEquals(owner, ReservedOwner)
            ? reserved : checked(Counter + 1);
        return this with { ReservedRevision = revision, ReservedOwner = owner, RecreationReady = ready };
    }

    public LocalInstructionHudLifetime MarkReady() => ReservedRevision is null ? this : this with { RecreationReady = true };
    public LocalInstructionHudLifetime CancelPendingRecreation() => this with {
        ReservedRevision = null, ReservedOwner = null, RecreationReady = false
    };
    public LocalInstructionHudLifetime Retire() => this with {
        Owner = null, PreviousLife = LocalPlayerLife.Unknown, Created = null,
        ReservedRevision = null, ReservedOwner = null, RecreationReady = false
    };

    public (LocalInstructionHudLifetime State, InstructionHudState? Frame, bool Withdrawn) Prepare(
        LocalMenuAdmissionSnapshot? admission, MenuAppearance appearance, bool preview, bool production, bool stopped,
        string accent, bool rainbow) {
        bool declared = Declared || production || admission is not null;
        var retained = this with { Declared = declared };
        if (preview) return (retained.Retire(), null, false);
        if (!declared) return (retained.Retire(), null, false);
        if (stopped || !appearance.InstructionsVisible) return (retained.Retire(), InstructionHudState.Absent, false);
        // Unknown is missing current life proof, not an observed source death or owner replacement
        bool withdrawal = admission is not null && admission.Observation.HasMenu == true
            && admission.Observation.Life == LocalPlayerLife.Unknown && Created is not null
            && ReferenceEquals(Owner, admission.OwnerToken);
        if (withdrawal) return (retained, InstructionHudState.Absent, true);
        if (admission is null || admission.Observation.HasMenu != true
            || admission.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)) {
            // Missing owner, revoked permission and unsupported proof cannot retain another source lifetime
            return (retained.Retire(), InstructionHudState.Absent, false);
        }

        bool sameOwner = ReferenceEquals(Owner, admission.OwnerToken);
        bool pending = RecreationReady && ReferenceEquals(ReservedOwner, admission.OwnerToken);
        bool death = sameOwner && PreviousLife == LocalPlayerLife.Alive && admission.Observation.Life == LocalPlayerLife.Dead;
        bool create = !sameOwner || Created is null || pending || death;
        ulong revision = create ? pending && ReservedRevision is { } reserved ? reserved : checked(Counter + 1) : Counter;
        bool delayedCause = !RecreationReady && ReservedRevision is not null
            && ReferenceEquals(ReservedOwner, admission.OwnerToken);
        // An earlier observed creation needs its own ID and a distinct, still-reservable future ID
        ulong? remainingRevision = create && delayedCause ? checked(revision + 1) : null;
        // Rainbow captures the fixed configuration fallback plus its creation mode, not a sampled RGB
        var frame = create ? new InstructionHudState(true, revision, admission.Revision,
            appearance.Design, appearance.AlternateInstructions, appearance.InstructionsBackground, accent, rainbow)
            : Created! with { AdmissionRevision = admission.Revision };
        var prepared = retained with { Counter = revision, Owner = admission.OwnerToken,
            PreviousLife = admission.Observation.Life, Created = frame };
        if (create) prepared = prepared with {
            ReservedRevision = remainingRevision,
            ReservedOwner = remainingRevision is null ? null : admission.OwnerToken,
            RecreationReady = false
        };
        return (prepared, frame, false);
    }
}
