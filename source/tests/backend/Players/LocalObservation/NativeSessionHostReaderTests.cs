using System.IO;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class NativeSessionHostReaderTests {
    public static void BothPartyBranchesRecognizeAVerifiedNonzeroHostMember() {
        foreach (bool second in new[] { false, true }) {
            var fixture = new SessionHostMemoryFixture(second, 3);
            TestAssert.True(Read(fixture, 3), "A verified nonzero member can be the session host");
            TestAssert.True(!Read(fixture, 1), "A different verified member is not this party host");
        }
    }

    public static void FirstPartyMatchWinsWithoutInspectingTheSecondParty() {
        var fixture = new SessionHostMemoryFixture(member: 2);
        fixture.Int64(SessionHostMemoryFixture.SecondParty, SessionHostMemoryFixture.Session);
        fixture.Int32(SessionHostMemoryFixture.SecondParty + SessionHostMemoryFixture.HostOffset, 1);
        TestAssert.True(Read(fixture, 2), "The native lookup checks the first party before the second");
        TestAssert.True(!fixture.Reads.Any(item => item.Address == SessionHostMemoryFixture.SecondParty),
            "An unused party is not a source for the selected role");
    }

    public static void NativeNonzeroActivityAndHostComparisonPreserveSignedValues() {
        foreach (int activity in new[] { -1, 1, int.MaxValue }) {
            var fixture = new SessionHostMemoryFixture(member: 2);
            fixture.Int32(SessionHostMemoryFixture.FirstParty + SessionHostMemoryFixture.ActivityOffset, activity);
            TestAssert.True(Read(fixture, 2), "Every captured nonzero activity value selects the host comparison");
            // The captured host field is compared directly, not converted to a guessed local slot
            foreach (int host in new[] { int.MinValue, -1, 0, 3, int.MaxValue }) {
                fixture.Int32(SessionHostMemoryFixture.FirstParty + SessionHostMemoryFixture.HostOffset, host);
                TestAssert.True(!Read(fixture, 2), "A different host integer cannot match verified member two");
            }
        }
    }

    public static void AbsentOrInactivePartiesUseOnlyTheVerifiedMemberZeroFallback() {
        foreach (bool absent in new[] { false, true }) {
            var fixture = new SessionHostMemoryFixture();
            if (absent) {
                fixture.Int64(SessionHostMemoryFixture.FirstParty, 0);
                fixture.Int64(SessionHostMemoryFixture.SecondParty, 0);
            } else {
                fixture.Int32(SessionHostMemoryFixture.FirstParty + SessionHostMemoryFixture.ActivityOffset, 0);
            }
            TestAssert.True(Read(fixture, 0), "The native fallback applies to an already verified member zero");
            TestAssert.True(!Read(fixture, 3), "The fallback does not choose or authenticate member zero");
            TestAssert.True(!fixture.Reads.Any(item => item.Address == SessionHostMemoryFixture.FirstParty + SessionHostMemoryFixture.HostOffset),
                "An inactive or absent party does not require its host field");
        }
    }

    public static void ChangedSessionActivityAndHostFieldsRejectTheConsistencyPass() {
        foreach (bool second in new[] { false, true }) {
            long party = second ? SessionHostMemoryFixture.SecondParty : SessionHostMemoryFixture.FirstParty;
            var changes = new (long Address, Action<SessionHostMemoryFixture> Change)[] {
                (party, item => item.Int64(party, SessionHostMemoryFixture.Session + 8)),
                (party + SessionHostMemoryFixture.ActivityOffset, item => item.Int32(party + SessionHostMemoryFixture.ActivityOffset, 0)),
                (party + SessionHostMemoryFixture.HostOffset, item => item.Int32(party + SessionHostMemoryFixture.HostOffset, 1))
            };
            foreach (var change in changes) {
                var fixture = new SessionHostMemoryFixture(second);
                fixture.BeforeRead = (address, _, count) => {
                    if (address == change.Address && count == 2) change.Change(fixture);
                };
                TestAssert.Throws<InvalidOperationException>(() => Read(fixture, 3));
            }
        }
    }

    public static void AnUnmatchedFirstPartyChangingToMatchRejectsTheSecondPartyResult() {
        var fixture = new SessionHostMemoryFixture(second: true);
        fixture.BeforeRead = (address, _, count) => {
            if (address == SessionHostMemoryFixture.FirstParty && count == 2) {
                fixture.Int64(address, SessionHostMemoryFixture.Session);
            }
        };
        TestAssert.Throws<InvalidOperationException>(() => Read(fixture, 3));
    }

    public static void InvalidBindingArgumentsRejectBeforeAnyReadCallback() {
        foreach (int member in new[] { int.MinValue, -1, 4, int.MaxValue }) {
            var fixture = new SessionHostMemoryFixture();
            TestAssert.Throws<InvalidOperationException>(() => Read(fixture, member));
            TestAssert.Equal(0, fixture.Reads.Count);
        }
        foreach (long session in new[] { 0L, 0xFFF8L, 0x10001L, 0x7FFFFFFF0008L }) {
            var fixture = new SessionHostMemoryFixture();
            TestAssert.Throws<InvalidOperationException>(() => NativeSessionHostReader.Read(
                SessionHostMemoryFixture.ModuleBase, session, 3, fixture.Read));
            TestAssert.Equal(0, fixture.Reads.Count);
        }
        foreach (long moduleBase in new[] { 0L, 0x10001L, 0x7FFFFFFF0008L }) {
            var fixture = new SessionHostMemoryFixture();
            TestAssert.Throws<InvalidOperationException>(() => NativeSessionHostReader.Read(
                moduleBase, SessionHostMemoryFixture.Session, 3, fixture.Read));
            TestAssert.Equal(0, fixture.Reads.Count);
        }
        // The second party's complete span is checked before the first callback too
        var overflow = new SessionHostMemoryFixture();
        TestAssert.Throws<InvalidOperationException>(() => NativeSessionHostReader.Read(
            0x7FFFFFFF0000L - 0x5254DA0, SessionHostMemoryFixture.Session, 3, overflow.Read));
        TestAssert.Equal(0, overflow.Reads.Count);
        TestAssert.Throws<ArgumentNullException>(() => NativeSessionHostReader.Read(
            SessionHostMemoryFixture.ModuleBase, SessionHostMemoryFixture.Session, 3, (Func<long, int, byte[]?>)null!));
    }

    public static void MissingFailedAndIncompleteReadsNeverYieldAHostRole() {
        foreach (Func<long, int, byte[]?> read in new Func<long, int, byte[]?>[] {
            (_, _) => null,
            (_, width) => new byte[width - 1],
            (_, _) => throw new IOException("Owned fixture failure")
        }) {
            TestAssert.Throws<IOException>(() => NativeSessionHostReader.Read(
                SessionHostMemoryFixture.ModuleBase, SessionHostMemoryFixture.Session, 0, read));
        }
        var fixture = new SessionHostMemoryFixture();
        TestAssert.Throws<IOException>(() => NativeSessionHostReader.Read(
            SessionHostMemoryFixture.ModuleBase, SessionHostMemoryFixture.Session, 3,
            (address, width) => address == SessionHostMemoryFixture.FirstParty + SessionHostMemoryFixture.HostOffset
                ? new byte[3] : fixture.Read(address, width)));
        var reread = new SessionHostMemoryFixture();
        // A successful first pass cannot hide a later missing source field
        TestAssert.Throws<IOException>(() => NativeSessionHostReader.Read(
            SessionHostMemoryFixture.ModuleBase, SessionHostMemoryFixture.Session, 3,
            (address, width) => {
                byte[]? bytes = reread.Read(address, width);
                return address == SessionHostMemoryFixture.FirstParty
                    && reread.Reads.Count(item => item.Address == address) == 2 ? null : bytes;
            }));
    }

    public static void ReusedCallbackBuffersAreCopiedAndInternalReadsJoinTheOuterPass() {
        var fixture = new SessionHostMemoryFixture(second: true);
        var buffers = new Dictionary<int, byte[]>();
        var fields = new ObservedBindingReads((address, width) => {
            if (!buffers.TryGetValue(width, out byte[]? buffer)) buffers[width] = buffer = new byte[width];
            fixture.Read(address, width)!.CopyTo(buffer, 0);
            return buffer;
        });
        TestAssert.True(NativeSessionHostReader.Read(SessionHostMemoryFixture.ModuleBase,
            SessionHostMemoryFixture.Session, 3, fields), "Copied role fields preserve the true host result");
        fields.VerifyUnchanged();
        fixture.Int32(SessionHostMemoryFixture.SecondParty + SessionHostMemoryFixture.HostOffset, 1);
        // The outer actor pass retains all role fields and detects a later changed host
        TestAssert.Throws<InvalidOperationException>(fields.VerifyUnchanged);
        TestAssert.True(fixture.Reads.Count < 32 && fixture.Reads.All(item => item.Width is 4 or 8),
            "Host observation remains bounded to exact small fields");
    }

    private static bool Read(SessionHostMemoryFixture fixture, int member) => NativeSessionHostReader.Read(
        SessionHostMemoryFixture.ModuleBase, SessionHostMemoryFixture.Session, member, fixture.Read);
}
