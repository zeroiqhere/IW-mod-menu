using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOptionCallbackWireTests {
    public static void FactorySuppressesSelectedHelpWhileCallbackControlsArePaused() {
        var snapshot = new MenuSnapshot("Apparition", "CONNECTED", new[] {
            new MenuRow("Godmode", "UNKNOWN", false, new(MenuActionKind.None), "Press select to toggle")
        }, "Updating option; controls paused");
        var callback = new MenuOptionCallback(1, "boolean", "pending");
        var frame = StateDocument.Create(42, 1, true, snapshot, 0, menuOptionCallback: callback);
        TestAssert.Equal("Updating option; controls paused", frame.Notice);
        TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, false, snapshot, 0,
            menuOptionCallback: callback));
    }

    public static void FocusHiddenCallbackProjectionSerializesWithoutMutatingItsOwner() {
        var owner = Frame() with { MenuOptionCallback = new(1, "boolean", "pending") };
        var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(!hidden.IsOpen && hidden.MenuOptionCallback is null,
            "A hidden frame must not publish an active ordinary callback lock");
        TestAssert.True(hidden.Serialize().Length > 0, "The hidden frame must pass the actual public validator");
        TestAssert.True(owner.MenuOptionCallback is not null && owner.IsOpen,
            "Focus projection must preserve the retained callback owner");
    }

    public static void CallbackCursorMustSelectAnExistingNonSpacerOrdinaryRow() {
        var callback = new MenuOptionCallback(1, "plain", "cooldown");
        foreach (int cursor in new[] { -1, 2, int.MaxValue })
            TestAssert.Throws<InvalidOperationException>(() => (Frame() with {
                SelectedIndex = cursor, MenuOptionCallback = callback }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (Frame() with {
            Rows = new[] { new StateRow("", "", false, true, null), new StateRow("Item", "", true, false, null) },
            MenuOptionCallback = callback }).Serialize());
    }

    private static StateDocument Frame(bool open = true, int rows = 2) => StateDocument.Create(42, 1, open,
        new("Apparition", "CONNECTED", Enumerable.Range(0, rows).Select(index => new MenuRow("Item " + index, "OFF", true, new(MenuActionKind.None))).ToArray(), ""), 0);

    public static void ValidCallbacksRoundTripExactFieldsOnBothFamiliesWithoutConfirmingGameValues() {
        foreach (string surface in new[] { "main", "quick" }) {
            foreach (var owner in new[] { new MenuOptionCallback(1, "boolean", "pending"), new(ulong.MaxValue, "boolean", "cooldown"), new(2, "plain", "cooldown") }) {
                var state = Frame() with { MenuSurface = surface, MenuOptionCallback = owner };
                using var json = System.Text.Json.JsonDocument.Parse(state.Serialize());
                var callback = json.RootElement.GetProperty("menuOptionCallback");
                TestAssert.Equal(3, callback.EnumerateObject().Count());
                TestAssert.Equal(owner.Revision, callback.GetProperty("revision").GetUInt64());
                TestAssert.Equal(owner.Kind, callback.GetProperty("kind").GetString()!);
                TestAssert.Equal(owner.Phase, callback.GetProperty("phase").GetString()!);
                TestAssert.Equal("OFF", state.Rows[0].Value);
            }
        }
        using var legacy = System.Text.Json.JsonDocument.Parse(Frame().Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("menuOptionCallback", out _), "Legacy packets must omit the absent callback owner");
    }

    public static void UnsupportedCallbackCopiesCannotSerializeAsPositiveMonitorFacts() {
        foreach (var owner in new[] { new MenuOptionCallback(0, "boolean", "pending"), new(1, "plain", "pending"),
            new(1, "unknown", "cooldown"), new(1, "boolean", "ready"), new(1, null!, "pending") }) {
            TestAssert.True(!owner.IsSupported(), "The callback descriptor must reject unsupported facts");
            TestAssert.Throws<InvalidOperationException>(() => (Frame() with { MenuOptionCallback = owner }).Serialize());
        }
    }

    public static void CallbackOwnersCannotBorrowClosedEmptyOrCompetingTransitionFrames() {
        var owner = new MenuOptionCallback(1, "boolean", "pending");
        var frames = new[] { Frame(false), Frame(rows: 0), Frame() with { MenuUnlock = "pause" },
            Frame() with { MenuPreferenceRefresh = new("pending") }, Frame() with { MenuOpenTransition = new("cooldown") },
            Frame() with { MenuDesignTransition = new("rebuild") }, Frame() with { MenuPreferenceRebuild = new("cooldown") },
            Frame() with { MenuCredits = new(0) }, Frame() with { WidthEditor = new() },
            Frame() with { InstructionPositionEditor = new("editing") }, Frame() with { MenuPositionEditor = new() } };
        foreach (var frame in frames)
            TestAssert.Throws<InvalidOperationException>(() => (frame with { MenuOptionCallback = owner }).Serialize());
    }

    public static void OnlyBooleanRedrawRetiresTheActiveOpeningPresentation() {
        var opening = new MenuOpening(1, 100);
        foreach (var owner in new[] { new MenuOptionCallback(1, "boolean", "pending"), new(2, "plain", "cooldown") })
            TestAssert.True((Frame() with { MenuOpening = opening, MenuOptionCallback = owner }).Serialize().Length > 0, "Pre-redraw callbacks can retain row opacity");
        TestAssert.Throws<InvalidOperationException>(() => (Frame() with { MenuOpening = opening,
            MenuOptionCallback = new(1, "boolean", "cooldown") }).Serialize());
    }
}
