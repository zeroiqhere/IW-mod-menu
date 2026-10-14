using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// The actual sparse-memory reader feeds the same immutable capture used by group operations
internal static class PlayerCohortBridgeTests {
    public static void ReaderOutputCreatesACompleteCaptureOnlyWhenEverySupportedSlotIsKnown() {
        var fixture = new PlayerStateMemoryFixture();
        var observation = PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, 4242, fixture.ReadBytes);
        var capture = PlayerCohortCapture.TryCreate(observation);
        TestAssert.True(capture is not null && capture.Identities.Count == 4, "Fully linked reader output cannot enter cohort capture");
        fixture.Int32(PlayerStateMemoryFixture.Entity(3) + 0x1E0, 1);
        var downed = PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, 4242, fixture.ReadBytes);
        TestAssert.True(capture!.Matches(downed) && !capture.Matches(downed, requireEnableEligible: true),
            "The reader and capture disagree about downed membership versus enable eligibility");
        fixture.Int32(PlayerStateMemoryFixture.Server(3) + 8, 0);
        var unknown = PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, 4242, fixture.ReadBytes);
        TestAssert.True(PlayerCohortCapture.TryCreate(unknown) is null && !capture.Matches(unknown),
            "Four-record capacity turned an unproved free slot into a complete lobby");
    }

    public static void LinkedReaderActorsWithAnAliasedAccountNeverCreateACompleteCapture() {
        var fixture = new PlayerStateMemoryFixture();
        TestAssert.True(PlayerCohortCapture.TryCreate(PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase,
            4242, fixture.ReadBytes)) is not null, "Positive reader capture failed");
        fixture.Text(PlayerStateMemoryFixture.Server(1) + 0x9F340, PlayerStateMemoryFixture.Guid(0), 21);
        var aliased = PlayerStateReader.ReadCohort(PlayerStateMemoryFixture.ModuleBase, 4242, fixture.ReadBytes);
        TestAssert.True(aliased.Slots.All(slot => slot.Presence == PlayerSlotPresence.Present),
            "The account collision fixture did not reach capture validation");
        TestAssert.True(PlayerCohortCapture.TryCreate(aliased) is null,
            "An individual linked identity cannot stand in for distinct whole-cohort accounts");
    }
}
