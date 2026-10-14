using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class ScrollAnimationProtocolTests {
    public static void LegacyFactoryOmitsTheNullableMenuEpochWhileControllerFramesAlwaysPublishAnUnsignedContext() {
        var legacy = StateDocument.Create(42, 0, false, new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        using var old = JsonDocument.Parse(legacy.Serialize());
        TestAssert.True(!old.RootElement.TryGetProperty("menuRevision", out _), "Legacy null must be omitted rather than sent as an invalid explicit null");
        TestAssert.Equal(JsonValueKind.Null, old.RootElement.GetProperty("appearance").ValueKind);
        foreach (ulong epoch in new[] { 0UL, ulong.MaxValue }) {
            using var present = JsonDocument.Parse((legacy with { MenuRevision = epoch }).Serialize());
            TestAssert.Equal(epoch, present.RootElement.GetProperty("menuRevision").GetUInt64());
        }
        var controller = new BackendController(42, new SessionBoundary());
        var initial = controller.Snapshot();
        TestAssert.True(initial.MenuRevision.HasValue, "Production controller frames always identify their menu context");
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        TestAssert.True(controller.Snapshot().MenuRevision > initial.MenuRevision, "Opening advances the presentation context");
        var hidden = StateFocusProjection.Apply(controller.Snapshot(), false);
        TestAssert.Equal(controller.Snapshot().MenuRevision, hidden.MenuRevision);
        TestAssert.True(!hidden.IsOpen, "Focus hiding retains the owner epoch without exposing drawing");
    }
}
