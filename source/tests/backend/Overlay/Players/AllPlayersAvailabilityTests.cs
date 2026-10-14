using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class AllPlayersAvailabilityTests {
    private const string IndividualGodmodeRoute =
        "Individual God Mode: Basic Scripts (self) or Players Menu > select player > Basic Scripts (teammate).";

    private static OverlayMenuState ConnectedWithoutCohort(string? status) => new SessionBoundary().State with {
        Connected = true,
        CanConnect = false,
        CanAllPlayersGodmode = false,
        AllPlayersGodmodeValue = "UNKNOWN",
        AllPlayersGodmodeStatus = status!,
        AllPlayersGodmodeTarget = null
    };

    private static MenuRow GodmodeRow(OverlayMenuState state) =>
        OverlayMenuBuilder.Build(MenuPage.AllPlayers, state).Rows[5];

    private static void RequireUnavailableWithoutAction(MenuRow row) {
        TestAssert.Equal("UNAVAILABLE", row.Value);
        TestAssert.True(!row.Enabled && row.Action.Kind == MenuActionKind.None,
            "A missing cohort target must not create an aggregate action");
    }

    public static void MissingCohortKeepsGodModeUnavailableAndShowsCurrentSessionReasonAndIndividualRoute() {
        foreach (string status in new[] {
            "The complete lobby membership is unknown",
            "The original local actor or current host proof changed"
        }) {
            var row = GodmodeRow(ConnectedWithoutCohort(status));
            RequireUnavailableWithoutAction(row);
            TestAssert.True(row.Detail.Contains(status, StringComparison.Ordinal),
                "A connected session's current cohort reason must stay visible");
            TestAssert.True(row.Detail.Contains(IndividualGodmodeRoute, StringComparison.Ordinal),
                "The disabled aggregate row must point to the individual God Mode routes");
        }
    }

    public static void EmptyMissingOrBlockedStatusesUseFallbackWithoutReusingStaleConnectedReason() {
        const string fallback = "A complete verified lobby observation is required.";
        foreach (string? status in new string?[] { "", "   ", null }) {
            var row = GodmodeRow(ConnectedWithoutCohort(status));
            RequireUnavailableWithoutAction(row);
            TestAssert.True(row.Detail.Contains(fallback, StringComparison.Ordinal),
                "An empty session status must retain a useful availability explanation");
            TestAssert.True(row.Detail.Contains(IndividualGodmodeRoute, StringComparison.Ordinal),
                "Fallback help must retain the individual God Mode routes");
        }

        var staleStatus = ConnectedWithoutCohort("Stale connected cohort failure");
        foreach (var blocked in new[] {
            staleStatus with { Preview = true },
            staleStatus with { Connected = false },
            staleStatus with { Busy = true }
        }) {
            var row = GodmodeRow(blocked);
            RequireUnavailableWithoutAction(row);
            TestAssert.True(!row.Detail.Contains("Stale connected cohort failure", StringComparison.Ordinal),
                "A blocked view cannot display an old connected-session reason");
            TestAssert.True(row.Detail.Contains(IndividualGodmodeRoute, StringComparison.Ordinal),
                "A blocked view must keep the individual God Mode routes available as guidance");
        }
    }

    public static void UnavailableReasonUsesBoundedStateNoticeSanitizationAndKeepsIndividualRoute() {
        var state = ConnectedWithoutCohort("Current failure\r\n" + new string('x', 3000) + "\u001bhidden");
        var snapshot = OverlayMenuBuilder.Build(MenuPage.AllPlayers, state);
        var document = StateDocument.Create(42, 1, true, snapshot, 5, isRootMenu: false);
        byte[] serialized = document.Serialize();

        TestAssert.True(serialized.Length <= StateDocument.MaximumBytes,
            "The current status must remain inside the existing IPC size bound");
        TestAssert.True(document.Notice.Length <= 1500,
            "The selected help must use the existing bounded notice path");
        TestAssert.True(document.Notice.Contains(IndividualGodmodeRoute, StringComparison.Ordinal),
            "A long current reason cannot push the individual route out of the published notice");
        TestAssert.True(document.Notice.Contains("Current failure", StringComparison.Ordinal),
            "The sanitized published notice must retain the current session reason");
        TestAssert.True(!document.Notice.Contains('\r') && !document.Notice.Contains('\u001b'),
            "The existing state sanitizer must remove unsupported control characters");
        using var json = JsonDocument.Parse(serialized);
        TestAssert.Equal(document.Notice, json.RootElement.GetProperty("notice").GetString());
    }
}
