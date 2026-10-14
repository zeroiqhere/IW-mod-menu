using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.SessionLifecycle;

namespace Backend.Tests;

// Commands run through the actual controller while account ownership belongs to this fixture
internal sealed class PreferenceControllerFixture {
    public MemoryCommittedMenuPreferencesStore Store { get; } = new();
    public PreferenceSession Session { get; } = new();
    public ManualTimeProvider Clock { get; } = new();
    public BackendController Controller { get; }
    public ulong Sequence = 1;

    public PreferenceControllerFixture(CommittedMenuPreferences? saved = null, VerifiedOpeningActions? capabilities = null,
        ICommittedMenuPreferencesStore? store = null) {
        Session.Scope = new(123, new object());
        if (saved is not null) Store.Write(Session.Scope, saved);
        Controller = new(42, Session, true, true, Clock, true, true, capabilities, store ?? Store);
    }

    public BackendController Recreate() => new(42, Session, true, true, new ManualTimeProvider(), true, true,
        preferenceStore: Store);

    public void Send(OverlayInput input) => Controller.Consume(new(Sequence++, 1, 42, input, true,
        input >= OverlayInput.Close ? Controller.Snapshot().NavigationRevision : null), () => true);

    public void OpenCustomization() {
        Send(OverlayInput.Toggle);MenuOpenTestHarness.Settle(Controller, Clock);
        Highlight("Menu Customization");Send(OverlayInput.Select);
    }

    public void Highlight(string label) {
        var rows = Controller.Snapshot().Rows;
        int target = rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(target >= 0, "The actual source row is missing: " + label);
        for (int move = 0; move < rows.Count && Controller.Snapshot().SelectedIndex != target; move++) Send(OverlayInput.Down);
        TestAssert.Equal(target, Controller.Snapshot().SelectedIndex);
    }

    public void OpenPreferences() {
        OpenCustomization();Highlight("Menu Preferences");Send(OverlayInput.Select);
    }

    internal sealed class PreferenceSession : IBackendSession {
        public LocalMenuPreferenceScope? Scope;
        public SessionBoundary Boundary { get; } = new();
        public LocalMenuPreferenceScope? ReadLocalMenuPreferenceScope() => Scope;
        public OverlayMenuState ReadMenuState() => Boundary.ReadMenuState();
        public Task ExecuteAsync(MenuAction action) => Boundary.ExecuteAsync(action);
        public Task<SessionShutdownResult> ShutdownAsync() => Boundary.ShutdownAsync();
    }
}
