using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Capture confirms display state without invoking feature changes or cleanup
public sealed partial class PlayerGodmodeRegistry {
    public async Task<GodmodeOperationResult> CaptureAsync(IReadOnlyList<PlayerIdentity> capturedIdentities,
        CancellationToken cancellationToken = default) {
        PlayerIdentity[] captured = CapturedGodmodeRoster.Copy(capturedIdentities);
        if (!CapturedGodmodeRoster.Valid(captured)) {
            return Result(false, false, "Capture one to four distinct, validated player identities", captured);
        }
        bool entered = false;
        try {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (IsClosed()) {
                return Result(false, false, "The godmode registry is closed", captured);
            }
            BeginMetadataOperation(captured);
            return CaptureLocked(captured, cancellationToken);
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

    // Both capture routes publish through the same checks while holding the registry gate
    private GodmodeOperationResult CaptureLocked(PlayerIdentity[] captured, CancellationToken cancellationToken) {
        ValidateCaptured(captured);
        var staged = new List<(Entry Entry, bool New, bool CleanupRequired, GodmodePreflightResult Preflight)>();
        foreach (PlayerIdentity identity in captured) {
            cancellationToken.ThrowIfCancellationRequested();
            Entry? entry;
            bool slotAlreadyBound;
            lock (entriesLock) {
                entries.TryGetValue(identity, out entry);
                slotAlreadyBound = entries.Values.Any(member => member.Identity.Slot == identity.Slot);
            }
            bool isNew = entry is null;
            if (isNew) {
                // Closing a former binding could restore flags, so capture cannot retarget
                if (slotAlreadyBound || HasPendingCleanup()) {
                    return Result(false, false, "Read-only capture cannot replace a binding or bypass pending cleanup", captured);
                }
                entry = new(identity, factory(identity)
                    ?? throw new InvalidOperationException("The player adapter could not be created"));
                lock (entriesLock) {
                    if (entries.Values.Any(member => ReferenceEquals(member.Feature, entry.Feature))
                        || staged.Any(member => ReferenceEquals(member.Entry.Feature, entry.Feature))) {
                        throw new InvalidOperationException("A player adapter cannot be shared between different identities");
                    }
                }
            }
            bool cleanupRequired = PendingCleanup(entry!);
            GodmodePreflightResult preflight = entry!.Feature.Preflight(false);
            if (!preflight.Succeeded || preflight.Value is not (GodmodeValue.On or GodmodeValue.Off or GodmodeValue.Unknown)
                || isNew && preflight.Value == GodmodeValue.Unknown) {
                if (!isNew) {
                    // A rejected current target must not keep a previously confirmed OFF
                    UpdateEntry(entry, false, preflight.FailureReason);
                }
                return Result(false, cancellationToken.IsCancellationRequested,
                    preflight.FailureReason ?? "Player capture could not be confirmed", captured);
            }
            staged.Add((entry, isNew, cleanupRequired, preflight));
        }
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCaptured(captured);
        lock (entriesLock) {
            // Publish the clean capture only after every member and identity succeeds
            foreach (var member in staged) {
                if (member.New) {
                    entries.Add(member.Entry.Identity, member.Entry);
                }
                if (!member.CleanupRequired && member.Preflight.Value is GodmodeValue.On or GodmodeValue.Off) {
                    member.Entry.Known = true;
                    member.Entry.Failure = null;
                }
            }
        }
        return Result(true, false, null, captured);
    }
}
