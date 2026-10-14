using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests.Controller.BooleanPaint;

internal static class MenuBooleanPaintCacheTests {
    public static void CapturesOnlySourceGameBooleanRowsInLogicalOrder() {
        var rows = new[] {
            Row(MenuActionKind.TogglePersonalGodmode, "ON"),
            Row(MenuActionKind.ToggleLobbyAmmo, "ON"),
            Row(MenuActionKind.RestorePersonalGodmode, "UNKNOWN"),
            Row(MenuActionKind.ChangePreference, "OFF"),
            Row(MenuActionKind.ToggleTeammateGodmode, "OFF", slot: 2)
        };

        var cache = Capture(rows);
        TestAssert.Equal(7UL, cache.Frame.Revision);
        TestAssert.True(cache.Frame.IsSupported(), "The copied frame must satisfy the wire contract");
        TestAssert.Equal(3, cache.Frame.Rows.Count);
        TestAssert.Equal(new MenuBooleanPaintRow(0, "ON"), cache.Frame.Rows[0]);
        TestAssert.Equal(new MenuBooleanPaintRow(2, "UNKNOWN"), cache.Frame.Rows[1]);
        TestAssert.Equal(new MenuBooleanPaintRow(4, "OFF"), cache.Frame.Rows[2]);
    }

    public static void ChangedLiveValuesAndBusyEligibilityPreserveCapturedPaint() {
        var rows = new[] { Row(MenuActionKind.TogglePersonalGodmode, "OFF") };
        var cache = Capture(rows);
        // Feature completion and busy state cannot silently redraw the copied indicator
        var completed = new[] { rows[0] with { Value = "ON", Enabled = false, Detail = "Busy" } };

        TestAssert.True(cache.MatchesTopology(completed), "Live facts and busy state must not redraw cached paint");
        TestAssert.Equal("OFF", cache.Frame.Rows[0].Value);
        TestAssert.True(cache.MatchesTopology(new[] { completed[0] with { Value = "UNKNOWN", Enabled = true } }),
            "Unknown current facts remain separate from cached paint");
    }

    public static void ChangedLabelsOrAnyActionIdentityRejectOldTopology() {
        var row = Row(MenuActionKind.ToggleTeammateGodmode, "ON", slot: 1);
        var cache = Capture(new[] { row });
        var changed = new[] {
            row with { Label = "Other player" },
            row with { Action = row.Action with { Kind = MenuActionKind.TogglePersonalGodmode } },
            row with { Action = row.Action with { Page = MenuPage.QuickMenu } },
            row with { Action = row.Action with { Slot = 2 } },
            row with { Action = row.Action with { ExpectedName = "replacement" } }
        };

        foreach (var candidate in changed) {
            TestAssert.True(!cache.MatchesTopology(new[] { candidate }), "Every action identity change must retire the paint");
        }
        TestAssert.Equal("ON", cache.Frame.Rows[0].Value);
    }

    public static void ChangedSpacerOpeningActionOrderOrCountRejectOldTopology() {
        var rows = new[] {
            Row(MenuActionKind.TogglePersonalGodmode, "ON"),
            Row(MenuActionKind.ChangeMainOpeningAction, "OFF") with { OpeningAction = OpeningAction.Aim }
        };
        var cache = Capture(rows);

        TestAssert.True(!cache.MatchesTopology(new[] { rows[0] with { IsSpacer = true }, rows[1] }), "Spacer identity is structural");
        TestAssert.True(!cache.MatchesTopology(new[] { rows[0], rows[1] with { OpeningAction = OpeningAction.Melee } }), "Opening action identity is structural");
        TestAssert.True(!cache.MatchesTopology(rows.Reverse().ToArray()), "Reordered rows change logical paint indices");
        TestAssert.True(!cache.MatchesTopology(rows.Take(1).ToArray()), "Removed nonpaint rows still change topology");
        TestAssert.True(!cache.MatchesTopology(rows.Append(Row(MenuActionKind.None, "")).ToArray()), "Added rows change topology");
    }

