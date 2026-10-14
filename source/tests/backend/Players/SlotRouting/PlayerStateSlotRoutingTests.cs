using System.IO;
using syndishanx_iw_tool.Players;

namespace Backend.Tests;

// Sparse owned bytes exercise the zero record without a process or a memory writer
internal static class PlayerStateSlotRoutingTests {
    private static long Entity => PlayerStateMemoryFixture.Entity(0);
    private static long Client => PlayerStateMemoryFixture.Client(0);
    private static long Server => PlayerStateMemoryFixture.Anchor;

    public static void SlotZeroReadsTheSelectedAnchorAndPreservesAllFlagsBits() {
        var fixture = new PlayerStateMemoryFixture();
        fixture.Int32(Entity + 0x1C8, unchecked((int)0xA1B2C3D2U));

        var state = fixture.Read(0);

        TestAssert.Equal(0, state.Identity.Slot);
        TestAssert.Equal(Server, state.Identity.ServerClient);
        TestAssert.Equal(Server, state.Identity.ServerClients);
        TestAssert.Equal(Entity, state.Identity.Entity);
        TestAssert.Equal(Client, state.Identity.Client);
        TestAssert.Equal(PlayerStateMemoryFixture.Name(0), state.Identity.Name);
        TestAssert.Equal(PlayerStateMemoryFixture.Guid(0), state.Identity.Guid);
        TestAssert.Equal(0xA1B2C3D2U, state.Flags);
        TestAssert.Equal(100, state.Health);
        TestAssert.Equal(100, state.CommandTime);
        TestAssert.True(fixture.Reads.Count(read => read.Address == PlayerStateMemoryFixture.Table) >= 3,
            "Slot zero must retain the shared-anchor and selected-pointer rechecks");
    }

    public static void SlotZeroRejectsEntityClientServerNameAndGuidMismatches() {
        // Each failure must reach its original guard rather than the former slot policy
        var cases = new (Action<PlayerStateMemoryFixture> Change, string Reason)[] {
            (f => f.Int16(Entity, 1), "selected slot"),
            (f => f.Int32(Entity + 0x9C, 1), "selected slot"),
            (f => f.Int32(Client + 0x4A90, 1), "normal active play"),
            (f => f.Int32(Client + 0x4A94, 1), "normal active play"),
            (f => f.Int32(Client + 0x4BDC, 1), "normal active play"),
            (f => f.Int32(Client + 4, 1), "normal active play"),
            (f => f.Text(Client + 0x4C50, "Other", 32), "client names"),
            (f => f.Int32(Server + 8, 4), "host server record"),
            (f => f.Int64(Server + 136, PlayerStateMemoryFixture.Entity(1)), "host server record"),
            (f => f.Text(Server + 1188, "Other", 32), "host server record"),
            (f => f.Text(Server + 0x9F340, "Z123456789ABCDEF", 21), "non-hexadecimal"),
            (f => f.Text(Server + 0x9F340, "1234", 21), "sixteen bounded"),
            (f => f.Bytes(Client + 0x4BB0, Enumerable.Repeat((byte)'A', 32).ToArray()), "bounded terminator"),
            (f => f.Bytes(Client + 0x4BB0, new byte[] { 0xFF, 0 }), "UTF-8"),
            (f => f.Text(Client + 0x4BB0, " ", 32), "whitespace"),
            (f => f.Text(Client + 0x4BB0, "A\n", 32), "control characters")
        };
        foreach (var item in cases) {
            var fixture = new PlayerStateMemoryFixture();
            item.Change(fixture);
            Reject(() => fixture.Read(0), item.Reason);
        }
    }

