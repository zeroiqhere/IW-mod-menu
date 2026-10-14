using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Individual changes, batches, and shutdown share one serialized owner
public sealed partial class PlayerGodmodeRegistry {
    private sealed class Entry(PlayerIdentity identity, IPlayerGodmodeFeature feature) {
        public PlayerIdentity Identity { get; } = identity;
        public IPlayerGodmodeFeature Feature { get; } = feature;
        public bool Known { get; set; }
        public bool CleanupRequired { get; set; }
        public bool UnconfirmedStop { get; set; }
        public string? Failure { get; set; }
    }

    private readonly Func<PlayerIdentity, IPlayerGodmodeFeature> factory;
    private readonly Func<PlayerIdentity, bool> validateIdentity;
    private readonly Func<PlayerIdentity, bool> validateDisplayIdentity;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object entriesLock = new();
    private readonly Dictionary<PlayerIdentity, Entry> entries = new();
    private bool closed;
    private PlayerIdentity[] operating = Array.Empty<PlayerIdentity>();

    internal bool IsOperating(PlayerIdentity identity) => Volatile.Read(ref operating).Contains(identity);

    public PlayerGodmodeRegistry(Func<PlayerIdentity, IPlayerGodmodeFeature> factory,
        Func<PlayerIdentity, bool> validateIdentity, Func<PlayerIdentity, bool>? validateDisplayIdentity = null) {
        // Validation must read the captured identity rather than a display name or slot
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.validateIdentity = validateIdentity ?? throw new ArgumentNullException(nameof(validateIdentity));
        this.validateDisplayIdentity = validateDisplayIdentity ?? this.validateIdentity;
    }

    public Task<GodmodeOperationResult> SetAsync(PlayerIdentity identity, bool desired,
        CancellationToken cancellationToken = default) => SetBatchAsync(new[] { identity }, desired, cancellationToken);

    public Task<GodmodeOperationResult> ToggleAsync(PlayerIdentity identity,
        CancellationToken cancellationToken = default) => ApplyCapturedAsync(new[] { identity }, null, cancellationToken, null);

    public Task<GodmodeOperationResult> SetBatchAsync(IReadOnlyList<PlayerIdentity> capturedIdentities,
        bool desired, CancellationToken cancellationToken = default) =>
        ApplyCapturedAsync(capturedIdentities, desired, cancellationToken, null);

    internal Task<GodmodeOperationResult> ToggleWithAdmissionAsync(PlayerIdentity identity,
        Func<bool, bool> admitDesired, CancellationToken cancellationToken = default) =>
        ApplyCapturedAsync(new[] { identity }, null, cancellationToken, admitDesired);

