using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Backend.Tests;

// These supplied facts are owned offline test data, not a retail identity claim
internal sealed class MenuObservationFixture {
    public const int ProcessId = 51;
    public static readonly PlayerIdentity Local = new(0, 0x10000, 0x20000, 0x30000,
        4, "Local player", "1234567890ABCDEF") { ServerClient = 0x50000 };
    public ManualTimeProvider Clock { get; } = new();
    public ObservationSession Session { get; } = new();
    public OverlayNavigation Navigation { get; } = new();
    public BackendController Controller { get; }
    private ulong sequence;

    public MenuObservationFixture(MenuSurface surface = MenuSurface.Quick, bool open = true) {
        if (open) Navigation.Open(surface);
        Controller = new(ProcessId, Session, Navigation, menuPositionEditorControlsAvailable: true,
            choiceControlsAvailable: true, clock: Clock);
    }

    public void Send(OverlayInput input, bool focused = true, ulong? navigationRevision = null) =>
        Controller.Consume(new(++sequence, 1, ProcessId, input, true, navigationRevision), () => focused);

    public static LocalPlayerObservation Observed(LocalPlayerLife life = LocalPlayerLife.Alive,
        bool? host = true) => new(ProcessId, Local, life, true, false,
            "Supplied authenticated offline observation", host);

    public static OverlayMenuState State() => new(false, true, false, false, true, "ON", "Personal state",
        false, "UNKNOWN", "", false, false, Array.Empty<OverlayPlayer>(), -1, null, false,
        "UNKNOWN", "", false, "");

    internal sealed class ObservationSession : IBackendSession {
        public LocalPlayerObservation Observation { get; set; } = Observed();
        public OverlayMenuState MenuState { get; set; } = State();
        public List<MenuAction> Executed { get; } = new();
        public int ObservationReads { get; private set; }
        public Action<int>? BeforeObservation { get; set; }
        public OverlayMenuState ReadMenuState() => MenuState;
        public LocalPlayerObservation ReadLocalObservation() {
            ObservationReads++;
            BeforeObservation?.Invoke(ObservationReads);
            return Observation;
        }
        public Task ExecuteAsync(MenuAction action) { Executed.Add(action); return Task.CompletedTask; }
        public Task<SessionShutdownResult> ShutdownAsync() =>
            Task.FromResult(new SessionShutdownResult(true, true, null));
    }
}
