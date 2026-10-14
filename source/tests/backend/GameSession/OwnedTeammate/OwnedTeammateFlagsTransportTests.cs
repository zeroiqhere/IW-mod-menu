using System.IO;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// Real bound adapters keep the same owner while fixture callbacks record exact writer arguments
internal static class OwnedTeammateFlagsTransportTests {
    public static void OwnedNonlivingReadsAndOffPreserveTheOriginalBaselineWithoutEnabling() {
        var memory = new PlayerStateMemoryFixture();
        var initial = memory.Read(0);
        bool eligible = false;
        uint flags = 0x201;
        int writes = 0;
        var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true,
            () => new OwnedPlayerFlagsState(initial.Identity, 100, flags, eligible), (address, expected, desired) => {
                TestAssert.Equal(initial.Identity.Entity + 0x1C8, address);
                TestAssert.Equal(flags, expected);
                flags = desired;
                writes++;
                return true;
            });
        TestAssert.Equal(0x201U, transport.ReadFlags());
        TestAssert.True(transport.WriteFlags(0x201, 0x200), "Owned OFF must remain available while nonliving");
        TestAssert.Equal(1, writes);
        TestAssert.True(!transport.WriteFlags(0x200, 0x201), "New ON cannot use a cleanup-only source");
        TestAssert.Equal(1, writes);
        eligible = true;
        TestAssert.True(transport.WriteFlags(0x200, 0x201), "Temporary ineligibility must not retire the exact binding");
        TestAssert.True(!transport.WriteFlags(0x201, 0x203), "An unrelated bit change must not reach the writer");
        TestAssert.True(!transport.WriteFlags(0x200, 0x201), "A changed baseline must not reach the writer");
    }

    public static void WriteTimeDeathCannotBorrowAnEarlierEligiblePreflight() {
        var initial = new PlayerStateMemoryFixture().Read(3);
        bool eligible = true;
        int writes = 0;
        var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true,
            () => new OwnedPlayerFlagsState(initial.Identity, 100, 0x200, eligible), (_, _, _) => { writes++; return true; });
        TestAssert.Equal(0x200U, transport.ReadFlags());
        eligible = false;
        TestAssert.True(!transport.WriteFlags(0x200, 0x201), "Write entry must reject ON after death follows preflight");
        TestAssert.Equal(0, writes);
        TestAssert.True(transport.Connected, "Life changes cannot pretend the original process ended");
    }

    public static void ReplacementTimeRollbackReadFailureAndWriterFailureRetireTheOriginalAdapter() {
        foreach (string fault in new[] { "identity", "time", "read", "write" }) {
            var initial = new PlayerStateMemoryFixture().Read(2);
            bool broken = true;
            int writes = 0;
            var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true, () => {
                if (broken && fault == "read") throw new IOException("Unreadable owned source");
                return new OwnedPlayerFlagsState(broken && fault == "identity" ? initial.Identity with { Client = initial.Identity.Client + 8 } : initial.Identity,
                    broken && fault == "time" ? 99 : 100, 0x200, true);
            }, (_, _, _) => { writes++; if (broken && fault == "write") throw new IOException("Native recheck failed"); return true; });
            TestAssert.Throws<IOException>(() => transport.WriteFlags(0x200, 0x201));
            broken = false;
            TestAssert.Throws<IOException>(() => transport.ReadFlags());
            TestAssert.True(transport.Connected, "Retirement cannot release an original process cleanup lease");
            TestAssert.Equal(fault == "write" ? 1 : 0, writes);
        }
    }

    public static void FinalNativeRecheckRejectsTimeIdentityAndBaselineChangesAfterOuterSample() {
        var initial = new PlayerStateMemoryFixture().Read(2);
        var samples = new Queue<int>(new[] { 105, 102, 106 });
        int writes = 0;
        var access = new OwnedTeammateFlagsAccess(initial, new object(), () => true,
            () => new OwnedPlayerFlagsState(initial.Identity, samples.Dequeue(), 0x200, true),
            (_, _, _) => { writes++; return true; });
        var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true, access.Read, access.Write);
        TestAssert.Throws<IOException>(() => transport.WriteFlags(0x200, 0x201));
        TestAssert.Equal(0, writes);
        TestAssert.Throws<IOException>(() => access.Read());
        TestAssert.True(transport.Connected, "A native time rollback must retain the original cleanup owner");

        foreach (string fault in new[] { "life", "identity", "flags" }) {
            int reads = 0;
            int changedWrites = 0;
            bool changed = true;
            var guarded = new OwnedTeammateFlagsAccess(initial, new object(), () => true, () => {
                bool final = ++reads >= 2 && changed;
                return new OwnedPlayerFlagsState(final && fault == "identity" ? initial.Identity with { Client = initial.Identity.Client + 8 } : initial.Identity,
                    105, final && fault == "flags" ? 0x600U : 0x200U, !(final && fault == "life"));
            }, (_, _, _) => { changedWrites++; return true; });
            var bound = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true, guarded.Read, guarded.Write);
            if (fault == "identity") TestAssert.Throws<IOException>(() => bound.WriteFlags(0x200, 0x201));
            else TestAssert.True(!bound.WriteFlags(0x200, 0x201), "The final native sample must reject changed life or baseline");
            TestAssert.Equal(0, changedWrites);
            changed = false;
            if (fault == "identity") TestAssert.Throws<IOException>(() => guarded.Read());
            else TestAssert.Equal(0x200U, guarded.Read().Flags);
        }
    }

    public static void WithinReadCommandTimeRollbackRejectsBeforeAnyNativeWriterCallback() {
        var memory = new PlayerStateMemoryFixture();
        var initial = memory.Read(1);
        memory.Int32(initial.Identity.Client, 105);
        memory.BeforeRead = (address, _, _) => {
            if (address == initial.Identity.Entity + 0x1C8) memory.Int32(initial.Identity.Client, 102);
        };
        int writes = 0;
        var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true,
            () => PlayerStateReader.ReadOwnedFlags(PlayerStateMemoryFixture.ModuleBase, initial.Identity, memory.ReadBytes),
            (_, _, _) => { writes++; return true; });
        TestAssert.Throws<InvalidOperationException>(() => transport.WriteFlags(0x200, 0x201));
        TestAssert.Equal(0, writes);
    }

    public static void AllNativeOwnerReadsShareTerminalIdentityAndTimeState() {
        var initial = new PlayerStateMemoryFixture().Read(1);
        var times = new Queue<int>(new[] { 106, 105, 105 });
        int writes = 0;
        var registry = new OwnedTeammateFlagsRegistry(new object(), () => true,
            identity => new(identity, times.Dequeue(), 0x200, true),
            (_, _, _) => { writes++; return true; });
        var access = registry.Bind(initial);
        var bound = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true, access.Read, access.Write);
        TestAssert.Equal(106, registry.Read(initial.Identity).CommandTime);
        TestAssert.Throws<IOException>(() => bound.WriteFlags(0x200, 0x201));
        TestAssert.Equal(0, writes);
        TestAssert.Throws<IOException>(() => registry.Read(initial.Identity));

        var strictRegistry = new OwnedTeammateFlagsRegistry(new object(), () => true,
            identity => new(identity, 106, 0x200, true), (_, _, _) => true);
        strictRegistry.Bind(initial);
        strictRegistry.Read(initial.Identity);
        TestAssert.Throws<IOException>(() => strictRegistry.ObserveExisting(initial with { CommandTime = 105 }));
        TestAssert.Throws<IOException>(() => strictRegistry.Read(initial.Identity));

        // Read-only cache replacement never changes the adapter retained by an earlier writer
        var states = Enumerable.Range(0, 4).Select(slot => new PlayerStateMemoryFixture().Read(slot)).ToArray();
        var source = states.ToDictionary(state => state.Identity.Slot,
            state => new OwnedPlayerFlagsState(state.Identity, 100, state.Flags, true));
        var identities = new OwnedTeammateFlagsRegistry(new object(), () => true,
            identity => source[identity.Slot].Identity == identity ? source[identity.Slot] : throw new IOException("Unverified requested identity"),
            (_, _, _) => true);
        var originalAccess = states.Select(state => identities.Bind(state)).ToArray();
        foreach (var state in states) {
            TestAssert.Equal(state.Identity, identities.Read(state.Identity).Identity);
            TestAssert.True(ReferenceEquals(originalAccess[state.Identity.Slot], identities.Bind(state)),
                "Every slot must reuse its original read-access instance");
        }
        var replaced = states[1].Identity with { Guid = "FFFFFFFFFFFFFFFF" };
        TestAssert.Throws<IOException>(() => identities.Read(replaced));
        TestAssert.Equal(states[1].Identity, originalAccess[1].Read().Identity);
        source[1] = new(replaced, 101, 0x200, true);
        TestAssert.Equal(replaced, identities.Read(replaced).Identity);
        TestAssert.Throws<IOException>(() => originalAccess[1].Read());
        source[1] = new(states[1].Identity, 102, 0x200, true);
        TestAssert.Throws<IOException>(() => originalAccess[1].Read());
    }

    public static void LegacyStrictReaderAndExactBitWritesKeepTheirExistingContract() {
        var initial = new PlayerStateMemoryFixture().Read(1);
        bool fail = false;
        uint flags = 0x200;
        var transport = new BoundPlayerFlagsTransport(initial.Identity, initial, () => true,
            () => fail ? throw new IOException("Strict source failed") : initial with { Flags = flags },
            (_, expected, desired) => { if (flags != expected) return false; flags = desired; return true; });
        TestAssert.True(transport.WriteFlags(0x200, 0x201), "Legacy normal ON remains available");
        TestAssert.True(transport.WriteFlags(0x201, 0x200), "Legacy OFF still restores exact bytes");
        fail = true;
        TestAssert.Throws<IOException>(() => transport.ReadFlags());
        fail = false;
        TestAssert.Throws<IOException>(() => transport.ReadFlags());
    }
}