    private async Task<GodmodeOperationResult> ApplyCapturedAsync(IReadOnlyList<PlayerIdentity> capturedIdentities,
        bool? requested, CancellationToken cancellationToken, Func<bool, bool>? admitDesired,
        GodmodeCohortGuard? cohort = null, Func<bool>? admitRollbackEnable = null) {
        PlayerIdentity[] captured = CapturedGodmodeRoster.Copy(capturedIdentities);
        if (!CapturedGodmodeRoster.Valid(captured)) {
            return Result(false, false, "Capture one to four distinct, validated player identities", captured);
        }
        // Membership proof covers absent slots as well as retained member identities
        void ValidatePlan(bool requireEnableEligible) {
            cohort?.Validate(requireEnableEligible);
            ValidateCaptured(captured);
        }
        bool entered = false;
        try {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (IsClosed()) {
                return Result(false, false, "The godmode registry is closed", captured);
            }
            BeginMetadataOperation(captured);
            // Native preflight may wait for an owned monitor read, so return the command task first
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePlan(false);
            // Toggle observes its original member only after acquiring the same gate as batches
            bool desired;
            if (requested.HasValue) desired = requested.Value;
            else if (cohort is null) {
                Entry toggled = await BindAsync(captured.Single()).ConfigureAwait(false);
                desired = !(toggled.Feature.Active || toggled.Feature.HasPendingRestore);
            } else {
                desired = await DetermineCohortDesiredAsync(captured, () => ValidatePlan(false),
                    cancellationToken).ConfigureAwait(false);
            }
            if (cohort is not null) ValidatePlan(desired);
            if (admitDesired is not null && !admitDesired(desired))
                return Result(false, false, "The captured player is unavailable for this action", captured);
            if (desired && HasPendingCleanup()) {
                return Result(false, false, "An earlier change still needs confirmed cleanup", captured);
            }
            var plan = new List<GodmodeBatchMember>();
            foreach (PlayerIdentity identity in captured) {
                cancellationToken.ThrowIfCancellationRequested();
                if (cohort is not null) ValidatePlan(desired);
                Entry entry = await BindAsync(identity).ConfigureAwait(false);
                bool unconfirmedStop;
                bool retainedCleanup;
                lock (entriesLock) {
                    unconfirmedStop = entry.UnconfirmedStop;
                    retainedCleanup = entry.CleanupRequired;
                }
                if (unconfirmedStop || retainedCleanup && !entry.Feature.Active && !entry.Feature.HasPendingRestore) {
                    throw new InvalidOperationException("The original worker or restoration remains unconfirmed; retry close cleanup");
                }
                bool cleanupRequired = PendingCleanup(entry);
                GodmodePreflightResult preflight = entry.Feature.Preflight(desired);
                UpdateEntry(entry, !cleanupRequired && preflight.Succeeded
                    && preflight.Value is GodmodeValue.On or GodmodeValue.Off,
                    preflight.FailureReason);
                if (!preflight.Succeeded || preflight.Value is not (GodmodeValue.On or GodmodeValue.Off or GodmodeValue.Unknown)
                    || desired && preflight.Value == GodmodeValue.Unknown) {
                    return Result(false, false, preflight.FailureReason ?? "Player preflight failed", captured);
                }
                // Unconfirmed earlier changes are cleanup targets, not a prior ON lease
                plan.Add(new(identity, entry.Feature, cleanupRequired ? GodmodeValue.Unknown : preflight.Value));
            }
            // Every target has passed read-only preflight before batch execution
            GodmodeBatchOutcome outcome = await GodmodeBatchCoordinator.ApplyAsync(plan, desired,
                () => ValidatePlan(desired), validateIdentity, (member, known, failure) => {
                    Entry entry;
                    lock (entriesLock) {
                        entry = entries[member.Identity];
                    }
                    UpdateEntry(entry, known, failure);
                }, () => CompleteBatch(captured, desired), cancellationToken,
                admitRollbackEnable).ConfigureAwait(false);
            // A second live projection could contradict an already validated success receipt
            return outcome.Succeeded
                ? new(true, outcome.Cancelled, outcome.FailureReason, outcome.Snapshot!) {
                    OwnerMetadata = ReadMetadataStamp(captured, true)
                }
                : Result(false, outcome.Cancelled, outcome.FailureReason, captured);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return Result(false, true, "The operation was cancelled", captured);
        } catch (Exception exception) {
            return Result(false, cancellationToken.IsCancellationRequested, exception.Message, captured);
        } finally {
            if (entered) {
                Volatile.Write(ref operating, Array.Empty<PlayerIdentity>());
                gate.Release();
            }
        }
    }

    private bool IsClosed() {
        lock (entriesLock) {
            return closed;
        }
    }

    private void UpdateEntry(Entry entry, bool known, string? failure) {
        // Failed cleanup can retain an active worker as well as an owned flag
        bool cleanupRequired = false;
        if (!known) {
            try {
                cleanupRequired = entry.Feature.Active || entry.Feature.HasPendingRestore;
            } catch (Exception) {
                cleanupRequired = true;
            }
        }
        lock (entriesLock) {
            entry.Known = known;
            entry.CleanupRequired = cleanupRequired;
            entry.Failure = failure;
        }
    }

    private void ValidateCaptured(PlayerIdentity[] captured) {
        // Recheck the complete capture before each member can change state
        if (captured.Any(identity => !validateIdentity(identity))) {
            throw new InvalidOperationException("The captured player or match identity changed");
        }
    }

    private GodmodeOperationResult Result(bool succeeded, bool cancelled, string? failure, PlayerIdentity[] captured) =>
        new(succeeded, cancelled, failure, Snapshot(captured)) {
            OwnerMetadata = ReadMetadataStamp(captured, true)
        };
}
