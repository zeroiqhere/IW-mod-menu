using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Ipc;

// The descriptor exposes validated logical configuration, without opening authorization
public sealed class OpeningPoliciesState {
    private OpeningPoliciesState(QuickOpeningPolicies policies) {
        Revision = policies.Revision;
        Main = new TupleState(policies.Main.Revision, policies.Main.Actions);
        Quick = new TupleState(policies.Quick.Revision, policies.Quick.Actions);
        QuickDisabled = policies.QuickDisabled;
    }

    public ulong Revision { get; }
    public TupleState Main { get; }
    public TupleState Quick { get; }
    public bool QuickDisabled { get; }

    public static OpeningPoliciesState FromPolicies(QuickOpeningPolicies policies) {
        ArgumentNullException.ThrowIfNull(policies);
        // Each tuple already owns its bounded read-only action copy
        return new(policies);
    }

    internal bool MatchesMain(MainOpeningControlsState? mirror) => mirror is not null
        && Main.Revision == mirror.Revision && Main.Actions.SequenceEqual(mirror.Actions);

    // Nested tuples retain their own revision while the outer revision admits either family's edits
    public sealed class TupleState {
        internal TupleState(ulong revision, IReadOnlyList<OpeningAction> actions) {
            Revision = revision;
            Actions = actions;
        }

        public ulong Revision { get; }
        public IReadOnlyList<OpeningAction> Actions { get; }
    }
}
