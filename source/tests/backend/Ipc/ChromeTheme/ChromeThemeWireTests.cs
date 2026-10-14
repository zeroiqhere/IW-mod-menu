using System.Text.Json;
using syndishanx_iw_tool.Overlay.Themes;

namespace Backend.Tests;

// Direct record copies must keep source lifetime and journal linkage rather than bypass the factory
internal static class ChromeThemeWireTests {
    public static void TypedChromeRejectsMalformedHeaderAndTailWhileRetainingLegacyOmission() {
        var rig = ChromeThemeControllerTests.DesignedPalette(0);
        var frame = rig.Current();
        var header = frame.ChromeTheme!;
        ChromeThemeState Copy(ulong? creation = null, ulong? admission = null, string? design = null,
            string? accent = null, int? members = null, bool complete = true, params ChromeThemeEvent[] events) =>
            new(true, creation ?? header.CreationRevision, admission ?? header.AdmissionRevision,
                design ?? header.CreationDesign, accent ?? header.CreationAccent, header.CreationRainbow,
                members ?? header.CurrentRoleMask, complete, events);
        foreach (var invalid in new[] {
            Copy(creation: 0), Copy(admission: 0), Copy(creation: ulong.MaxValue), Copy(admission: ulong.MaxValue),
            Copy(design: "native"), Copy(design: "Native"), Copy(accent: "#GG0000"), Copy(accent: "FF0000"),
            Copy(members: 32), Copy(members: 1),
            Copy(events: new[] { new ChromeThemeEvent(0, 28, "#FF0000", 0, null, false) }),
            Copy(events: new[] { new ChromeThemeEvent(1, 1, "#FF0000", 0, null, false) }),
            Copy(events: new[] { new ChromeThemeEvent(1, 28, "#FF0000", 0, 501, false) }),
            Copy(complete: false, events: new[] { new ChromeThemeEvent(1, 28, "#FF0000", 500, null, false),
                new ChromeThemeEvent(3, 28, "#00FF00", 0, 500, false) }),
            Copy(events: new[] { new ChromeThemeEvent(1, 28, "#FF0000", 100, null, false),
                new ChromeThemeEvent(2, 28, "#00FF00", 0, 430, false) }),
            Copy(events: Enumerable.Range(1, 9).Select(index => new ChromeThemeEvent((ulong)index,
                28, "#FF0000", (ulong)(9 - index) * 500, index == 1 ? null : 500, false)).ToArray())
        }) TestAssert.Throws<InvalidOperationException>(() => (frame with { ChromeTheme = invalid }).Serialize());

        // Destroying Title leaves historical events intact and the remaining source members valid
        var titleGone = Copy(members: 27, events: new[] { new ChromeThemeEvent(1, 28, "#ff0000", ulong.MaxValue, null, false) });
        using var maximum = JsonDocument.Parse((frame with { ChromeTheme = titleGone }).Serialize());
        TestAssert.Equal(ulong.MaxValue, maximum.RootElement.GetProperty("chromeTheme").GetProperty("events")[0]
            .GetProperty("elapsedMilliseconds").GetUInt64());
        using var legacy = JsonDocument.Parse((frame with { ChromeTheme = null }).Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("chromeTheme", out _), "Legacy omission must retain its original wire contract");
        (frame with { ChromeTheme = ChromeThemeState.Absent }).Serialize();
        TestAssert.Throws<InvalidOperationException>(() => (frame with { IsOpen = false }).Serialize());
        TestAssert.Throws<ArgumentNullException>(() => new ChromeThemeState(true, 1, 10, "Apparition", "#3998FE", false, 31, true, null!));
    }

    public static void EventStorageAndProjectionAreImmutableAndUseOneCopiedReportClock() {
        var source = new List<ChromeThemeEvent> { new(1, 28, "#FF0000", 0, null, false) };
        var copied = new ChromeThemeState(true, 1, 10, "Apparition", "#3998FE", false, 31, true, source);
        source.Clear();
        TestAssert.Equal(1, copied.Events.Count);
        TestAssert.True(copied.IsSupported(), "Caller mutation cannot alter an accepted window");

        var clock = new CountingClock();
        var owner = new object();
        var journal = new Apparition.Backend.Controller.LocalChromeThemeJournal();
        var first = journal.Project(owner, 1, 10, "Apparition", "#3998FE", false, false, clock);
        journal = first.State.Append(owner, first.Frame, 28, "#FF0000", false, clock.GetTimestamp(), clock);
        clock.Time.Advance(TimeSpan.FromMilliseconds(430));
        journal = journal.Append(owner, first.Frame, 28, "#00FF00", false, clock.GetTimestamp(), clock);
        clock.Reads = 0;
        var projected = journal.Project(owner, 1, 10, "Apparition", "#000000", true, false, clock);
        TestAssert.Equal(1, clock.Reads);
        TestAssert.Equal(430UL, projected.Frame.Events[0].ElapsedMilliseconds);
        TestAssert.Equal(0UL, projected.Frame.Events[1].ElapsedMilliseconds);
        TestAssert.Equal("#3998FE", projected.Frame.CreationAccent);
        TestAssert.Equal<bool?>(false, projected.Frame.CreationRainbow);
        var discarded = projected.State.Project(owner, 2, 10, "Basic", "#000000", true, false, clock);
        TestAssert.Equal(0, discarded.Frame.Events.Count);
        TestAssert.Equal<ulong?>(1, projected.State.Created!.CreationRevision);
        TestAssert.Equal(2UL, projected.State.Counter);
    }

    private sealed class CountingClock : TimeProvider {
        public ManualTimeProvider Time { get; } = new();
        public int Reads { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() { Reads++; return Time.GetTimestamp(); }
    }
}
