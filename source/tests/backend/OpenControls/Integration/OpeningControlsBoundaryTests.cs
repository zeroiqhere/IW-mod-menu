using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class OpeningControlsBoundaryTests {
    public static void CommandPolicyEpochIsOptionalForLegacyButStrictPositiveAndPairedWithObservedContext() {
        var legacy = Parse("{\"version\":1,\"sequence\":1,\"processId\":42,\"input\":\"select\"}");
        TestAssert.True(legacy!.Valid && legacy.OpeningControlsRevision is null, "Existing command packets keep their admission shape");
        foreach (string input in new[] { "select", "toggle" }) {
            foreach (string revision in new[] { "1", ulong.MaxValue.ToString() }) {
                var command = Parse("{\"version\":1,\"sequence\":2,\"processId\":42,\"input\":\"" + input + "\",\"navigationRevision\":0,\"openingControlsRevision\":" + revision + "}");
                TestAssert.True(command!.Valid && command.OpeningControlsRevision is not null, "A bounded positive policy epoch must be parsed");
            }
            foreach (string invalid in new[] { "0", "-1", "null", "true", "\"1\"", "1.0", "18446744073709551616" }) {
                var command = Parse("{\"version\":1,\"sequence\":3,\"processId\":42,\"input\":\"" + input + "\",\"navigationRevision\":0,\"openingControlsRevision\":" + invalid + "}");
                TestAssert.True(command is { Valid: false, Sequence: 3 }, "Malformed epochs must remain recoverable only for acknowledgment");
            }
            var unpaired = Parse("{\"version\":1,\"sequence\":3,\"processId\":42,\"input\":\"" + input + "\",\"openingControlsRevision\":1}");
            TestAssert.True(unpaired is { Valid: false, Sequence: 3 }, "A supplied policy requires its observed navigation epoch");
        }
        foreach (string input in new[] { "up", "close" }) {
            var outOfScope = Parse("{\"version\":1,\"sequence\":3,\"processId\":42,\"input\":\"" + input
                + "\",\"navigationRevision\":0,\"openingControlsRevision\":1}");
            TestAssert.True(outOfScope is { Valid: false, Sequence: 3 }, "Policy epochs belong only to observed opening and option inputs");
        }
    }

    public static void StaleMissingForeignAndLostFocusCommandsCannotCommitOrReplayOpeningChoices() {
        var rig = new OpeningControlsRig();
        rig.Open();
        rig.Highlight("Use");
        var before = rig.Current();
        foreach (var epochs in new (ulong? Navigation, ulong? Policy)[] {
            (null, null), (before.NavigationRevision, null), (null, 1),
            (before.NavigationRevision - 1, 1), (before.NavigationRevision, 2)
        }) {
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true, epochs.Navigation, epochs.Policy), () => true);
            TestAssert.Equal(1UL, OpeningControlsRig.Revision(rig.Current()));
            TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Rejected context cannot start a timer");
        }
        foreach (var input in new[] { OverlayInput.Select, OverlayInput.NextChoice }) {
            int focus = 0;
            rig.Controller.Consume(new(rig.Sequence++, 1, 42, input, true, before.NavigationRevision, 1), () => ++focus == 1);
            rig.Controller.Consume(new(rig.Sequence++, 1, 99, input, true, before.NavigationRevision, 1), () => true);
        }
        TestAssert.Equal(1UL, OpeningControlsRig.Revision(rig.Current()));
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "Foreign or second-focus failure must not be deferred");
        rig.Send(OverlayInput.Select);
        rig.Wait(430);
        var committed = rig.Current();
        rig.Controller.Consume(new(rig.Sequence++, 1, 42, OverlayInput.Select, true, committed.NavigationRevision, 1), () => true);
        TestAssert.Equal(2UL, OpeningControlsRig.Revision(rig.Current()));
        TestAssert.True(rig.Current().MenuPreferenceRefresh is null, "An old policy cannot mutate after the prior wait completes");
        rig.NoGameWork();
    }

    public static void SpacerNavigationSkipsOnlySourceBlanksAndRejectsAnAllSpacerOrSelectedSpacerFrame() {
        var navigation = new OverlayNavigation();
        MenuRow blank = new("", "", false, new(MenuActionKind.None), IsSpacer: true);
        MenuRow unavailable = new("Unavailable", "UNAVAILABLE", false, new(MenuActionKind.None));
        navigation.SetRows(new[] { new MenuRow("Start", "", true, new(MenuActionKind.None)), blank, unavailable, blank });
        navigation.Open();
        navigation.Handle(OverlayInput.Down);
        TestAssert.Equal(2, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Down);
        TestAssert.Equal(0, navigation.SelectedIndex);
        navigation.Handle(OverlayInput.Up);
        TestAssert.Equal(2, navigation.SelectedIndex);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => navigation.SelectIndex(1));
        var before = navigation.MenuRevision;
        TestAssert.Throws<ArgumentException>(() => navigation.SetRows(new[] { blank, blank }));
        TestAssert.Equal(before, navigation.MenuRevision);
        TestAssert.Equal(2, navigation.SelectedIndex);
    }

    public static void MalformedTypedRowsRejectWhileLegacyRowsOmitNewProperties() {
        foreach (var row in new[] {
            new MenuRow("not blank", "", false, new(MenuActionKind.None), IsSpacer: true),
            new MenuRow("", "", true, new(MenuActionKind.None), IsSpacer: true),
            new MenuRow("", "", false, new(MenuActionKind.Connect), IsSpacer: true),
            new MenuRow("bad action", "OFF", true, new(MenuActionKind.None), OpeningAction: (Apparition.Backend.OpenControls.OpeningAction)13)
        }) {
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 0, true,
                new("Test", "PREVIEW", new[] { row }, ""), 0));
        }
        // Even a well-formed source blank cannot be published as the selected row
        TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 0, true,
            new("Test", "PREVIEW", new[] { new MenuRow("", "", false, new(MenuActionKind.None), IsSpacer: true) }, ""), 0));
        using var json = JsonDocument.Parse(StateDocument.Create(42, 0, true,
            new("Test", "PREVIEW", new[] { new MenuRow("Legacy", "OFF", true, new(MenuActionKind.None)) }, ""), 0).Serialize());
        var legacy = json.RootElement.GetProperty("rows")[0];
        TestAssert.True(!legacy.TryGetProperty("isSpacer", out _) && !legacy.TryGetProperty("openingAction", out _), "Old rows remain byte-compatible");
    }

    private static CommandEnvelope? Parse(string json) => CommandEnvelope.Parse(Encoding.UTF8.GetBytes(json));
}
