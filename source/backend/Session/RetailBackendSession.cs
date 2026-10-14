using syndishanx_iw_tool.Diagnostics;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Apparition.Backend.Session;

// Preserve feature ownership independently of rendering or the injected input loop
internal sealed partial class RetailBackendSession : IBackendSession {
    private const string IncompleteCleanupStatus =
        "Cleanup has not finished. Select Exit to retry cleanup of the original session.";
    private readonly int processId;
    private readonly Func<bool> foregroundAllowed;
    private readonly Func<IRetailSessionTransport> transportFactory;
    private readonly Func<bool> otherToolRunning;
    private readonly Func<IRetailSessionTransport, ILocalPlayerObservationProvider?>? observationProviderFactory;
    private readonly TimeSpan observationStopTimeout;
    private readonly TimeProvider observationRecoveryClock;
    private readonly MessageHistory history = new(30, 500);
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object shutdownLock = new();
    private RetailConnection? connection;
    private long admissionRevision;
    private IReadOnlyList<DetectedPlayer> players = Array.Empty<DetectedPlayer>();
    private Task<SessionShutdownResult>? shutdownTask;
    private volatile bool closing;
    private volatile bool cleanupBlocked;
    private volatile bool restartRequired;
    private volatile bool ammoFailed;
    private string personalStatus = "Not connected";
    private string ammoStatus = "Not connected";

    public RetailBackendSession(int processId, Func<bool> foregroundAllowed,
        Func<IRetailSessionTransport> transportFactory, Func<bool> otherToolRunning,
        Func<IRetailSessionTransport, ILocalPlayerObservationProvider?>? observationProviderFactory = null,
        TimeSpan? observationStopTimeout = null, TimeProvider? observationRecoveryClock = null) {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        this.processId = processId;
        this.foregroundAllowed = foregroundAllowed ?? throw new ArgumentNullException(nameof(foregroundAllowed));
        this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        this.otherToolRunning = otherToolRunning ?? throw new ArgumentNullException(nameof(otherToolRunning));
        this.observationProviderFactory = observationProviderFactory;
        this.observationRecoveryClock = observationRecoveryClock ?? TimeProvider.System;
        // This bounded join is a local cleanup policy, not source menu or gameplay timing
        this.observationStopTimeout = observationStopTimeout ?? TimeSpan.FromSeconds(2);
        if (this.observationStopTimeout <= TimeSpan.Zero || this.observationStopTimeout > TimeSpan.FromMinutes(1)) {
            throw new ArgumentOutOfRangeException(nameof(observationStopTimeout));
        }
        Log("Spawn in a custom Zombies match, then select Connect");
    }

