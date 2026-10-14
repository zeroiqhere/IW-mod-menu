using System.IO;
using syndishanx_iw_tool.Players.LocalBinding;

namespace Backend.Tests;

// The production reader consumes checked fixture bytes without any process or engine call
internal static class LocalAccountSessionReaderTests {
    public static void CurrentAccountBindsTheUniqueRegisteredMemberAndCompleteIdentity() {
        for (int member = 0; member <= 3; member++) {
            var fixture = new LocalBindingMemoryFixture(member);
            var binding = fixture.Read();

            TestAssert.Equal(0, binding.Controller);
            TestAssert.Equal(LocalBindingMemoryFixture.Account, binding.AccountId);
            TestAssert.Equal(member, binding.MemberIndex);
            TestAssert.Equal(LocalBindingMemoryFixture.FalseSession, binding.Session);
            TestAssert.Equal(LocalBindingMemoryFixture.Server(member), binding.Identity.ServerClient);
            TestAssert.Equal(LocalBindingMemoryFixture.Anchor, binding.Identity.ServerClients);
            TestAssert.Equal(LocalBindingMemoryFixture.Entity(member), binding.Identity.Entity);
            TestAssert.Equal(LocalBindingMemoryFixture.Client(member), binding.Identity.Client);
            TestAssert.Equal(LocalBindingMemoryFixture.Name(member), binding.Identity.Name);
            TestAssert.Equal(LocalBindingMemoryFixture.Guid(member), binding.Identity.Guid);
            TestAssert.Equal(4, binding.Identity.ServerCount);
        }
    }

    public static void ExactSelectorUsesEitherSessionBranchWithoutNamingItsPredicate() {
        foreach (byte value in new byte[] { 0, 1, 2, 255 }) {
            var fixture = new LocalBindingMemoryFixture(1, value);
            // A matching account in the other session must not influence the selected session
            long other = value == 0 ? LocalBindingMemoryFixture.TrueSession : LocalBindingMemoryFixture.FalseSession;
            fixture.Registered(other, 2, LocalBindingMemoryFixture.Account);
            var binding = fixture.Read();

            TestAssert.Equal(fixture.Session, binding.Session);
            TestAssert.Equal(value, binding.SelectorByte);
            TestAssert.Equal(LocalBindingMemoryFixture.Selector, binding.SelectorPointer);
            TestAssert.Equal(1, binding.MemberIndex);
            TestAssert.True(!fixture.Reads.Any(read => read.Address == other + 0x60),
                "The inactive session cannot supply membership evidence");
        }
    }

    public static void ZeroMissingAndDuplicateRegisteredAccountsRejectWithoutAnActor() {
        var zero = new LocalBindingMemoryFixture();
        zero.UInt64(LocalBindingMemoryFixture.AccountAddress, 0);
        Reject(zero, "account");

        var missing = new LocalBindingMemoryFixture();
        missing.Registered(missing.Session, 0, LocalBindingMemoryFixture.Account, 0);
        Reject(missing, "registered");

        var duplicate = new LocalBindingMemoryFixture();
        duplicate.Registered(duplicate.Session, 3, LocalBindingMemoryFixture.Account);
        Reject(duplicate, "registered");

        var unrelated = new LocalBindingMemoryFixture();
        unrelated.Registered(unrelated.Session, 0, LocalBindingMemoryFixture.Account + 1);
        Reject(unrelated, "registered");
    }