    public static void SlotZeroRetainsInitialAndRepeatedMapAndServerCountGuards() {
        var cases = new (long Address, Action<PlayerStateMemoryFixture> Change, string Reason)[] {
            (PlayerStateMemoryFixture.CountAddress, f => f.Int32(PlayerStateMemoryFixture.CountAddress, 3), "client count"),
            (PlayerStateMemoryFixture.ModuleBase + 0x2246C38, f => f.Int32(PlayerStateMemoryFixture.ModuleBase + 0x2246C38, 8), "active in a map"),
            (PlayerStateMemoryFixture.ModuleBase + 0x2246C50, f => f.Bytes(PlayerStateMemoryFixture.ModuleBase + 0x2246C50, new byte[] { 1, 1 }), "active in a map"),
            (PlayerStateMemoryFixture.ModuleBase + 0x2246C51, f => f.Bytes(PlayerStateMemoryFixture.ModuleBase + 0x2246C51, new byte[] { 0 }), "active in a map"),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, f => f.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D4, 1), "changing its map"),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D8, f => f.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9D8, 1), "changing its map"),
            (PlayerStateMemoryFixture.ModuleBase + 0x6B2C9DC, f => f.Int32(PlayerStateMemoryFixture.ModuleBase + 0x6B2C9DC, 1), "changing its map")
        };
        foreach (var item in cases) {
            foreach (bool late in new[] { false, true }) {
                var fixture = new PlayerStateMemoryFixture();
                if (!late) item.Change(fixture);
                else fixture.BeforeRead = (address, _, count) => {
                    if (address == item.Address && count == 2) item.Change(fixture);
                };
                Reject(() => fixture.Read(0), item.Reason);
            }
        }
    }

    public static void SlotZeroRejectsCanonicalPointerAndSpanFailuresBeforeDereference() {
        foreach (long pointer in new[] { 0L, 0xFFF8L, 0x10009L, 0x7FFFFFFF0008L, 0x7FFFFFFF0000L }) {
            var client = new PlayerStateMemoryFixture();
            client.Int64(Entity + 0x170, pointer);
            Reject(() => client.Read(0), pointer == 0x7FFFFFFF0000L ? "span" : "entity client");
            TestAssert.True(!client.Reads.Any(read => read.Address == pointer + 0x4A90),
                "The client span must be bounded before its contents are requested");

            var server = new PlayerStateMemoryFixture();
            server.Int64(PlayerStateMemoryFixture.Table, pointer);
            Reject(() => server.Read(0), pointer == 0x7FFFFFFF0000L ? "span" : "server clients");
            TestAssert.True(!server.Reads.Any(read => read.Address == pointer + 8),
                "The selected server span cannot be read through a failed pointer");
        }
        int calls = 0;
        Reject(() => PlayerStateReader.Read(0x7FFFFFFF0000L, 0, (_, _) => {
            calls++;
            throw new InvalidOperationException("Callback must not run");
        }), "span");
        TestAssert.Equal(0, calls);
    }

    public static void SlotZeroTableReplacementRejectsEqualRecordsWithoutAnchorFallback() {
        foreach (int replacementRead in new[] { 2, 3 }) {
            var fixture = new PlayerStateMemoryFixture();
            long replacement = Server + 0x200000;
            fixture.ServerRecord(replacement, 0);
            fixture.BeforeRead = (address, _, count) => {
                if (address == PlayerStateMemoryFixture.Table && count == replacementRead)
                    fixture.Int64(address, replacement);
            };

            Reject(() => fixture.Read(0), "identity changed");
        }
        // Consistent new text still changes the retained identity on the repeated pass
        var identityChanges = new Action<PlayerStateMemoryFixture>[] {
            f => {
                f.Text(Client + 0x4BB0, "Changed", 32);
                f.Text(Client + 0x4C50, "Changed", 32);
                f.Text(Server + 1188, "Changed", 32);
            },
            f => f.Text(Server + 0x9F340, "0123456789ABCDEF", 21)
        };
        foreach (var change in identityChanges) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.BeforeRead = (address, _, count) => {
                if (address == PlayerStateMemoryFixture.CountAddress && count == 2) change(fixture);
            };
            Reject(() => fixture.Read(0), "identity changed");
        }

        var missing = new PlayerStateMemoryFixture();
        missing.Int64(PlayerStateMemoryFixture.Table, 0);
        Reject(() => missing.Read(0), "server clients");
        TestAssert.True(!missing.Reads.Any(read => read.Address == Server + 8),
            "Old valid anchor contents cannot replace a missing table zero pointer");
    }

    public static void SlotZeroRequiresStrictLivingScalarsIncludingLateDownedChanges() {
        foreach (int health in new[] { 2, 10000 }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.Int32(Entity + 0x1E0, health);
            fixture.Int32(Client, 0);
            TestAssert.Equal(health, fixture.Read(0).Health);
        }
        foreach (int health in new[] { -1, 0, 1, 10001 }) {
            var fixture = new PlayerStateMemoryFixture();
            fixture.Int32(Entity + 0x1E0, health);
            Reject(() => fixture.Read(0), "supported living state");
        }
        var command = new PlayerStateMemoryFixture();
        command.Int32(Client, -1);
        Reject(() => command.Read(0), "supported living state");
        var late = new PlayerStateMemoryFixture();
        late.BeforeRead = (address, _, count) => {
            if (address == Entity + 0x1E0 && count == 2) late.Int32(address, 1);
        };
        Reject(() => late.Read(0), "supported living state");
    }

    public static void SlotZeroExactReadFailuresAndReusedBuffersCannotInventState() {
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 0,
            (_, width) => new byte[width - 1]));
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 0,
            (_, _) => throw new IOException("Owned fixture failure")));
        var partial = new PlayerStateMemoryFixture();
        TestAssert.Throws<IOException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 0,
            (address, width) => address == PlayerStateMemoryFixture.Table ? null : partial.ReadBytes(address, width)));

        // The callback reuses each size's storage; the reader must own its parsed bytes
        var reused = new PlayerStateMemoryFixture();
        var storage = new Dictionary<int, byte[]>();
        var state = PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, 0, (address, width) => {
            var bytes = reused.ReadBytes(address, width);
            if (bytes is null) return null;
            if (!storage.TryGetValue(width, out var buffer)) storage.Add(width, buffer = new byte[width]);
            bytes.CopyTo(buffer, 0);
            return buffer;
        });
        foreach (var buffer in storage.Values) Array.Clear(buffer);
        TestAssert.Equal(PlayerStateMemoryFixture.Name(0), state.Identity.Name);
        TestAssert.Equal(PlayerStateMemoryFixture.Guid(0), state.Identity.Guid);
        TestAssert.Equal(0x200U, state.Flags);
    }

    public static void UnsupportedSlotDomainStillRejectsBeforeAnyMemoryCallback() {
        foreach (int slot in new[] { int.MinValue, -1, 4, int.MaxValue }) {
            int calls = 0;
            TestAssert.Throws<InvalidOperationException>(() => PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, slot,
                (_, _) => { calls++; return null; }));
            TestAssert.Equal(0, calls);
        }
    }

    // Message matching proves the relevant original guard was reached
    private static void Reject(Action read, string expected) {
        try { read(); }
        catch (InvalidOperationException exception) {
            TestAssert.True(exception.Message.Contains(expected, StringComparison.Ordinal),
                "The original state guard must reject: " + expected);
            return;
        }
        throw new InvalidOperationException("Expected state rejection: " + expected);
    }
}
