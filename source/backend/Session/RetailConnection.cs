using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.FeatureState;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// One resource bundle is also the generation token for all of its action completions
internal sealed partial class RetailConnection {
    public IRetailSessionTransport Transport { get; }
    public InfiniteAmmoFeature Ammo { get; }
    public LocalGodmodeSession? LocalGodmode { get; }
    public ToggleCoordinator AmmoToggle { get; }
    public TeammateGodmodeSession Teammates { get; }
    public CohortGodmodeSession CohortGodmode { get; }
    private readonly GodmodeTargetReservations reservations = new();
    private readonly object localToken = new();
    private readonly object teammateTokensLock = new();
    private readonly Dictionary<PlayerIdentity, object> teammateTokens = new();
    private readonly Dictionary<PlayerIdentity, IPlayerGodmodeFeature> teammateFeatures = new();
    private ITeammateFlagsTransport? localFlags;
    private readonly Func<IRetailSessionTransport, ILocalPlayerObservationProvider?>? observationFactory;
    private ILocalPlayerObservationProvider? observationProvider;
    private bool observationProviderCreated;
    public CancellationTokenSource ObservationCancellation { get; } = new();
    public Task? ObservationRead { get; set; }
    public Task? ObservationCancellationWork { get; set; }
    public object? ObservationTicket { get; set; }
    public LocalPlayerObservation Observation = LocalPlayerObservation.BindingUnavailable;

    public RetailConnection(IRetailSessionTransport transport,
        Func<IRetailSessionTransport, ILocalPlayerObservationProvider?>? observationFactory = null,
        int processId = 0, TimeSpan? cohortStopTimeout = null) {
        Transport = transport;
        expectedProcessId = processId;
        this.observationFactory = observationFactory;
        // Construction takes no ownership of game bytes and performs no process reads
        byte[] ammoReplacement = [0x90, 0x90, 0x90, 0x90];
        Ammo = new(new OriginalConnectionAmmoTransport(transport, AdmitNewWrite, ammoReplacement),
            [0x01, 0x7C, 0x90, 0x14], ammoReplacement,
            [0x48, 0x8B, 0x4C, 0x24, 0x60]);
        AmmoToggle = new(Ammo.SetAsync);
        // No legacy health writer exists when a verified local flag source is unavailable
        if (transport is ILocalGodmodeTransport local) {
            LocalGodmode = new(() => local.ReadLocalActorFlagsSnapshot(CancellationToken.None), snapshot => {
                reservations.Acquire(localToken, snapshot.Binding.Identity);
                if (localFlags is not null) return localFlags;
                try {
                    var bound = local.BindLocalActor(snapshot)
                        ?? throw new IOException("The local flags adapter was not created");
                    localFlags = new OriginalConnectionFlagsTransport(bound, AdmitNewWrite);
                    reservations.Retain(localToken);
                    return localFlags;
                }
                catch {
                    // Only failed construction releases a reservation with no retained writer
                    if (localFlags is null) reservations.ReleaseUnbound(localToken);
                    throw;
                }
            });
        }
        // One registry owns individual and captured-set state for every retained actor
        GodmodeOwners = new(ResolveGodmodeOwner, ValidateGodmodeOwner, ValidateGodmodeOwnerForDisplay);
        Teammates = new(transport.ReadTeammateState, GodmodeOwners,
            transport is IOwnedTeammateStateTransport owned ? owned.ReadOwnedTeammateFlags : null,
            IsRetainedPersonalOwner);
        // The expected PID is supplied by the owner before Open; construction performs no query
        CohortGodmode = new(GodmodeOwners, processId,
            transport is IPlayerCohortTransport cohort ? cohort.ReadCohort : null,
            transport is ILocalActorSnapshotSource actor ? actor.ReadLocalActorSnapshot : null,
            () => LocalGodmode?.ReadView(),
            () => LocalGodmode is not null && GodmodeOwners.OwnsFeature(LocalGodmode),
            ObserveOriginalConnection, cohortStopTimeout);
    }

    // Only the one retained read task invokes construction, outside the lifecycle gate
    public ILocalPlayerObservationProvider? ReadObservationProvider() {
        if (!observationProviderCreated) {
            observationProvider = observationFactory?.Invoke(Transport);
            observationProviderCreated = true;
        }
        return observationProvider;
    }
}
