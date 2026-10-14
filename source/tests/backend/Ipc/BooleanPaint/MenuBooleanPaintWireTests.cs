using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuBooleanPaintWireTests {
    private static MenuSnapshot Snapshot() => new("Apparition", "CONNECTED", new[] {
        new MenuRow("God Mode", "ON", true, new(MenuActionKind.TogglePersonalGodmode), "Personal state")
    }, "");
    private static MenuBooleanPaint Paint() => new(1, new[] { new MenuBooleanPaintRow(0, "OFF") });
    private static StateDocument Frame() => StateDocument.Create(42, 1, true, Snapshot(), 0,
        menuRevision: 2, menuSurface: "quick", menuBooleanPaint: Paint());

    public static void PaintRoundTripsExactFieldsWithoutChangingCurrentFactsOrLegacyPackets() {
        var frame = Frame();
        using var json = JsonDocument.Parse(frame.Serialize());
        var paint = json.RootElement.GetProperty("menuBooleanPaint");
        TestAssert.Equal(2, paint.EnumerateObject().Count());
        TestAssert.Equal(1UL, paint.GetProperty("revision").GetUInt64());
        var row = paint.GetProperty("rows")[0];
        TestAssert.Equal(2, row.EnumerateObject().Count());
        TestAssert.Equal("OFF", row.GetProperty("value").GetString()!);
        TestAssert.Equal("ON", frame.Rows[0].Value);
        TestAssert.True(frame.Rows[0].Enabled, "Paint must not change current action eligibility");
        using var legacy = JsonDocument.Parse(StateDocument.Create(42, 1, true, Snapshot(), 0).Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("menuBooleanPaint", out _), "Legacy packets omit absent paint");
    }

    public static void MalformedPaintAndTargetsRejectAtBothFactoryAndSerialization() {
        var invalid = new[] { new MenuBooleanPaint(0, new[] { new MenuBooleanPaintRow(0, "OFF") }),
            new(1, Array.Empty<MenuBooleanPaintRow>()), new(1, new[] { new MenuBooleanPaintRow(-1, "ON") }),
            new(1, new[] { new MenuBooleanPaintRow(128, "ON") }), new(1, new[] { new MenuBooleanPaintRow(0, "false") }),
            new(1, new[] { new MenuBooleanPaintRow(0, "OFF"), new MenuBooleanPaintRow(0, "ON") }),
            new(1, new[] { new MenuBooleanPaintRow(1, "OFF") }), new(1, null!) };
        foreach (var paint in invalid) {
            TestAssert.Throws<InvalidOperationException>(() => (Frame() with { MenuBooleanPaint = paint }).Serialize());
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, Snapshot(), 0,
                menuRevision: 2, menuBooleanPaint: paint));
        }
        foreach (var row in new[] { new StateRow("Unavailable", "UNAVAILABLE", false), new StateRow("", "", false, true) })
            TestAssert.Throws<InvalidOperationException>(() => (Frame() with { Rows = new[] { row } }).Serialize());
    }

    public static void PaintRequiresVisibleOrdinaryMenuButAllowsOpeningAndCallbackTiming() {
        var invalid = new[] { Frame() with { IsOpen = false }, Frame() with { MenuRevision = null },
            Frame() with { Rows = Array.Empty<StateRow>() }, Frame() with { SelectedIndex = -1 },
            Frame() with { SelectedIndex = 1 }, Frame() with { MenuUnlock = "pause" },
            Frame() with { MenuOpenTransition = new("rebuild") }, Frame() with { WidthEditor = new() },
            Frame() with { MenuCredits = new(0) }, Frame() with { MenuPositionEditor = new() },
            Frame() with { InstructionPositionEditor = new("editing") },
            Frame() with { MenuPreferenceRefresh = new("pending") }, Frame() with { MenuPreferenceRebuild = new("cooldown") },
            Frame() with { MenuDesignTransition = new("rebuild") } };
        foreach (var frame in invalid) TestAssert.Throws<InvalidOperationException>(() => frame.Serialize());
        foreach (var frame in new[] { Frame() with { MenuOpening = new(1, 0) },
            Frame() with { MenuOpenTransition = new("cooldown") },
            Frame() with { MenuOptionCallback = new(1, "boolean", "pending") },
            Frame() with { MenuOptionCallback = new(1, "boolean", "cooldown") } })
            TestAssert.True(frame.Serialize().Length > 0, "Visible timing stages may retain copied boolean paint");
    }

    public static void DifferingCurrentStateDisclosureSurvivesAFullLengthStatusNotice() {
        var snapshot = Snapshot() with { Notice = new string('x', 1500) };
        var frame = StateDocument.Create(42, 1, true, snapshot, 0, menuRevision: 2, menuBooleanPaint: Paint());
        TestAssert.True(frame.Notice.Contains("Current God Mode: ON. Menu value is from the last refresh", StringComparison.Ordinal),
            "A retained OFF indicator cannot consume the notice budget before the current ON disclosure");
        TestAssert.True(frame.Notice.Length <= 1500 && frame.Notice.Contains("xxxxx", StringComparison.Ordinal),
            "The bounded disclosure must retain available status text");
        TestAssert.True(frame.Serialize().Length <= StateDocument.MaximumBytes, "Long notices retain the packet byte bound");
    }

    public static void FocusProjectionHidesPaintAndPreservesTheOwnersImmutableDisplay() {
        var owner = Frame(); var hidden = StateFocusProjection.Apply(owner, false);
        TestAssert.True(!hidden.IsOpen && hidden.MenuBooleanPaint is null, "Focus-hidden state cannot publish visible paint");
        TestAssert.True(hidden.Serialize().Length > 0 && owner.MenuBooleanPaint is not null,
            "Hiding focus must preserve owner paint without violating the wire contract");
    }
}
