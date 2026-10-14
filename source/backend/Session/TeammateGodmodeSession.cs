using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Apparition.Backend.Session;

// Display metadata never owns a writer or replaces the captured cleanup identity
public sealed record SelectedGodmodeView(PlayerIdentity? Identity, GodmodeValue Value,
    string Status, bool CanSelect, bool CanToggle, bool HasPendingCleanup) {
    // Retained display identity alone cannot confirm a current owned target
    public bool HasCurrentIdentity { get; init; }
}

// Selection and actions share one gate while the registry retains every owned player
public sealed partial class TeammateGodmodeSession {
    private readonly Func<int, PlayerState> read;
    private readonly PlayerGodmodeRegistry registry;
    private readonly bool ownsRegistry = true;
    private readonly Func<PlayerIdentity, bool>? isPersonalOwner;
    private readonly SemaphoreSlim gate = new(1, 1);
    private PlayerIdentity? selected;
    private string status = "Select a validated teammate";
    private int closing;

    public TeammateGodmodeSession(Func<int, PlayerState> read,
        Func<PlayerIdentity, IPlayerGodmodeFeature> bind,
        Func<PlayerIdentity, OwnedPlayerFlagsState>? readOwned = null) {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        ArgumentNullException.ThrowIfNull(bind);
        this.readOwned = readOwned;
        registry = new(bind, MatchesRetainedIdentity);
    }

    // A connection owns the shared registry; this service retains only peer selection
    internal TeammateGodmodeSession(Func<int, PlayerState> read, PlayerGodmodeRegistry registry,
        Func<PlayerIdentity, OwnedPlayerFlagsState>? readOwned, Func<PlayerIdentity, bool> isPersonalOwner) {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.readOwned = readOwned;
        this.isPersonalOwner = isPersonalOwner;
        ownsRegistry = false;
    }

    public async Task<bool> SelectAsync(int slot, string expectedName) {
        await gate.WaitAsync().ConfigureAwait(false);
        bool refreshNeeded = false;
        try {
            if (Volatile.Read(ref closing) != 0) return Reject("The teammate session is closed");
            if (!ValidPayload(slot, expectedName)) return Reject("Select one current teammate in slots 0 through 3");
            // A failed cleanup must retain its original selection and cannot bind a replacement
            refreshNeeded = true;
            GodmodeRegistrySnapshot owned = registry.Snapshot(Array.Empty<PlayerIdentity>());
            if (owned.HasPendingCleanup) return Reject("An earlier change needs confirmed cleanup before selection");
            PlayerIdentity identity = ReadIdentity(slot, expectedName);
            if (isPersonalOwner?.Invoke(identity) == true)
                return Reject("This player already has a Godmode owner; use the personal controls");
            GodmodeOperationResult capture = await registry.CaptureAsync(new[] { identity }).ConfigureAwait(false);
            if (!capture.Succeeded) return Reject(capture.FailureReason ?? "Player state could not be captured");
            if (Volatile.Read(ref closing) != 0) return Reject("The teammate session is closing");
            // Switching metadata never closes another active registry member
            Volatile.Write(ref selected, identity);
            Volatile.Write(ref status, "The selected teammate state was captured without changing flags");
            return true;
        } catch (Exception exception) {
            return Reject(exception.Message);
        } finally {
            try { if (refreshNeeded) RefreshPublication(); }
            finally { gate.Release(); }
        }
    }

    public async Task<GodmodeOperationResult?> ToggleAsync(int slot, string expectedName) {
        await gate.WaitAsync().ConfigureAwait(false);
        bool refreshNeeded = false;
        try {
            if (Volatile.Read(ref closing) != 0) { Reject("The teammate session is closed"); return null; }
            PlayerIdentity? identity = Volatile.Read(ref selected);
            if (!ValidPayload(slot, expectedName) || identity is null || identity.Slot != slot
                || !StringComparer.Ordinal.Equals(identity.Name, expectedName)) {
                Reject("The action does not match the captured current teammate");
                return null;
            }
            refreshNeeded = true;
            // Desired state is chosen inside the common registry gate, after any queued batch
            if (!MatchesRetainedIdentity(identity)) {
                Reject("The captured teammate is unavailable for this action");
                return null;
            }
            bool denied = false;
            GodmodeOperationResult result = await registry.ToggleWithAdmissionAsync(identity, desired => {
                denied = desired && !MatchesCurrentIdentity(identity);
                return !denied;
            }).ConfigureAwait(false);
            if (denied) { Reject("The captured teammate is unavailable for this action"); return null; }
            bool enabled = result.Snapshot.Members.Single().Value == GodmodeValue.On;
            Volatile.Write(ref status, result.FailureReason ?? (result.Succeeded
                ? enabled ? "Teammate Godmode enabled" : "The owned teammate flag was restored"
                : "The teammate change could not be confirmed"));
            return result;
        } catch (Exception exception) {
            Reject(exception.Message);
            return null;
        } finally {
            try { if (refreshNeeded) RefreshPublication(); }
            finally { gate.Release(); }
        }
    }

