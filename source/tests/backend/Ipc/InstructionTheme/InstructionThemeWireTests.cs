using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Wire validation applies equally to public factories and direct record copies
internal static class InstructionThemeWireTests {
    private static InstructionThemeEvent Event(ulong revision = 1, ulong age = 0, int? gap = null) => new(revision, "outline", "#FF0000", age, gap, false);

    public static void StateCopiesRejectMalformedWindowsAndPreserveUint64AndLegacyOmission() {
        var frame = new MenuChromeFixture().Current();
        ulong parent = frame.InstructionHud!.CreationRevision!.Value;
        var valid = new InstructionThemeState(parent, true, new[] { Event(ulong.MaxValue, ulong.MaxValue) });
        using var json = JsonDocument.Parse((frame with { InstructionTheme = valid }).Serialize());
        TestAssert.Equal(ulong.MaxValue, json.RootElement.GetProperty("instructionTheme").GetProperty("events")[0].GetProperty("elapsedMilliseconds").GetUInt64());
        foreach (var invalid in new[] {
            new InstructionThemeState(0, true, Array.Empty<InstructionThemeEvent>()),
            new InstructionThemeState(parent + 1, true, Array.Empty<InstructionThemeEvent>()),
            new InstructionThemeState(parent, true, new[] { Event(0) }),
            new InstructionThemeState(parent, true, new[] { Event() with { Target = "title" } }),
            new InstructionThemeState(parent, true, new[] { Event() with { Accent = "#１２３４５６" } }),
            new InstructionThemeState(parent, true, new[] { Event() with { PredecessorGapMilliseconds = 501 } }),
            new InstructionThemeState(parent, true, new[] { Event() with { PredecessorGapMilliseconds = -1 } })
        }) TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionTheme = invalid }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionTheme = valid, InstructionHud = InstructionHudState.Absent }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionTheme = valid,
            InstructionHud = frame.InstructionHud with { CreationAccent = null, CreationRainbow = null } }).Serialize());
        using var legacy = JsonDocument.Parse((frame with { InstructionTheme = null }).Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("instructionTheme", out _), "Legacy null omits new callback authority");
    }

    public static void StateOwnsImmutableEventsAndValidatesOrderGapToleranceAndPayloadBounds() {
        var frame = new MenuChromeFixture().Current();
        ulong parent = frame.InstructionHud!.CreationRevision!.Value;
        var source = new List<InstructionThemeEvent> { Event(1, 431), Event(2, 0, 430) };
        var owned = new InstructionThemeState(parent, true, source);
        source.Clear();
        TestAssert.Throws<ArgumentNullException>(() => new InstructionThemeState(parent, true, null!));
        TestAssert.True(!new InstructionThemeState(parent, true, new InstructionThemeEvent[] { null! }).IsSupported(),
            "A null invocation cannot become a complete window");
        TestAssert.Equal(2, owned.Events.Count);
        TestAssert.Throws<NotSupportedException>(() => ((IList<InstructionThemeEvent>)owned.Events).Clear());
        (frame with { InstructionTheme = owned }).Serialize();
        foreach (var invalid in new[] {
            new[] { Event(2, 430), Event(1, 0, 430) },
            new[] { Event(1, 430), Event(3, 0, 430) },
            new[] { Event(1, 0), Event(2, 1, 430) },
            new[] { Event(1, 430), Event(2, 0) },
            new[] { Event(1, 430), Event(2, 0, 400) },
            new[] { Event(1, 430), Event(2, 0, 500) },
            Enumerable.Range(1, 9).Select(index => Event((ulong)index)).ToArray()
        }) TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionTheme = new(parent, true, invalid) }).Serialize());
        // An incomplete prefix cannot hide skipped requests inside its retained tail
        var incompleteHole = new InstructionThemeState(parent, false,
            new[] { Event(9, 1000, 500), Event(12, 0, 500) });
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionTheme = incompleteHole }).Serialize());
        var maximum = new InstructionThemeState(parent, false,
            Enumerable.Range(0, 8).Select(index => Event((ulong)index + 1, ulong.MaxValue - (ulong)index, index == 0 ? null : 1)));
        TestAssert.True((frame with { InstructionTheme = maximum }).Serialize().Length < StateDocument.MaximumBytes,
            "The bounded maximum-width integer window must fit the actual payload limit");
    }
}
