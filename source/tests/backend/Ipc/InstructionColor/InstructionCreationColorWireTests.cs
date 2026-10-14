using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Both creation fields form one optional immutable fact, independent of the current mode
internal static class InstructionCreationColorWireTests {
    public static void CreationColorRequiresAPairedStrictHexAndModeWithoutBreakingSixFieldLegacy() {
        var current = new MenuChromeFixture().Current();
        // Legacy shape checks intentionally omit the newer dependent callback journal
        var frame = current with { InstructionTheme = null };
        var legacy = frame.InstructionHud! with { CreationAccent = null, CreationRainbow = null };
        TestAssert.Throws<InvalidOperationException>(() => (current with { InstructionHud = legacy }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (current with { InstructionHud = InstructionHudState.Absent }).Serialize());
        using var legacyJson = JsonDocument.Parse((frame with { InstructionHud = legacy }).Serialize());
        TestAssert.True(legacy.IsSupported(), "Six-field legacy creation remains supported");
        foreach (string value in new[] { "#000000", "#FFFFFF", "#aBcDeF", "#3998FE" }) {
            foreach (bool rainbow in new[] { false, true }) {
                var hud = legacy with { CreationAccent = value, CreationRainbow = rainbow };
                using var json = JsonDocument.Parse((frame with { InstructionHud = hud }).Serialize());
                TestAssert.Equal(value, json.RootElement.GetProperty("instructionHud").GetProperty("creationAccent").GetString());
                TestAssert.Equal(rainbow, json.RootElement.GetProperty("instructionHud").GetProperty("creationRainbow").GetBoolean());
            }
        }
        foreach (var hud in new[] {
            legacy with { CreationAccent = "#3998FE" }, legacy with { CreationRainbow = false },
            InstructionHudState.Absent with { CreationAccent = "#3998FE", CreationRainbow = false }
        }) TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = hud }).Serialize());
        foreach (string invalid in new[] { "", "3998FE", "#12345", "#1234567", "#12GG56", "#１２３４５６", " #123456", "#123456\n" }) {
            var hud = legacy with { CreationAccent = invalid, CreationRainbow = false };
            TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = hud }).Serialize());
        }
        using var absent = JsonDocument.Parse((frame with { InstructionHud = InstructionHudState.Absent }).Serialize());
        TestAssert.Equal(JsonValueKind.Null, absent.RootElement.GetProperty("instructionHud").GetProperty("creationAccent").ValueKind);
        TestAssert.Equal(JsonValueKind.Null, absent.RootElement.GetProperty("instructionHud").GetProperty("creationRainbow").ValueKind);
    }
}
