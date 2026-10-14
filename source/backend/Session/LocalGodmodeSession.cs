using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// Local metadata retains the entire account and actor binding, never a guessed local slot
public sealed record LocalGodmodeView(LocalAccountSessionBinding? Binding, PlayerIdentity? Identity,
    GodmodeValue Value, bool CanToggle, bool HasPendingCleanup, string Status) {
    // A failed candidate identity is not yet a retained flag writer or recovery capability
    public bool HasBoundTarget { get; init; }
    public bool CanRestore { get; init; }
}
public sealed record LocalGodmodeCloseResult(bool Stopped, bool Restored, string? ErrorMessage);

// One connection owns one binding and the existing flag feature's restoration lifetime
public sealed partial class LocalGodmodeSession {
    private readonly Func<LocalActorFlagsSnapshot> readCurrent;
    private readonly Func<LocalActorFlagsSnapshot, ITeammateFlagsTransport> bind;
    private readonly TimeSpan monitorInterval;
    private readonly SemaphoreSlim gate = new(1, 1);
    private LocalGodmodeView view = new(null, null, GodmodeValue.Unknown, false, false, "Not captured");
    private LocalAccountSessionBinding? binding;
    private OwnedGodmodeFlagsTransport? transport;
    private TeammateGodmodeFeature? feature;
    private int lastCommandTime;
    private int closing;
    private int operating;
    private int invalidated;
    private int closingOperations;
    private bool unconfirmedStop;

    public LocalGodmodeSession(Func<LocalActorFlagsSnapshot> readCurrent,
        Func<LocalActorFlagsSnapshot, ITeammateFlagsTransport> bind, TimeSpan? monitorInterval = null) {
        this.readCurrent = readCurrent ?? throw new ArgumentNullException(nameof(readCurrent));
        this.bind = bind ?? throw new ArgumentNullException(nameof(bind));
        this.monitorInterval = monitorInterval ?? TimeSpan.FromMilliseconds(50);
        if (this.monitorInterval.TotalMilliseconds is < 1 or > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(monitorInterval));
        }
    }

    // Frame publication reads only cached metadata and the feature's volatile ownership flags
    public LocalGodmodeView ReadView() {
        var current = Volatile.Read(ref view);
        var owner = Volatile.Read(ref feature);
        bool stoppedOn = current.Value == GodmodeValue.On && owner?.Active != true;
        bool busy = Volatile.Read(ref closingOperations) != 0 || Volatile.Read(ref operating) != 0;
        bool retired = Volatile.Read(ref closing) != 0;
        bool canRestore = !busy && !retired && owner?.HasPendingRestore == true;
        if (!busy && !stoppedOn && !(retired && current.CanToggle)
            && current.HasPendingCleanup == (owner?.HasPendingRestore == true)
            && current.CanRestore == canRestore) return current;
        return current with {
            Value = busy || stoppedOn ? GodmodeValue.Unknown : current.Value,
            CanToggle = !busy && !retired && !stoppedOn && current.CanToggle,
            CanRestore = canRestore,
            HasPendingCleanup = current.HasPendingCleanup || owner?.HasPendingRestore == true,
            Status = busy ? "Local godmode operation in progress"
                : stoppedOn ? "The owned flag needs a fresh observation" : current.Status
        };
    }

    // The connection can disarm admission synchronously without doing native cleanup work
    public void RequestClose() => Interlocked.Exchange(ref closing, 1);

    public async Task<LocalGodmodeCloseResult> CloseAsync() {
        // Admission closes before waiting for any source callback or monitor to finish
        RequestClose();
        Interlocked.Increment(ref closingOperations);
        await gate.WaitAsync().ConfigureAwait(false);
        try {
            bool restored = feature is null || await feature.CloseAsync().ConfigureAwait(false);
            unconfirmedStop = false;
            bool confirmed = restored && feature?.HasPendingRestore != true;
            Publish(GodmodeValue.Unknown, false, confirmed ? "Closed" : feature?.FailureReason);
            return new(true, confirmed, confirmed ? null : feature?.FailureReason);
        } catch (Exception error) {
            // The owner remains retained until a later close confirms both stop and restoration
            unconfirmedStop = true;
            Publish(GodmodeValue.Unknown, false, error.Message);
            return new(false, false, error.Message);
        } finally {
            gate.Release();
            Interlocked.Decrement(ref closingOperations);
        }
    }

    private void Publish(GodmodeValue value, bool canToggle, string? status) {
        Volatile.Write(ref view, new(binding, binding?.Identity, value,
            canToggle && Volatile.Read(ref closing) == 0, unconfirmedStop || feature?.HasPendingRestore == true,
            string.IsNullOrWhiteSpace(status) ? "The local flag could not be confirmed" : status) {
                HasBoundTarget = feature is not null,
                CanRestore = Volatile.Read(ref closing) == 0 && feature?.HasPendingRestore == true
            });
    }
}