    public OverlayMenuState ReadMenuState() {
        // A frame retains a whole owner, so cleanup cannot clear one of its checked resources
        var owner = Volatile.Read(ref connection);
        var selection = Volatile.Read(ref playerSelection);
        var frameSelected = selection?.Player;
        var framePlayers = Volatile.Read(ref players);
        // Connection liveness is sampled by workers and never waits for the native access lock here
        bool connected = owner?.HasConfirmedConnection == true && !closing;
        bool retired = owner?.HasRetiredConnection == true;
        var personalView = owner?.LocalGodmode?.ReadView();
        // A local factory can finish before read-only shared registration succeeds
        if (personalView is not null && owner?.LocalGodmode is not null
            && !owner.GodmodeOwners.OwnsFeature(owner.LocalGodmode))
            personalView = personalView with { Value = GodmodeValue.Unknown, CanToggle = false,
                Status = owner.LocalGodmode.FailureReason ?? "The original personal owner is not registered" };
        if (personalView?.Identity is not null && owner!.GodmodeOwners.IsOperating(personalView.Identity))
            personalView = personalView with { Value = GodmodeValue.Unknown, CanToggle = false,
                CanRestore = false, Status = "Personal Godmode operation in progress" };
        var friendView = owner?.Teammates.ReadView();
        var cohortView = owner?.CohortGodmode.ReadView();
        bool sameOwner = ReferenceEquals(owner, Volatile.Read(ref connection));
        // A retired owner cannot confirm values using its replacement's metadata
        connected &= sameOwner;
        bool frameBusy = closing || !sameOwner;
        bool needsSessionRecovery = cleanupBlocked || restartRequired;
        bool available = connected && !frameBusy && !needsSessionRecovery;
        bool pendingFriend = friendView?.HasPendingCleanup == true;
        bool personalCleanup = personalView?.HasPendingCleanup == true || personalView?.Value == GodmodeValue.On;
        bool sameSelection = selection is not null && ReferenceEquals(selection.Owner, owner)
            && ReferenceEquals(selection, Volatile.Read(ref playerSelection));
        var capturedTarget = sameSelection ? selection!.Target : null;
        bool selectedSelf = capturedTarget?.Route == PlayerMenuRoute.Personal
            && (personalView?.HasBoundTarget == true && personalView.Identity == capturedTarget.Identity
                || AuthenticatedLocalIdentity(owner!) == capturedTarget.Identity);
        bool friendSelected = capturedTarget?.Route == PlayerMenuRoute.Teammate
            && friendView?.HasCurrentIdentity == true && friendView.Identity == capturedTarget.Identity;
        bool selectedTeammateNeedsCleanup = friendSelected && friendView!.CanToggle
            && (friendView.Value == GodmodeValue.On || friendView.HasPendingCleanup);
        bool selectedTeammateMissingFromRoster = capturedTarget is not null
            && !framePlayers.Any(player => player.Slot == capturedTarget.Identity.Slot
                && StringComparer.Ordinal.Equals(player.Name, capturedTarget.Identity.Name));
        // A mid-read selection change cannot attach earlier feature values to a newer receipt
        sameSelection &= ReferenceEquals(selection, Volatile.Read(ref playerSelection));
        selectedSelf &= sameSelection;
        friendSelected &= sameSelection;
        var publishedTarget = available && sameOwner && !frameBusy && (selectedSelf || friendSelected) ? capturedTarget : null;
        return new(false, connected, frameBusy,
            owner is null && !frameBusy && !needsSessionRecovery,
            available && personalView?.CanToggle == true && (!pendingFriend || personalCleanup),
            available ? FormatGodmodeValue(personalView?.Value) : "UNKNOWN",
            retired ? owner!.ConnectionFailureStatus : pendingFriend && !personalCleanup && personalView?.Value == GodmodeValue.Off
                ? "An earlier Godmode change needs cleanup before enabling"
                : personalView?.Status ?? "A verified local actor flags source is unavailable",
            available && owner?.Ammo.RequiresGameRestart != true,
            !available || ammoFailed || owner?.Ammo.RequiresGameRestart == true
                || owner?.Ammo.HasPendingRestore == true && !owner.Ammo.Active ? "UNKNOWN" : owner?.Ammo.Active == true ? "ON" : "OFF",
            // Cleanup guidance remains actionable even after original connection liveness retires
            needsSessionRecovery ? ammoStatus : retired ? owner!.ConnectionFailureStatus : ammoStatus,
            available && !pendingFriend, available && friendView?.CanSelect == true,
            framePlayers.Select(player => new OverlayPlayer(player.Slot, player.Name)).ToArray(),
            frameSelected?.Slot ?? -1, frameSelected?.Name,
            available && !selectedSelf && friendSelected && friendView?.CanToggle == true,
            !available || selectedSelf ? "UNKNOWN" : friendSelected ? FormatGodmodeValue(friendView?.Value) : "UNKNOWN",
            retired ? owner!.ConnectionFailureStatus : selectedSelf ? "This is the verified personal actor; use Basic Scripts"
                : friendView?.Status ?? "Select a spawned teammate",
            false, string.Join("\n", history.Entries.TakeLast(5))) {
                // A retained failed cleanup is an administrative route, never a fresh actor grant
                NeedsSessionRecovery = needsSessionRecovery,
                SelectedPlayerTarget = publishedTarget,
                CanRecoverSelectedTeammate = available && selectedTeammateNeedsCleanup
                    && selectedTeammateMissingFromRoster,
                PersonalGodmodeIdentity = sameOwner && personalView?.HasBoundTarget == true ? personalView.Identity : null,
                PersonalGodmodeRequiresVerifiedTarget = true,
                CanRestorePersonalGodmode = available && personalView?.CanRestore == true,
                CanAllPlayersGodmode = available && cohortView?.CanToggle == true,
                AllPlayersGodmodeValue = available ? FormatCohortGodmodeValue(cohortView?.Value) : "UNKNOWN",
                AllPlayersGodmodeStatus = retired ? owner!.ConnectionFailureStatus : cohortView?.Status ?? "A complete verified lobby is required",
                AllPlayersGodmodeTarget = available ? cohortView?.Target : null
            };
    }

    public Task ExecuteAsync(MenuAction action) {
        var captured = CapturePlayerAction(action);
        long revision = Volatile.Read(ref admissionRevision);
        bool admitted = !closing;
        return Task.Run(() => ExecuteOnWorkerAsync(captured, revision, admitted));
    }

    internal Task ExecuteOnWorkerAsync(MenuAction action) =>
        ExecuteOnWorkerAsync(CapturePlayerAction(action), Volatile.Read(ref admissionRevision), !closing);