    public static void ControllerAndSelectedMemberStayWithinTheProvenPcAndServerDomains() {
        foreach (int controller in new[] { -1, 1, int.MaxValue }) {
            var fixture = new LocalBindingMemoryFixture();
            fixture.Int32(LocalBindingMemoryFixture.ControllerAddress, controller);
            Reject(fixture, "controller");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == LocalBindingMemoryFixture.AccountAddress),
                "An unsupported controller cannot index an account field");
        }
        foreach (int member in new[] { 4, 17 }) {
            var fixture = new LocalBindingMemoryFixture(member);
            Reject(fixture, "server");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == LocalBindingMemoryFixture.Table + member * 8),
                "A session member outside four server records cannot index the player table");
        }
        var count = new LocalBindingMemoryFixture();
        count.Int32(LocalBindingMemoryFixture.CountAddress, 3);
        Reject(count, "count");
    }

    public static void RecordDomainRequiresExactStrideAndMatchingSelectedTablePointer() {
        foreach (long pointer in new[] {
            LocalBindingMemoryFixture.Server(1) + 8,
            LocalBindingMemoryFixture.Server(2),
            LocalBindingMemoryFixture.Anchor - 8
        }) {
            var fixture = new LocalBindingMemoryFixture(1);
            fixture.Int64(LocalBindingMemoryFixture.Table + 8, pointer);
            Reject(fixture, "record");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == pointer + 8),
                "An unmatched record domain cannot reach selected server contents");
        }
    }

    public static void InvalidPointersAndArithmeticBoundsRejectBeforeUnsafeReads() {
        foreach (long pointer in new[] { 0L, 0x10001L, 0xFFF8L, 0x7FFFFFFF0008L, 0x7FFFFFFF0000L }) {
            var fixture = new LocalBindingMemoryFixture();
            fixture.Int64(LocalBindingMemoryFixture.SelectorAddress, pointer);
            Reject(fixture, "memory");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == pointer + 0x10),
                "The predicate pointer is checked before its byte is read");
        }
        // Selected server pointers use the same bounds before any record is dereferenced
        foreach (long pointer in new[] { 0L, 0x10001L, 0xFFF8L, 0x7FFFFFFF0008L }) {
            var fixture = new LocalBindingMemoryFixture(1);
            fixture.Int64(LocalBindingMemoryFixture.Table + 8, pointer);
            Reject(fixture, "memory");
            TestAssert.True(!fixture.Reads.Any(read => read.Address == pointer + 8),
                "An invalid selected server pointer cannot reach its record fields");
        }
        var record = new LocalBindingMemoryFixture(3);
        record.Int64(LocalBindingMemoryFixture.Table, 0x7FFFFFFF0000);
        Reject(record, "memory");

        var invalidModule = new LocalBindingMemoryFixture();
        RejectCall(() => LocalAccountSessionReader.Read(0x7FFFFFFF0000, invalidModule.ReadBytes), "memory");
        TestAssert.Equal(0, invalidModule.Reads.Count);
    }

    public static void ChangedSelectorsAccountsSessionsAndMembersRejectRepeatedSnapshot() {
        var unavailable = new LocalBindingMemoryFixture();
        unavailable.Bytes(unavailable.Session + 0x10, new byte[] { 0 });
        Reject(unavailable, "session");
        var changes = new (long Address, Action<LocalBindingMemoryFixture> Change)[] {
            (LocalBindingMemoryFixture.FalseSession + 0x10, fixture => fixture.Bytes(fixture.Session + 0x10, new byte[] { 0 })),
            (LocalBindingMemoryFixture.ControllerAddress, fixture => fixture.Int32(LocalBindingMemoryFixture.ControllerAddress, 1)),
            (LocalBindingMemoryFixture.AccountAddress, fixture => fixture.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1)),
            (LocalBindingMemoryFixture.SelectorAddress, fixture => fixture.Int64(LocalBindingMemoryFixture.SelectorAddress, LocalBindingMemoryFixture.Selector + 8)),
            (LocalBindingMemoryFixture.Selector + 0x10, fixture => fixture.Bytes(LocalBindingMemoryFixture.Selector + 0x10, new byte[] { 1 })),
            (LocalBindingMemoryFixture.FalseSession + 0x60, fixture => fixture.Registered(fixture.Session, 0, LocalBindingMemoryFixture.Account, 0)),
            (LocalBindingMemoryFixture.FalseSession + 0x68, fixture => fixture.UInt64(LocalBindingMemoryFixture.FalseSession + 0x68, LocalBindingMemoryFixture.Account + 1)),
            (LocalBindingMemoryFixture.FalseSession + 3 * 0x38 + 0x60, fixture => fixture.Registered(fixture.Session, 3, LocalBindingMemoryFixture.Account)),
            (LocalBindingMemoryFixture.Table, fixture => fixture.Int64(LocalBindingMemoryFixture.Table, LocalBindingMemoryFixture.Anchor + 8))
        };
        foreach (var item in changes) {
            var fixture = new LocalBindingMemoryFixture();
            fixture.BeforeRead = (address, _, count) => {
                if (address == item.Address && count == 2) item.Change(fixture);
            };
            Reject(fixture, "changed");
        }
    }

    public static void MapTransitionsAndLinkedIdentityChangesRejectBeforeBindingReturns() {
        var changes = new (long Address, Action<LocalBindingMemoryFixture> Change)[] {
            (LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 8, fixture => fixture.Int32(LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 8, 0)),
            (LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 32, fixture => fixture.Bytes(LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 32, new byte[] { 1 })),
            (LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 33, fixture => fixture.Bytes(LocalBindingMemoryFixture.ModuleBase + 0x2246C30 + 33, new byte[] { 0 })),
            (LocalBindingMemoryFixture.ModuleBase + 0x6B2C9D4, fixture => fixture.Int32(LocalBindingMemoryFixture.ModuleBase + 0x6B2C9D4, 1)),
            (LocalBindingMemoryFixture.ModuleBase + 0x6B2C9D8, fixture => fixture.Int32(LocalBindingMemoryFixture.ModuleBase + 0x6B2C9D8, 1)),
            (LocalBindingMemoryFixture.ModuleBase + 0x6B2C9DC, fixture => fixture.Int32(LocalBindingMemoryFixture.ModuleBase + 0x6B2C9DC, 1)),
            (LocalBindingMemoryFixture.Entity(0) + 0x170, fixture => fixture.Int64(LocalBindingMemoryFixture.Entity(0) + 0x170, LocalBindingMemoryFixture.Client(0) + 8)),
            (LocalBindingMemoryFixture.Server(0) + 136, fixture => fixture.Int64(LocalBindingMemoryFixture.Server(0) + 136, LocalBindingMemoryFixture.Entity(1))),
            (LocalBindingMemoryFixture.Client(0) + 0x4BB0, fixture => fixture.Text(LocalBindingMemoryFixture.Client(0) + 0x4BB0, "Changed", 32)),
            (LocalBindingMemoryFixture.Server(0) + 0x9F340, fixture => fixture.Text(LocalBindingMemoryFixture.Server(0) + 0x9F340, "1234567890ABCDEF", 21))
        };
        foreach (var item in changes) {
            var fixture = new LocalBindingMemoryFixture();
            fixture.BeforeRead = (address, _, count) => {
                if (address == item.Address && count == 2) item.Change(fixture);
            };
            Reject(fixture, "changed");
        }
    }

    public static void InvalidIdentityFieldsRemainRejectedWithoutUsingHealthAsLife() {
        var cases = new (Action<LocalBindingMemoryFixture> Change, string Message)[] {
            (fixture => fixture.Int16(LocalBindingMemoryFixture.Entity(0), 1), "slot"),
            (fixture => fixture.Int32(LocalBindingMemoryFixture.Entity(0) + 0x9C, 1), "slot"),
            (fixture => fixture.Int64(LocalBindingMemoryFixture.Entity(0) + 0x170, 0), "memory"),
            (fixture => fixture.Int32(LocalBindingMemoryFixture.Client(0) + 0x4A90, 1), "connected"),
            (fixture => fixture.Int32(LocalBindingMemoryFixture.Client(0) + 0x4BDC, 1), "slot"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Client(0) + 0x4C50, "Other", 32), "names"),
            (fixture => fixture.Int32(LocalBindingMemoryFixture.Server(0) + 8, 4), "server"),
            (fixture => fixture.Int64(LocalBindingMemoryFixture.Server(0) + 136, LocalBindingMemoryFixture.Entity(1)), "server"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Server(0) + 1188, "Other", 32), "server"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Server(0) + 0x9F340, "NOTHEX0123456789", 21), "GUID"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Server(0) + 0x9F340, "1234", 21), "GUID"),
            (fixture => fixture.Bytes(LocalBindingMemoryFixture.Client(0) + 0x4BB0, Enumerable.Repeat((byte)'A', 32).ToArray()), "name"),
            (fixture => fixture.Bytes(LocalBindingMemoryFixture.Client(0) + 0x4BB0, new byte[] { 0xFF, 0 }), "name"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Client(0) + 0x4BB0, " ", 32), "name"),
            (fixture => fixture.Text(LocalBindingMemoryFixture.Client(0) + 0x4BB0, "A\n", 32), "name")
        };
        foreach (var item in cases) {
            var fixture = new LocalBindingMemoryFixture();
            item.Change(fixture);
            Reject(fixture, item.Message);
        }
        var identityOnly = new LocalBindingMemoryFixture();
        identityOnly.Read();
        TestAssert.True(!identityOnly.Reads.Any(read => read.Address == LocalBindingMemoryFixture.Entity(0) + 0x1E0
            || read.Address == LocalBindingMemoryFixture.Client(0) + 4
            || read.Address == LocalBindingMemoryFixture.Client(0) + 0x4A94),
            "Identity binding does not read life, normal movement or menu authorization fields");
    }

    public static void MissingAndIncompleteFieldsNeverYieldABinding() {
        TestAssert.Throws<ArgumentNullException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase, null!));
        TestAssert.Throws<IOException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase, (_, _) => null));
        TestAssert.Throws<IOException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase, (_, width) => new byte[width - 1]));
        TestAssert.Throws<IOException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase, (_, _) => throw new IOException("Owned read failure")));
        var fixture = new LocalBindingMemoryFixture();
        TestAssert.Throws<IOException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase,
            (address, width) => address == fixture.Session + 0x68 ? new byte[7] : fixture.ReadBytes(address, width)));
    }

    public static void AFailedRereadAndMutableCallbackBufferCannotValidateOldFields() {
        var fixture = new LocalBindingMemoryFixture();
        TestAssert.Throws<IOException>(() => LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase,
            (address, width) => {
                byte[]? bytes = fixture.ReadBytes(address, width);
                return address == LocalBindingMemoryFixture.ControllerAddress
                    && fixture.Reads.Count(read => read.Address == address) == 2 ? null : bytes;
            }));
        // Same-width callbacks reuse arrays; the reader must retain independent copies
        var reused = new LocalBindingMemoryFixture();
        var buffers = new Dictionary<int, byte[]>();
        var binding = LocalAccountSessionReader.Read(LocalBindingMemoryFixture.ModuleBase, (address, width) => {
            byte[] exact = reused.ReadBytes(address, width)!;
            if (!buffers.TryGetValue(width, out byte[]? buffer)) {
                buffer = new byte[width];
                buffers.Add(width, buffer);
            }
            exact.CopyTo(buffer, 0);
            return buffer;
        });
        TestAssert.Equal(LocalBindingMemoryFixture.Account, binding.AccountId);
        TestAssert.True(reused.Reads.Count < 256 && reused.Reads.All(read => read.Width <= 32),
            "Two passes stay bounded and request only exact small fields");
    }

    private static void Reject(LocalBindingMemoryFixture fixture, string message) => RejectCall(() => fixture.Read(), message);

    private static void RejectCall(Action read, string message) {
        try { read(); }
        catch (InvalidOperationException exception) {
            TestAssert.True(exception.Message.Contains(message, StringComparison.OrdinalIgnoreCase),
                "The intended local binding guard must reject: " + message);
            return;
        }
        throw new InvalidOperationException("Expected local binding rejection: " + message);
    }
}
