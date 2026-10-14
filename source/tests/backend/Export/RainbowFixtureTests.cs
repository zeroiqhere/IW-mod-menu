using System.Text.Json;

namespace Backend.Tests;

internal static class RainbowFixtureTests {
    public static void ActualRainbowFixturesExposeExclusiveModeIdempotentUseAndFixedStopWithoutChangingOlderProfiles() {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var enabled = fixtures["theme-colors-rainbow.json"];
        var repeated = fixtures["theme-colors-rainbow-no-op.json"];
        var stopped = fixtures["theme-colors-rainbow-stopped.json"];
        foreach (var state in new[] { enabled, repeated }) {
            TestAssert.True(SmoothRainbowThemeTests.Mode(state), "Rainbow profiles must publish the actual enabled mode");
            TestAssert.Equal("#3998FE", state.Accent);
            TestAssert.Equal(37, state.Rows.Count);
            TestAssert.Equal(36, state.SelectedIndex);
            TestAssert.Equal("Smooth Rainbow", state.Rows.Single(row => row.Value == "ON").Label);
            TestAssert.True(state.Rows.Take(36).All(row => row.Value == "OFF" && row.Enabled), "Fixed rows remain usable but unchecked in rainbow mode");
        }
        TestAssert.Equal(enabled.NavigationRevision, repeated.NavigationRevision);
        TestAssert.Equal(enabled.Appearance, repeated.Appearance);
        TestAssert.True(repeated.LastSequence > enabled.LastSequence, "Repeated Use remains an acknowledged no-op");
        TestAssert.True(!SmoothRainbowThemeTests.Mode(stopped), "A fixed color profile must disable rainbow");
        TestAssert.Equal("#98FF98", stopped.Accent);
        TestAssert.Equal("Mint", stopped.Rows.Single(row => row.Value == "ON").Label);
        TestAssert.Equal("Mint", stopped.Rows[stopped.SelectedIndex].Label);
        foreach (var fixture in fixtures.Where(item => !item.Key.StartsWith("theme-colors-rainbow", StringComparison.Ordinal))) {
            TestAssert.True(!SmoothRainbowThemeTests.Mode(fixture.Value), "Existing profile semantics must retain fixed mode");
        }
        foreach (var state in fixtures.Values) {
            using var json = JsonDocument.Parse(state.Serialize());
            TestAssert.True(json.RootElement.GetProperty("smoothRainbowTheme").ValueKind is JsonValueKind.True or JsonValueKind.False, "Mode metadata must be a JSON boolean");
            TestAssert.True(state.Serialize().Length < Apparition.Backend.Ipc.StateDocument.MaximumBytes, "Every actual packet must remain bounded");
        }
    }
}