    public static void MutableInputAndPublicFrameCannotAlterCapturedState() {
        var original = Row(MenuActionKind.TogglePersonalGodmode, "OFF");
        var rows = new List<MenuRow> { original };
        var cache = Capture(rows);
        // Replace and extend the caller-owned list after capture
        rows[0] = original with { Label = "Replaced", Value = "ON" };
        rows.Add(Row(MenuActionKind.ToggleTeammateGodmode, "ON", slot: 1));

        TestAssert.Equal(1, cache.Frame.Rows.Count);
        TestAssert.Equal("OFF", cache.Frame.Rows[0].Value);
        TestAssert.True(cache.MatchesTopology(new[] { original }), "Input list mutation must not change copied topology");
        // The published list must not expose a writable path into cached paint
        var exposed = (IList<MenuBooleanPaintRow>)cache.Frame.Rows;
        TestAssert.True(exposed.IsReadOnly, "Public paint rows must be read-only");
        TestAssert.Throws<NotSupportedException>(() => exposed[0] = new MenuBooleanPaintRow(0, "ON"));
    }

    public static void EmptyAndNoneligibleMenusProduceNoPaintDescriptor() {
        TestAssert.True(MenuBooleanPaintCache.Capture(1, Array.Empty<MenuRow>()) is null, "Empty menus have no paint");
        var rows = new[] {
            Row(MenuActionKind.ToggleLobbyAmmo, "ON"),
            Row(MenuActionKind.ChangePreference, "ON"),
            Row(MenuActionKind.ToggleHotkeys, "OFF"),
            Row(MenuActionKind.None, "UNAVAILABLE"),
            Row(MenuActionKind.TogglePersonalGodmode, "") with { IsSpacer = true }
        };
        TestAssert.True(MenuBooleanPaintCache.Capture(1, rows) is null, "Plain, local, and spacer rows are excluded");
    }

    public static void InvalidRevisionBoundsAndRowDomainsRejectCapture() {
        var valid = Row(MenuActionKind.TogglePersonalGodmode, "ON");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MenuBooleanPaintCache.Capture(0, new[] { valid }));
        TestAssert.Throws<ArgumentNullException>(() => MenuBooleanPaintCache.Capture(1, null!));
        TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(1, Enumerable.Repeat(valid, 129).ToArray()));
        // Invalid identities and unsupported boolean values fail before a cache is returned
        var malformed = new[] {
            null!, valid with { Label = null! }, valid with { Value = null! }, valid with { Action = null! },
            valid with { Action = valid.Action with { Kind = (MenuActionKind)int.MaxValue } },
            valid with { Action = valid.Action with { Page = (MenuPage)int.MaxValue } },
            valid with { OpeningAction = (OpeningAction)int.MaxValue },
            valid with { Value = "UNAVAILABLE" }, valid with { Value = "on" }
        };
        foreach (var row in malformed) {
            TestAssert.Throws<ArgumentException>(() => MenuBooleanPaintCache.Capture(1, new[] { row }));
        }
    }

    public static void BoundaryCaptureAndMalformedCandidatesRemainSafe() {
        // The last legal logical index remains representable without coercing UNKNOWN
        var rows = Enumerable.Range(0, 128)
            .Select(index => Row(index == 127 ? MenuActionKind.TogglePersonalGodmode : MenuActionKind.None,
                index == 127 ? "UNKNOWN" : "", label: "Row " + index)).ToArray();
        var cache = MenuBooleanPaintCache.Capture(ulong.MaxValue, rows)
            ?? throw new InvalidOperationException("Boundary capture unexpectedly absent");

        TestAssert.Equal(ulong.MaxValue, cache.Frame.Revision);
        TestAssert.Equal(new MenuBooleanPaintRow(127, "UNKNOWN"), cache.Frame.Rows.Single());
        TestAssert.True(!cache.MatchesTopology(null!), "Malformed candidate must fail closed");
        TestAssert.True(!cache.MatchesTopology(new MenuRow[] { null! }), "Null candidate row must fail closed");
        var malformed = (MenuRow[])rows.Clone();
        malformed[0] = malformed[0] with { Action = null! };
        TestAssert.True(!cache.MatchesTopology(malformed), "Malformed same-sized topology must fail closed");
        TestAssert.True(cache.MatchesTopology(rows), "Rejected candidates cannot corrupt the original capture");
    }

    private static MenuBooleanPaintCache Capture(IReadOnlyList<MenuRow> rows) =>
        MenuBooleanPaintCache.Capture(7, rows) ?? throw new InvalidOperationException("Expected captured paint");

    private static MenuRow Row(MenuActionKind kind, string value, int slot = -1, string label = "God Mode") =>
        new(label, value, true, new MenuAction(kind, Slot: slot, ExpectedName: "original"));
}