    private async Task ExecuteOnWorkerAsync(MenuAction action, long revision, bool admitted) {
        RetailConnection? owner = null;
        bool connectedNow = false;
        await lifecycleGate.WaitAsync();
        try {
            // A queued request cannot become a new action after shutdown finishes
            if (!admitted || revision != Volatile.Read(ref admissionRevision) || closing
                || cleanupBlocked || restartRequired || !foregroundAllowed()) return;
            if (action.Kind == MenuActionKind.Connect) {
                owner = Connect();
                connectedNow = owner is not null;
            } else {
                owner = Volatile.Read(ref connection);
            }
        } finally { lifecycleGate.Release(); }
        // The first local source read starts only after publication releases the lifecycle gate
        if (connectedNow) {
            await CapturePersonalGodmodeAsync(owner!);
            await RefreshCohortGodmodeAsync(owner!);
            return;
        }
        if (action.Kind != MenuActionKind.Connect && owner is not null) await ExecuteCoreAsync(owner, action);
    }

    private async Task ExecuteCoreAsync(RetailConnection owner, MenuAction action) {
        if (!CanCommit(owner) || !foregroundAllowed()) return;
        if (!owner.ObserveOriginalConnection()) return;
        if (action.Kind is MenuActionKind.SelectPlayer or MenuActionKind.ToggleTeammateGodmode
            || action.ExpectedTarget is not null || action.Page == MenuPage.SelectedBasicScripts
            || action.Kind == MenuActionKind.TogglePersonalGodmode && action.Slot >= 0) {
            await ExecuteTargetActionAsync(owner, action);
            return;
        }
        switch (action.Kind) {
            case MenuActionKind.TogglePersonalGodmode: await ToggleGodmodeAsync(owner, action); break;
            case MenuActionKind.RestorePersonalGodmode: await RestoreGodmodeAsync(owner); break;
            case MenuActionKind.ToggleAllPlayersGodmode: await ToggleCohortGodmodeAsync(owner, action); break;
            case MenuActionKind.ToggleLobbyAmmo: await ToggleAmmoAsync(owner); break;
            case MenuActionKind.RefreshPlayers:
                if (!owner.Teammates.ReadView().HasPendingCleanup) await RefreshPlayersAsync(owner);
                break;
        }
    }

    private RetailConnection? Connect() {
        if (connection is not null || !foregroundAllowed() || closing) return null;
        if (otherToolRunning()) throw new InvalidOperationException("Another RTM or Apparition tool is running. Close it before connecting");
        lock (shutdownLock) {
            if (shutdownTask?.IsCompleted == false) return null;
            shutdownTask = null;
        }
        var candidate = transportFactory() ?? throw new InvalidOperationException("The retail transport factory returned no transport");
        var owner = new RetailConnection(candidate, observationProviderFactory, processId, observationStopTimeout);
        try {
            if (!candidate.Open(processId) || !owner.ObserveOriginalConnection()) {
                throw new InvalidOperationException("The original retail game could not be opened");
            }
            if (closing) {
                candidate.Dispose();
                owner.ObservationCancellation.Dispose();
                return null;
            }
            // All resources exist before a frame or action can observe this generation
            ammoFailed = false;
            personalStatus = "The local actor has not been captured";
            ammoStatus = "Off";
            Volatile.Write(ref playerSelection, new(owner, new object(), 0));
            Volatile.Write(ref connection, owner);
            RefreshPlayers(owner);
            Log("Connected to the original Steam game. Gameplay stability is still being tested");
            return owner;
        } catch (Exception exception) {
            try {
                candidate.Dispose();
                // An unpublished candidate cannot have started an observation task
                owner.ObservationCancellation.Dispose();
            }
            catch {
                // Failed disposal retains the candidate and its cleanup path, even after failed Open
                Volatile.Write(ref connection, owner);
                cleanupBlocked = true;
                personalStatus = ammoStatus = IncompleteCleanupStatus;
                // Preserve the original failed Open reason alongside its retained cleanup route
                Log("Connection could not complete: " + exception.Message);
                Log("Session cleanup: " + ammoStatus);
            }
            throw;
        }
    }

    private void RefreshPlayers(RetailConnection owner) {
        try {
            players = owner.Transport.ReadPlayerNames().ToArray();
            ClearMissingSelection(players);
        } catch (Exception exception) {
            players = Array.Empty<DetectedPlayer>();
            Log("Player detection failed: " + exception.Message);
        }
    }

    private bool CanCommit(RetailConnection owner) => ReferenceEquals(owner, Volatile.Read(ref connection))
        && !closing && !cleanupBlocked && !restartRequired;

    private async Task CommitAsync(RetailConnection owner, Action commit) {
        await lifecycleGate.WaitAsync();
        // Checking and changing display state share one gate with final owner release
        try { if (CanCommit(owner)) commit(); }
        finally { lifecycleGate.Release(); }
    }

