using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.SessionLifecycle;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// The process boundary is simulated because regression tests must never open game memory
internal sealed class SessionBoundary : IBackendSession {
    public OverlayMenuState State { get; set; } = new(false, false, false, true,
        false, "UNKNOWN", "Not connected", false, "UNKNOWN", "Not connected",
        false, false, Array.Empty<OverlayPlayer>(), -1, null, false,
        "UNKNOWN", "Select a spawned teammate", false, "");
    private readonly object selectionOwner = new();
    public ulong SelectionEpoch { get; set; }
    public bool CaptureSelection { get; set; } = true;
    public PlayerMenuSelectionScope? ReadPlayerSelectionScope() => new(selectionOwner, SelectionEpoch);
    public PlayerMenuTarget? ReadSelectedPlayerTarget() => State.SelectedPlayerTarget;
    public List<MenuAction> Executed { get; } = new();
    public TaskCompletionSource? Pending { get; set; }
    public bool Fail { get; set; }
    public int ShutdownCalls { get; private set; }
    public int MenuStateReads { get; private set; }
    public bool RejectMenuStateReads { get; set; }
    public OverlayMenuState ReadMenuState() {
        if (RejectMenuStateReads) throw new InvalidOperationException("Controlled menu-state reads are disabled");
        MenuStateReads++;
        return State;
    }
    public Task ExecuteAsync(MenuAction action) {
        Executed.Add(action);
        // The controlled capture owns its full result independently of the later task completion
        if (!Fail && CaptureSelection && action.Kind == MenuActionKind.SelectPlayer && action.SelectionScope is { } scope
            && scope.Equals(ReadPlayerSelectionScope()) && SelectionEpoch < ulong.MaxValue) {
            var local = State.PersonalGodmodeIdentity;
            bool personal = local is not null && local.Slot == action.Slot && local.Name == action.ExpectedName;
            var identity = personal ? local! : new PlayerIdentity(action.Slot, 0x11000 + action.Slot * 0x1000,
                0x22000 + action.Slot * 0x1000, 0x33000, 4, action.ExpectedName!, "ABCDEF0123456789") { ServerClient = 0x44000 + action.Slot * 0x1000 };
            State = State with { SelectedSlot = action.Slot, SelectedName = action.ExpectedName,
                SelectedPlayerTarget = new(identity, personal ? PlayerMenuRoute.Personal : PlayerMenuRoute.Teammate,
                    selectionOwner, ++SelectionEpoch) };
        }
        return Fail ? Task.FromException(new IOException("Controlled failure"))
            : Pending?.Task ?? Task.CompletedTask;
    }
    public Task<SessionShutdownResult> ShutdownAsync() {
        ShutdownCalls++;
        return Task.FromResult(new SessionShutdownResult(true, true, null));
    }
}
