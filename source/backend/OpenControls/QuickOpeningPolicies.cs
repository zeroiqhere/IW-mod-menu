namespace Apparition.Backend.OpenControls;

// One admission epoch protects the two independently configured opening policies
public sealed class QuickOpeningPolicies {
    private QuickOpeningPolicies(MainOpeningChord main, QuickOpeningChord quick,
        ulong revision, bool quickDisabled) {
        Main = main;
        Quick = quick;
        Revision = revision;
        QuickDisabled = quickDisabled;
    }

    public MainOpeningChord Main { get; }
    public QuickOpeningChord Quick { get; }
    public ulong Revision { get; }
    public bool QuickDisabled { get; }

    public static bool TryCreateDefault(VerifiedOpeningActions? capabilities,
        out QuickOpeningPolicies? policies, out OpeningControlFailure failure) {
        policies = null;
        if (!MainOpeningChord.TryCreateDefault(capabilities, out var main, out failure)) return false;
        if (!QuickOpeningChord.TryCreateDefault(capabilities, out var quick, out failure)) return false;
        // Failure leaves no partially created pair, including when only Main is verified
        return TryCreate(main, quick, 1, false, out policies, out failure);
    }

    public static bool TryCreate(MainOpeningChord? main, QuickOpeningChord? quick,
        ulong revision, bool quickDisabled, out QuickOpeningPolicies? policies,
        out OpeningControlFailure failure) {
        policies = null;
        failure = OpeningControlFailure.InvalidRevision;
        if (revision == 0) return false;
        failure = OpeningControlFailure.InvalidChord;
        if (main is null || quick is null) return false;
        // Each immutable tuple was validated independently, so overlap between families is allowed
        policies = new QuickOpeningPolicies(main, quick, revision, quickDisabled);
        failure = OpeningControlFailure.None;
        return true;
    }

    public bool TrySetMainSlot(ulong expectedRevision, int slot, OpeningAction? action,
        VerifiedOpeningActions? capabilities, out QuickOpeningPolicies updated,
        out OpeningControlFailure failure) {
        updated = this;
        if (!TryAdmit(expectedRevision, out failure)) return false;
        if (!Main.TrySetSlot(Main.Revision, slot, action, capabilities, out var prepared, out failure)) return false;
        return TryCommit(prepared, Quick, QuickDisabled, out updated, out failure);
    }

    public bool TrySetQuickSlot(ulong expectedRevision, int slot, OpeningAction? action,
        VerifiedOpeningActions? capabilities, out QuickOpeningPolicies updated,
        out OpeningControlFailure failure) {
        updated = this;
        if (!TryAdmit(expectedRevision, out failure)) return false;
        if (!Quick.TrySetSlot(Quick.Revision, slot, action, capabilities, out var prepared, out failure)) return false;
        return TryCommit(Main, prepared, QuickDisabled, out updated, out failure);
    }

    public bool TrySetQuickDisabled(ulong expectedRevision, bool disabled,
        out QuickOpeningPolicies updated, out OpeningControlFailure failure) {
        updated = this;
        if (!TryAdmit(expectedRevision, out failure)) return false;
        // Disabling Quick changes its local preference, without discarding its configured tuple
        return TryCommit(Main, Quick, disabled, out updated, out failure);
    }

    private bool TryAdmit(ulong expectedRevision, out OpeningControlFailure failure) {
        failure = OpeningControlFailure.StaleRevision;
        if (expectedRevision != Revision) return false;
        failure = OpeningControlFailure.None;
        return true;
    }

    private bool TryCommit(MainOpeningChord main, QuickOpeningChord quick, bool disabled,
        out QuickOpeningPolicies updated, out OpeningControlFailure failure) {
        updated = this;
        failure = OpeningControlFailure.None;
        // No-op tuple edits and preference reselects do not need a new admission epoch
        if (ReferenceEquals(main, Main) && ReferenceEquals(quick, Quick) && disabled == QuickDisabled) return true;
        failure = OpeningControlFailure.RevisionExhausted;
        if (Revision == ulong.MaxValue) return false;

        // A prepared tuple stays private until the global revision can commit with it
        updated = new QuickOpeningPolicies(main, quick, checked(Revision + 1), disabled);
        failure = OpeningControlFailure.None;
        return true;
    }
}
