using System.Text.Json;
using syndishanx_iw_tool.Overlay;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

// Public record copies must remain subject to the same wire validation as controller snapshots
internal static class InstructionHudWireTests {
    public static void TypedFooterRejectsMalformedFieldsAndMismatchedEligibilityWithoutRejectingClosedMenus() {
        var current = new MenuChromeFixture().Current();
        // Legacy shape checks intentionally omit the newer dependent callback journal
        var frame = current with { InstructionTheme = null };
        var valid = new InstructionHudState(true, 1, frame.LocalMenuEligibility!.Revision, "Apparition", false, true);
        // Retaining authoritative requests with an incompatible or missing parent must still reject
        foreach (var incompatible in new InstructionHudState?[] { valid, InstructionHudState.Absent, null }) {
            TestAssert.Throws<InvalidOperationException>(() => (current with { InstructionHud = incompatible }).Serialize());
        }
        using var json = JsonDocument.Parse((frame with { InstructionHud = valid }).Serialize());
        TestAssert.Equal(1UL, json.RootElement.GetProperty("instructionHud").GetProperty("creationRevision").GetUInt64());
        foreach (var invalid in new[] { valid with { CreationRevision = 0 },
            valid with { AdmissionRevision = 0 }, valid with { CreationDesign = "Unknown" },
            valid with { CreationAlternate = null }, valid with { CreationBackground = null },
            valid with { Exists = false }, valid with { AdmissionRevision = valid.AdmissionRevision + 1 } }) {
            TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = invalid }).Serialize());
        }
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = valid, LocalMenuEligibility = null }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = valid,
            LocalMenuEligibility = frame.LocalMenuEligibility with { Life = "unknown" } }).Serialize());
        TestAssert.Throws<InvalidOperationException>(() => (frame with { InstructionHud = valid,
            Appearance = frame.Appearance! with { InstructionsVisible = false } }).Serialize());
        using var maximum = JsonDocument.Parse((frame with { InstructionHud = valid with { CreationRevision = ulong.MaxValue } }).Serialize());
        TestAssert.Equal(ulong.MaxValue, maximum.RootElement.GetProperty("instructionHud").GetProperty("creationRevision").GetUInt64());
        // Both independent creation and admission counters retain the full unsigned protocol range
        using var admissionMaximum = JsonDocument.Parse((frame with {
            LocalMenuEligibility = frame.LocalMenuEligibility with { Revision = ulong.MaxValue },
            InstructionHud = valid with { AdmissionRevision = ulong.MaxValue }
        }).Serialize());
        TestAssert.Equal(ulong.MaxValue, admissionMaximum.RootElement.GetProperty("instructionHud").GetProperty("admissionRevision").GetUInt64());
        using var absent = JsonDocument.Parse((frame with { InstructionHud = InstructionHudState.Absent }).Serialize());
        TestAssert.True(!absent.RootElement.GetProperty("instructionHud").GetProperty("exists").GetBoolean(), "Explicit absence is distinct from legacy omission");
        using var legacy = JsonDocument.Parse((frame with { InstructionHud = null }).Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("instructionHud", out _), "Null remains a legacy omitted descriptor");
    }
}
