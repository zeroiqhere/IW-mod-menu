using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// The production controller and session use recorded flags and an owned observation receipt
internal sealed class OwnedCleanupAccessRig : IAsyncDisposable {
    internal CohortRetailTransport Transport { get; private set; } = new();
    internal LocalAdmissionFixture.ReceiptProvider Provider { get; private set; }
    internal RetailBackendSession Session { get; }
    internal BackendController Controller { get; }
    private readonly ManualTimeProvider clock = new();
    private ulong sequence;

    internal OwnedCleanupAccessRig() {
        Transport.Inner.LocalSlot = 2;
        Provider = new(new OwnedLocalPlayerObservationProvider(4242, Transport));
        Session = new(4242, () => true, () => Transport, () => false, _ => Provider);
        Check(VerifiedOpeningActions.TryCreate([OpeningAction.Aim, OpeningAction.Melee], out var actions, out _),
            "The owned opening controls were rejected");
        Controller = new(4242, Session, clock: clock, openingActions: actions,
            preferenceStore: new MemoryCommittedMenuPreferencesStore());
    }

    internal async Task ConnectAsync() {
        await Session.ExecuteAsync(new(MenuActionKind.Connect));
        await Session.RefreshLocalObservationAsync();
    }

    internal async Task OwnFlagsAsync(bool personal = true, bool peer = true) {
        if (personal) await Session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
        if (peer) {
            await Session.ExecuteAsync(Select(1));
            await Session.ExecuteAsync(Toggle(1));
        }
    }

    internal async Task OpenNormalAsync() {
        var admission = Session.ReadLocalMenuAdmission() ?? throw new InvalidOperationException("Missing healthy admission");
        var frame = Controller.Snapshot();
        Controller.Consume(new(++sequence, 1, 4242, OverlayInput.OpenMain, true,
            frame.NavigationRevision, frame.MainOpeningControls!.Revision, admission.Revision), () => true);
        Controller.StartQueuedAction();
        await Controller.DrainOperationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Controller.Snapshot();
        clock.Advance(TimeSpan.FromMilliseconds(50));
        Controller.Snapshot();
        clock.Advance(TimeSpan.FromSeconds(2));
        Check(Controller.Snapshot().IsOpen, "A healthy owned admission failed to open");
    }

    internal async Task LoseAdmissionAsync(bool readFailure = true) {
        if (readFailure) Provider.BeforeRead = (_, _) => throw new IOException("Owned receipt temporarily unavailable");
        else Transport.Inner.LocalSessionState = 99;
        await Session.RefreshLocalObservationAsync();
        Check(Session.ReadLocalMenuAdmission() is null, "The fixture retained actor admission");
        Controller.Snapshot();
    }

    internal async Task RecoverAdmissionAsync() {
        Provider.BeforeRead = null;
        Transport.Inner.LocalSessionState = 0;
        await Session.RefreshLocalObservationAsync();
    }

    internal void ReplaceRecordedTransport() {
        Transport = new();
        Transport.Inner.LocalSlot = 2;
        Provider = new(new OwnedLocalPlayerObservationProvider(4242, Transport));
    }

    // Sequence overrides let a consumed or foreign command be tested without inventing a new channel
    internal void Send(OverlayInput input, bool foreground = true, int processId = 4242, bool replay = false) {
        if (!replay) sequence++;
        var frame = input == OverlayInput.Toggle ? Controller.Snapshot() : null;
        Controller.Consume(new(sequence, 1, processId, input, true,
            frame?.NavigationRevision, frame?.MainOpeningControls?.Revision), () => foreground);
    }

    internal void SelectWire(ulong navigationRevision, ulong? localRevision = null, ulong? openingRevision = null) {
        var fields = new Dictionary<string, object> { ["version"] = 1, ["processId"] = 4242,
            ["sequence"] = ++sequence, ["input"] = "select", ["navigationRevision"] = navigationRevision,
            ["openingControlsRevision"] = openingRevision ?? Controller.Snapshot().MainOpeningControls!.Revision };
        if (localRevision is not null) fields["localAdmissionRevision"] = localRevision.Value;
        var command = CommandEnvelope.Parse(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fields));
        Check(command is not null && command.Valid == (localRevision is null),
            "Production parsing did not distinguish ordinary Exit from forged actor authority");
        Controller.Consume(command!, () => true);
    }

    internal void CapturedToggle(ulong? navigationRevision, ulong? policyRevision) {
        var fields = new Dictionary<string, object> { ["version"] = 1, ["processId"] = 4242,
            ["sequence"] = ++sequence, ["input"] = "toggle" };
        if (navigationRevision is not null) fields["navigationRevision"] = navigationRevision.Value;
        if (policyRevision is not null) fields["openingControlsRevision"] = policyRevision.Value;
        var command = CommandEnvelope.Parse(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fields));
        Check(command is not null, "The captured Toggle packet lost its recoverable sequence");
        Controller.Consume(command!, () => true);
    }

    public async ValueTask DisposeAsync() {
        Provider.BeforeRead = null;
        await Session.ShutdownAsync();
    }
}
