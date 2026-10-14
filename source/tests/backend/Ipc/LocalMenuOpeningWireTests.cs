using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Wire tests exercise actual parsing and publication without a process or key reader
internal static class LocalMenuOpeningWireTests {
    private static CommandEnvelope Parse(string input, string fields) => CommandEnvelope.Parse(
        Encoding.UTF8.GetBytes("{\"version\":1,\"sequence\":7,\"processId\":42,\"input\":\"" + input + "\"" + fields + "}"))!;

    public static void ScopedOpeningsAndDeadBackPreserveAllCapturedEpochs() {
        foreach (var input in new[] { "openMain", "openQuick" }) {
            var command = Parse(input, ",\"navigationRevision\":0,\"openingControlsRevision\":2,\"localAdmissionRevision\":18446744073709551615");
            TestAssert.True(command.Valid, "A complete scoped opening must parse");
            TestAssert.Equal(input == "openMain" ? OverlayInput.OpenMain : OverlayInput.OpenQuick, command.Input!.Value);
            TestAssert.Equal(ulong.MaxValue, command.LocalAdmissionRevision!.Value);
        }
        var back = Parse("jumpBack", ",\"navigationRevision\":0,\"localAdmissionRevision\":3");
        TestAssert.True(back.Valid, "Dead back captures navigation and its observed local owner");
        TestAssert.Equal(OverlayInput.JumpBack, back.Input!.Value);
        TestAssert.Equal(3UL, back.LocalAdmissionRevision!.Value);
    }

    public static void MissingMalformedOrBorrowedOpeningScopesAreAcknowledgedButInvalid() {
        foreach (var fields in new[] { "", ",\"navigationRevision\":0", ",\"navigationRevision\":0,\"openingControlsRevision\":1",
            ",\"navigationRevision\":0,\"localAdmissionRevision\":1", ",\"openingControlsRevision\":1,\"localAdmissionRevision\":1" }) {
            TestAssert.True(!Parse("openQuick", fields).Valid, "Opening must include all captured scope fields");
        }
        foreach (var value in new[] { "0", "-1", "18446744073709551616", "null", "true", "\"1\"", "1.5" }) {
            var command = Parse("openMain", ",\"navigationRevision\":0,\"openingControlsRevision\":1,\"localAdmissionRevision\":" + value);
            TestAssert.Equal(7UL, command.Sequence);
            TestAssert.True(!command.Valid, "A malformed local scope cannot authorize an opening");
        }
        foreach (var input in new[] { "toggle", "select", "up", "close" }) {
            TestAssert.True(!Parse(input, ",\"navigationRevision\":0,\"localAdmissionRevision\":1").Valid,
                "A local opening epoch cannot be attached to an unrelated intent");
        }
        TestAssert.True(!Parse("jumpBack", ",\"localAdmissionRevision\":1").Valid,
            "Dead back cannot omit its navigation capture");
        TestAssert.True(Parse("toggle", "").Valid, "The deliberate legacy bootstrap remains compatible");
    }

    public static void EligibilityPublicationRetainsUnknownFactsAndOpaqueFullWidthEpochs() {
        var doc = new StateDocument(1, 42, 0, 0, false, "#3998FE", "Main", "", "", 0,
            Array.Empty<StateRow>()) { MenuSurface = "quick", LocalMenuEligibility = new(ulong.MaxValue, "unknown", null, null, true) };
        using var json = JsonDocument.Parse(doc.Serialize());
        var facts = json.RootElement.GetProperty("localMenuEligibility");
        TestAssert.Equal(5, facts.EnumerateObject().Count());
        TestAssert.Equal(ulong.MaxValue, facts.GetProperty("revision").GetUInt64());
        TestAssert.Equal(JsonValueKind.Null, facts.GetProperty("hasMenu").ValueKind);
        TestAssert.Equal(JsonValueKind.Null, facts.GetProperty("controlsDisabled").ValueKind);
        TestAssert.Equal("quick", json.RootElement.GetProperty("menuSurface").GetString()!);
        TestAssert.True(facts.GetProperty("blocked").GetBoolean(), "Typed blocking cannot be inferred from display text");
        TestAssert.True(!facts.TryGetProperty("accountId", out _) && !facts.TryGetProperty("identity", out _),
            "Private account and pointer bindings cannot cross the menu wire");
    }

    public static void InvalidDescriptorCopiesAndUnknownMenuFamiliesCannotSerialize() {
        var doc = new StateDocument(1, 42, 0, 0, false, "#3998FE", "Main", "", "", 0, Array.Empty<StateRow>());
        foreach (var facts in new[] { new LocalMenuEligibilityState(0, "alive", true, false, false),
            new LocalMenuEligibilityState(1, "spectator", true, false, false) }) {
            TestAssert.Throws<InvalidOperationException>(() => (doc with { LocalMenuEligibility = facts }).Serialize());
        }
        TestAssert.Throws<InvalidOperationException>(() => (doc with { MenuSurface = "other" }).Serialize());
        using var legacy = JsonDocument.Parse(doc.Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("menuSurface", out _)
            && !legacy.RootElement.TryGetProperty("localMenuEligibility", out _), "Legacy publication omits new descriptors");
    }
}