    private async Task ToggleAmmoAsync(RetailConnection owner) {
        var feature = owner.Ammo;
        if (feature.RequiresGameRestart) return;
        var result = await owner.AmmoToggle.SetAsync(!feature.Active);
        string status = result.Succeeded ? feature.Active ? "On" : "Off"
            : "Not confirmed: " + (feature.FailureReason ?? result.ErrorMessage);
        Task<SessionShutdownResult>? cleanup = null;
        await lifecycleGate.WaitAsync();
        try {
            if (!CanCommit(owner)) return;
            ammoFailed = !result.Succeeded;
            ammoStatus = status;
            Log("Lobby Infinite Ammo: " + status);
            // Latch shutdown inside the commit guard, but await it only after releasing this gate
            if (!result.Succeeded && feature.HasPendingRestore) cleanup = ShutdownAsync();
        } finally { lifecycleGate.Release(); }
        if (cleanup is not null && !(await cleanup).Stopped) {
            lock (shutdownLock) {
                // Exit retries this retained owner once; a newer explicit retry keeps its own task
                if (ReferenceEquals(shutdownTask, cleanup)) shutdownTask = null;
            }
        }
    }

    public Task<SessionShutdownResult> ShutdownAsync() {
        lock (shutdownLock) {
            if (shutdownTask is not null) return shutdownTask;
            // Disarm connection and action entry from the moment shutdown is requested
            closing = true;
            // Closing revokes cached opening proof before any retained source task is joined
            Volatile.Write(ref localMenuAdmission, null);
            Interlocked.Increment(ref admissionRevision);
            var owner = Volatile.Read(ref connection);
            // Source callbacks cannot finish into a new local adapter after shutdown is requested
            owner?.RequestNewWriteClose();
            owner?.LocalGodmode?.RequestClose();
            owner?.Teammates.RequestClose();
            owner?.CohortGodmode.RequestClose();
            Volatile.Write(ref cohortObservationWanted, false);
            if (owner is not null) Volatile.Write(ref owner.Observation, LocalPlayerObservation.Unknown("The session is closing"));
            return shutdownTask = Task.Run(ShutdownCoreAsync);
        }
    }

    private async Task<SessionShutdownResult> ShutdownCoreAsync() {
        await lifecycleGate.WaitAsync();
        RetailConnection? owner;
        try { owner = Volatile.Read(ref connection); }
        finally { lifecycleGate.Release(); }
        var godmodeCleanup = new ConnectionGodmodeCleanup(owner);
        var shutdown = new SessionShutdown([
            () => owner?.AmmoToggle.CloseAsync() ?? Task.CompletedTask,
            async () => {
                // A peer refresh can hold its selection gate, so join the retained source first
                // A timed-out source leaves the original Godmode owners intact for cleanup retry
                await StopLocalObservationAsync(owner).ConfigureAwait(false);
                await godmodeCleanup.StopAsync().ConfigureAwait(false);
            },
            () => StopLocalObservationAsync(owner)
        ], async () => {
            bool godmodeRestored = godmodeCleanup.RestoreConfirmed;
            bool ammoRestored = owner is null || await owner.Ammo.CloseAsync();
            return godmodeRestored && ammoRestored && (owner?.LocalGodmode is null
                || owner.LocalGodmode.ReadView().HasPendingCleanup == false);
        }, () => {
            owner?.Transport.Dispose();
            // The native source and its cancellation state release only after every owned task has ended
            owner?.ObservationCancellation.Dispose();
        });
        var result = await shutdown.ShutdownAsync();
        // Field clearing shares the action commit gate; a completion cannot cross a reconnect
        await lifecycleGate.WaitAsync();
        try {
            if (ReferenceEquals(owner, Volatile.Read(ref connection))) {
                if (result.Stopped) {
                    Volatile.Write(ref connection, null);
                    Volatile.Write(ref playerSelection, null);
                    players = Array.Empty<DetectedPlayer>();
                    restartRequired |= !result.Restored;
                }
                cleanupBlocked = !result.Stopped;
                closing = false;
                // Incomplete cleanup retains its source; unconfirmed released state needs a new game process
                personalStatus = ammoStatus = cleanupBlocked
                    ? IncompleteCleanupStatus
                    : restartRequired
                        ? "Cleanup was not confirmed. Fully exit Infinite Warfare and restart it through Steam before reconnecting."
                        : "Not connected";
                if (cleanupBlocked || restartRequired) {
                    // The recovery route stays visible even when an empty retry has no new error details
                    Log("Session cleanup: " + ammoStatus);
                    if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) Log("Cleanup details: " + result.ErrorMessage);
                }
            }
        } finally { lifecycleGate.Release(); }
        return result;
    }

    public void AllowShutdownRetry() {
        lock (shutdownLock) {
            if (shutdownTask?.IsCompleted == true) shutdownTask = null;
        }
    }

    private void Log(string message) => history.Append(message);
}
