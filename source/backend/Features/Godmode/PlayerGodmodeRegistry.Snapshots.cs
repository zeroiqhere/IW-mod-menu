using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Snapshots keep current rosters separate from the full retained cleanup roster
public sealed partial class PlayerGodmodeRegistry {
    public GodmodeRegistrySnapshot Snapshot(IReadOnlyList<PlayerIdentity> capturedIdentities) {
        PlayerIdentity[] captured = CapturedGodmodeRoster.Copy(capturedIdentities);
        if (!CapturedGodmodeRoster.Valid(captured)) {
            captured = Array.Empty<PlayerIdentity>();
        }
        return BuildSnapshot(captured);
    }

    // Render paths may use cached identity evidence; action and completion snapshots remain fresh
    internal GodmodeRegistrySnapshot SnapshotForDisplay(IReadOnlyList<PlayerIdentity> capturedIdentities) {
        PlayerIdentity[] captured = CapturedGodmodeRoster.Copy(capturedIdentities);
        return BuildSnapshot(CapturedGodmodeRoster.Valid(captured) ? captured : Array.Empty<PlayerIdentity>(), true);
    }

    private GodmodeRegistrySnapshot BuildSnapshot(PlayerIdentity[] captured, bool forDisplay = false) {
        Entry[] owned;
        bool isClosed;
        lock (entriesLock) {
            owned = entries.Values.ToArray();
            isClosed = closed;
        }
        var members = new List<PlayerGodmodeSnapshot>(captured.Length);
        foreach (PlayerIdentity identity in captured) {
            Entry? entry = owned.FirstOrDefault(member => member.Identity == identity);
            members.Add(entry is null ? new(identity, GodmodeValue.Unknown, false, false, false, null)
                : ReadSnapshot(entry, forDisplay));
        }
        GodmodeValue summary = members.Count == 0 || members.Any(member => member.Value == GodmodeValue.Unknown)
            ? GodmodeValue.Unknown : members.All(member => member.Value == GodmodeValue.On) ? GodmodeValue.On
            : members.All(member => member.Value == GodmodeValue.Off) ? GodmodeValue.Off : GodmodeValue.Mixed;
        // No mutable collection or adapter escapes through the published view
        return new(summary, Array.AsReadOnly(members.ToArray()), owned.Any(entry => PendingCleanup(entry, forDisplay)), isClosed);
    }

    private PlayerGodmodeSnapshot ReadSnapshot(Entry entry, bool forDisplay = false) {
        try {
            bool connected = entry.Feature.Connected;
            bool active = entry.Feature.Active;
            bool pending = entry.Feature.HasPendingRestore;
            bool known;
            string? failure;
            lock (entriesLock) {
                known = entry.Known && !entry.UnconfirmedStop && !entry.CleanupRequired;
                failure = entry.Failure;
            }
            GodmodePreflightResult? observed = connected && known && (forDisplay ? validateDisplayIdentity : validateIdentity)(entry.Identity)
                ? entry.Feature.Preflight(false) : null;
            // A fresh foreign flag or a stopped owner cannot keep an old OFF display
            GodmodeValue value = observed is null || !observed.Succeeded || !active && pending ? GodmodeValue.Unknown
                : active && observed.Value == GodmodeValue.On ? GodmodeValue.On
                : !active && observed.Value == GodmodeValue.Off ? GodmodeValue.Off : GodmodeValue.Unknown;
            return new(entry.Identity, value, connected, active, pending,
                observed?.FailureReason ?? failure ?? entry.Feature.FailureReason);
        } catch (Exception exception) {
            return new(entry.Identity, GodmodeValue.Unknown, false, false, true, exception.Message);
        }
    }

    private bool PendingCleanup(Entry entry, bool forDisplay = false) {
        try {
            bool retainedCleanup;
            lock (entriesLock) {
                retainedCleanup = entry.UnconfirmedStop || entry.CleanupRequired;
            }
            if (retainedCleanup) {
                return true;
            }
            bool active = entry.Feature.Active;
            bool pending = entry.Feature.HasPendingRestore;
            // A changed owned identity blocks new work before its monitor next runs
            return !active && pending || (active || pending) && !(forDisplay ? validateDisplayIdentity : validateIdentity)(entry.Identity);
        } catch (Exception) {
            return true;
        }
    }

    private bool HasPendingCleanup() {
        Entry[] owned;
        lock (entriesLock) {
            owned = entries.Values.ToArray();
        }
        return owned.Any(entry => PendingCleanup(entry));
    }
}
