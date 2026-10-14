using System.Text.Json;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

internal static class PlayerMenuTargetContractTests {
    internal static readonly PlayerIdentity Identity = new(2, 0x10000, 0x20000, 0x30000,
        4, "Captured peer", "1234567890ABCDEF") { ServerClient = 0x40000 };
    private sealed class EqualOwner {
        public override bool Equals(object? obj) => obj is EqualOwner;
        public override int GetHashCode() => 1;
    }

    public static void EqualLookingOwnersNeverShareTargetsScopesOrRowTopology() {
        object first = new EqualOwner(), second = new EqualOwner();
        var target = new PlayerMenuTarget(Identity, PlayerMenuRoute.Teammate, first, 1);
        var same = new PlayerMenuTarget(Identity with { }, PlayerMenuRoute.Teammate, first, 1);
        var other = new PlayerMenuTarget(Identity, PlayerMenuRoute.Teammate, second, 1);
        TestAssert.True(target.Equals(same) && target.GetHashCode() == same.GetHashCode(), "Copies keep the original owner");
        TestAssert.True(!target.Equals(other), "Equal-looking tokens cannot share target authority");
        TestAssert.True(!new PlayerMenuSelectionScope(first, 0).Equals(new PlayerMenuSelectionScope(second, 0)), "Selection scopes use owner references");
        var navigation = new OverlayNavigation(); navigation.Open();
        MenuRow Row(PlayerMenuTarget value) => new("God Mode", "OFF", true,
            new(MenuActionKind.ToggleTeammateGodmode) { ExpectedTarget = value });
        navigation.SetRows(new[] { Row(target) }); ulong before = navigation.MenuRevision;
        navigation.SetRows(new[] { Row(same) }); TestAssert.Equal(before, navigation.MenuRevision);
        navigation.SetRows(new[] { Row(other) }); TestAssert.Equal(before + 1, navigation.MenuRevision);
    }

    public static void PrivateReceiptsAreExcludedFromEverySerializedContainer() {
        var owner = new object(); var target = new PlayerMenuTarget(Identity, PlayerMenuRoute.Teammate, owner, 1);
        var action = new MenuAction(MenuActionKind.ToggleTeammateGodmode) {
            ExpectedTarget = target, SelectionScope = new(owner, 0) };
        string wire = JsonSerializer.Serialize(action);
        TestAssert.True(!wire.Contains("ExpectedTarget") && !wire.Contains("SelectionScope")
            && !wire.Contains(Identity.Guid), "Action serialization cannot expose private receipt fields");
        TestAssert.Equal("{}", JsonSerializer.Serialize(target));
        TestAssert.Equal("{}", JsonSerializer.Serialize(new PlayerMenuSelectionScope(owner, 0)));
        string state = JsonSerializer.Serialize(new SessionBoundary().State with { SelectedPlayerTarget = target });
        TestAssert.True(!state.Contains("SelectedPlayerTarget") && !state.Contains(Identity.Guid), "State serialization cannot expose target memory or account data");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new PlayerMenuTarget(Identity, (PlayerMenuRoute)9, owner, 1));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new PlayerMenuTarget(Identity, PlayerMenuRoute.Teammate, owner, 0));
    }
}