    internal void RequestClose() => Interlocked.Exchange(ref closing, 1);

    public async Task<GodmodeRegistryCloseResult> CloseAsync() {
        // Reject queued actions as soon as shutdown starts, before waiting for the gate
        RequestClose();
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            return ownsRegistry ? await registry.CloseAsync().ConfigureAwait(false)
                : new(true, true, registry.SnapshotForDisplay(Array.Empty<PlayerIdentity>()));
        } finally {
            gate.Release();
        }
    }

    private PlayerIdentity ReadIdentity(int slot, string expectedName) {
        lock (proofGate) {
            // Retained identities are selection hints only; their own reader must confirm them
            GodmodeMetadataStamp owners = registry.ReadAllOwnerMetadata();
            PlayerIdentity[] retained = owners.Members
                .Where(member => (member.Active || member.Pending) && member.Identity.Slot == slot
                    && StringComparer.Ordinal.Equals(member.Identity.Name, expectedName))
                .Select(member => member.Identity).ToArray();
            if (retained.Length > 1)
                throw new InvalidOperationException("More than one retained teammate matches this selection");
            if (retained.Length == 1) {
                // A failed retained read cannot fall through into a replacement capture
                if (!MatchesRetainedIdentity(retained[0]))
                    throw new InvalidOperationException("The original teammate proof could not be confirmed");
                return retained[0];
            }

            // A new owner needs a complete living sample from the strict reader
            PlayerState state = read(slot) ?? throw new InvalidOperationException("The teammate state is missing");
            if (state.Identity.Slot != slot || !StringComparer.Ordinal.Equals(state.Identity.Name, expectedName)
                || state.CommandTime < 0 || state.Health is <= 1 or > 10000)
                throw new InvalidOperationException("The teammate name, slot or living state changed");
            if (readOwned is not null && !ObserveCommandTime(state.Identity, state.CommandTime)) {
                // The actual selection sample is the first monotonic-time boundary for owned cleanup
                throw new InvalidOperationException("The original teammate proof was retired");
            }
            return state.Identity;
        }
    }

    private bool MatchesCurrentIdentity(PlayerIdentity identity) => MatchesCurrentIdentity(identity, out _);

    private bool MatchesCurrentIdentity(PlayerIdentity identity, out bool knownDead) {
        knownDead = false;
        lock (proofGate) {
            try {
                if (!ValidPayload(identity.Slot, identity.Name)) return false;
                PlayerState state = read(identity.Slot)
                    ?? throw new InvalidOperationException("The teammate state is missing");
                if (state.Identity != identity || state.CommandTime < 0) {
                    // A returned replacement or invalid timestamp retires the expected owner
                    if (readOwned is not null) RetireIdentity(identity);
                    return false;
                }
                if (readOwned is not null && !ObserveCommandTime(identity, state.CommandTime)) return false;
                // A dead observation is distinct from a replacement, but proves no writable state
                knownDead = state.Health is >= 0 and <= 1;
                return state.Health is > 1 and <= 10000;
            } catch (Exception) {
                // A strict living reader may reject death without invalidating cleanup ownership
                return false;
            }
        }
    }

    private static bool ValidPayload(int slot, string? name) => slot is >= 0 and <= 3
        && !string.IsNullOrWhiteSpace(name);

    private bool Reject(string reason) {
        // Report a failed command without dropping the original selected cleanup owner
        Volatile.Write(ref status, reason);
        // Rejected payloads change the notice without starting a native refresh
        var copied = Volatile.Read(ref publication);
        if (copied is not null) Volatile.Write(ref publication,
            copied with { View = copied.View with { Status = reason } });
        return false;
    }
}
