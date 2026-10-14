using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// One connection borrows its shared registry and retains every source operation until stop
public sealed partial class CohortGodmodeSession {
    private readonly PlayerGodmodeRegistry registry;
    private readonly int expectedProcessId;
    private readonly Func<PlayerCohortObservation>? readCohort;
    private readonly Func<CancellationToken, LocalActorSnapshot>? readActor;
    private readonly Func<LocalGodmodeView?> readLocalView;
    private readonly Func<bool> localRegistered;
    private readonly Func<bool> connectionCheck;
    private readonly TimeSpan stopTimeout;
    private readonly object workGate = new();
    private readonly object ownerToken = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task<GodmodeOperationResult>? work;
    private Task? cancellationWork;
    private int closing;
    private int operating;
    private CohortGodmodeTarget? target;
    private readonly Dictionary<PlayerIdentity, int> latestTimes = new();
    private Publication publication = new(new(GodmodeValue.Unknown, false, "Not captured", null), null);

    private sealed record Publication(CohortGodmodeView View, GodmodeMetadataStamp? Metadata);

    public CohortGodmodeSession(PlayerGodmodeRegistry registry, int expectedProcessId,
        Func<PlayerCohortObservation>? readCohort,
        Func<CancellationToken, LocalActorSnapshot>? readActor,
        Func<LocalGodmodeView?> readLocalView, Func<bool> localRegistered,
        Func<bool> connectionCheck, TimeSpan? stopTimeout = null) {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.expectedProcessId = expectedProcessId;
        this.readCohort = readCohort;
        this.readActor = readActor;
        this.readLocalView = readLocalView ?? throw new ArgumentNullException(nameof(readLocalView));
        this.localRegistered = localRegistered ?? throw new ArgumentNullException(nameof(localRegistered));
        this.connectionCheck = connectionCheck ?? throw new ArgumentNullException(nameof(connectionCheck));
        this.stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(5);
        if (this.stopTimeout <= TimeSpan.Zero || this.stopTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(stopTimeout));
        // Construction performs no source reads and creates no flag adapter
    }

    public CohortGodmodeView ReadView() {
        Publication cached = Volatile.Read(ref publication);
        if (Volatile.Read(ref closing) != 0) return Unavailable("Cohort admission is closed");
        if (Volatile.Read(ref operating) != 0) {
            // A source BOOL wait can keep its private row target while the actual operation runs
            return cached.View with { Value = GodmodeValue.Unknown, CanToggle = false,
                Status = "Cohort Godmode operation in progress" };
        }
        try {
            var captured = cached.View.Target?.Capture;
            if (captured is null || cached.Metadata is null) return cached.View;
            if (!cached.Metadata.Matches(registry.ReadMetadataStamp(captured.Identities)))
                return Unavailable("The shared Godmode owners need a fresh cohort observation");
            return cached.View;
        } catch (Exception) {
            // A broken metadata accessor cannot retain an actionable cached row
            return Unavailable("The shared Godmode owner metadata is unavailable");
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default) {
        Task<GodmodeOperationResult> retained = Start(false, null, cancellationToken);
        await retained.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GodmodeOperationResult> ToggleAsync(CohortGodmodeTarget? expectedTarget,
        CancellationToken cancellationToken = default) =>
        await Start(true, expectedTarget, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);

    private Task<GodmodeOperationResult> Start(bool toggle, CohortGodmodeTarget? expected,
        CancellationToken callerToken) {
        lock (workGate) {
            if (Volatile.Read(ref closing) != 0) return Task.FromResult(Rejected("Cohort admission is closed"));
            if (work is { IsCompleted: false }) {
                // Refresh callers share one retained task; a new action cannot join a different action
                return toggle ? Task.FromResult(Rejected("A cohort operation is already running")) : work;
            }
            Volatile.Write(ref operating, 1);
            work = Task.Run(async () => {
                using var token = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, callerToken);
                try { return await ExecuteAsync(toggle, expected, token.Token).ConfigureAwait(false); }
                finally { Volatile.Write(ref operating, 0); }
            });
            return work;
        }
    }

    private static CohortGodmodeView Unavailable(string reason) => new(GodmodeValue.Unknown, false, reason, null);
    private static GodmodeOperationResult Rejected(string reason, bool cancelled = false) =>
        new(false, cancelled, reason, new(GodmodeValue.Unknown, [], false, false));
}
