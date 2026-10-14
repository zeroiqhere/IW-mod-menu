using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

internal static class BoundLocalActorFlagsTransportTests {
    internal static void EveryVerifiedLocalSlotWritesOnlyItsOwnGodmodeBitAndRestoresIt() {
        for (int slot = 0; slot < 4; slot++) {
            var fixture = new BoundLocalActorFlagsFixture(slot);
            var transport = fixture.Bind();
            // Binding itself cannot read, change flags or launch a monitor
            TestAssert.Equal(0, fixture.Reads);
            TestAssert.Equal(0, fixture.Writes.Count);
            TestAssert.True(transport.Connected, "The original process remains connected");
            TestAssert.Equal(0x20U, transport.ReadFlags());
            TestAssert.True(transport.WriteFlags(0x20, 0x21), "The one owned bit can be enabled");
            TestAssert.True(transport.WriteFlags(0x21, 0x20), "The same owned bit can be restored");
            TestAssert.Equal(2, fixture.Writes.Count);
            TestAssert.True(fixture.Writes.All(write => write.Address == fixture.Current.Binding.Identity.Entity + 0x1C8),
                "A local nonzero slot must never write Player 1's entity");
            TestAssert.Equal(0x20U, transport.ReadFlags());
        }
    }

    internal static void FullAccountSessionAndActorReplacementPermanentlyRejectsTheOldBinding() {
        // A same name or entity address cannot make a replacement account or session equal
        Action<BoundLocalActorFlagsFixture>[] replacements = [
            f => f.ChangeBinding(f.Current.Binding with { AccountId = 2 }),
            f => f.ChangeBinding(f.Current.Binding with { Session = f.Current.Binding.Session + 8 }),
            f => f.ChangeBinding(f.Current.Binding with { SelectorByte = 1 }),
            f => f.ChangeBinding(f.Current.Binding with { Identity = f.Current.Binding.Identity with { Client = 0x500000000 } })
        ];
        foreach (var replace in replacements) {
            var fixture = new BoundLocalActorFlagsFixture(2);
            var original = fixture.Current;
            var transport = fixture.Bind();
            replace(fixture);
            TestAssert.Throws<IOException>(() => transport.ReadFlags());
            fixture.Current = original;
            TestAssert.Throws<IOException>(() => transport.ReadFlags());
            TestAssert.Throws<IOException>(() => transport.WriteFlags(0x20, 0x21));
            TestAssert.Equal(0, fixture.Writes.Count);
        }
    }

    internal static void CommandTimeRollbackAndSourceFailureCannotRecoverIntoAnOldWriter() {
        foreach (bool failedRead in new[] { false, true }) {
            var fixture = new BoundLocalActorFlagsFixture(3);
            var original = fixture.Current;
            var transport = fixture.Bind();
            fixture.Current = new(original.Actor, 1001, 0, original.Flags);
            TestAssert.Equal(0x20U, transport.ReadFlags());
            if (failedRead) fixture.ThrowOnRead = true;
            else fixture.Current = original;
            TestAssert.Throws<IOException>(() => transport.ReadFlags());
            fixture.ThrowOnRead = false;
            fixture.Current = new(original.Actor, 1002, 0, original.Flags);
            TestAssert.Throws<IOException>(() => transport.ReadFlags());
            TestAssert.Equal(0, fixture.Writes.Count);
        }
    }

    internal static void FirstReadFailureRemainsVisibleAcrossRetiredReadsAndWrites() {
        var fixture = new BoundLocalActorFlagsFixture(1);
        var transport = fixture.Bind();
        var firstCause = new IOException("The original local flags read failed");
        fixture.BeforeRead = () => throw firstCause;

        IOException firstFailure = CaptureIOException(() => transport.ReadFlags());
        TestAssert.True(ReferenceEquals(firstCause, firstFailure), "The first read failure is rethrown unchanged");

        // Restoring the source cannot revive the adapter after its first failed read
        fixture.BeforeRead = null;
        IOException laterRead = CaptureIOException(() => transport.ReadFlags());
        IOException laterWrite = CaptureIOException(() => transport.WriteFlags(0x20, 0x21));
        TestAssert.True(laterRead.Message.Contains(firstCause.Message, StringComparison.Ordinal)
            && ReferenceEquals(firstCause, laterRead.InnerException), "A later read must expose the first retirement cause");
        TestAssert.True(laterWrite.Message.Contains(firstCause.Message, StringComparison.Ordinal)
            && ReferenceEquals(firstCause, laterWrite.InnerException), "A later write must expose the first retirement cause");
        TestAssert.Equal(1, fixture.Reads);
        TestAssert.Equal(0, fixture.Writes.Count);
    }

    internal static void FirstWriterFailureRemainsVisibleWithoutRetryingTheWriter() {
        var fixture = new BoundLocalActorFlagsFixture(2);
        var firstCause = new IOException("The native writer rejected its actor validation");
        int writerCalls = 0;
        var transport = new BoundLocalActorFlagsTransport(fixture.Current, () => fixture.Connected,
            fixture.Read, (_, _, _) => {
                writerCalls++;
                throw firstCause;
            });

        IOException firstFailure = CaptureIOException(() => transport.WriteFlags(0x20, 0x21));
        TestAssert.True(ReferenceEquals(firstCause, firstFailure), "The first writer failure is rethrown unchanged");

        IOException laterRead = CaptureIOException(() => transport.ReadFlags());
        IOException laterWrite = CaptureIOException(() => transport.WriteFlags(0x20, 0x21));
        TestAssert.True(laterRead.Message.Contains(firstCause.Message, StringComparison.Ordinal)
            && ReferenceEquals(firstCause, laterRead.InnerException), "A later read must expose the first writer failure");
        TestAssert.True(laterWrite.Message.Contains(firstCause.Message, StringComparison.Ordinal)
            && ReferenceEquals(firstCause, laterWrite.InnerException), "A later write must expose the first writer failure");
        TestAssert.Equal(1, writerCalls);
        TestAssert.Equal(1, fixture.Reads);
    }

    internal static void RollbackAndBindingMismatchRemainDistinctPermanentRetirementCauses() {
        var rollbackFixture = new BoundLocalActorFlagsFixture(0);
        var original = rollbackFixture.Current;
        var rollbackTransport = rollbackFixture.Bind();
        rollbackFixture.Current = new(original.Actor, original.CommandTime - 1, 0, original.Flags);
        IOException rollback = CaptureIOException(() => rollbackTransport.ReadFlags());
        TestAssert.True(rollback.Message.Contains("command time", StringComparison.OrdinalIgnoreCase),
            "A rollback must retain a command-time-specific cause");

        // Returning the original clock value cannot restore the retired reader
        rollbackFixture.Current = original;
        IOException retiredRollback = CaptureIOException(() => rollbackTransport.WriteFlags(0x20, 0x21));
        TestAssert.True(retiredRollback.Message.Contains(rollback.Message, StringComparison.Ordinal)
            && ReferenceEquals(rollback, retiredRollback.InnerException), "A later write must retain the rollback cause");
        TestAssert.Equal(0, rollbackFixture.Writes.Count);

        var bindingFixture = new BoundLocalActorFlagsFixture(0);
        var originalBinding = bindingFixture.Current;
        var bindingTransport = bindingFixture.Bind();
        bindingFixture.ChangeBinding(originalBinding.Binding with { AccountId = originalBinding.Binding.AccountId + 1 });
        IOException replacement = CaptureIOException(() => bindingTransport.ReadFlags());
        TestAssert.True(replacement.Message.Contains("binding changed", StringComparison.OrdinalIgnoreCase),
            "A binding replacement must retain a binding-specific cause");
        TestAssert.True(!StringComparer.Ordinal.Equals(rollback.Message, replacement.Message),
            "A rollback and replacement must remain distinguishable");

        // Restoring the old identity cannot recover the original adapter
        bindingFixture.Current = originalBinding;
        IOException retiredReplacement = CaptureIOException(() => bindingTransport.ReadFlags());
        TestAssert.True(retiredReplacement.Message.Contains(replacement.Message, StringComparison.Ordinal)
            && ReferenceEquals(replacement, retiredReplacement.InnerException), "A later read must retain the binding cause");
        TestAssert.Equal(0, bindingFixture.Writes.Count);
    }

    private static IOException CaptureIOException(Action action) {
        try {
            action();
        } catch (IOException error) {
            return error;
        }
        throw new InvalidOperationException("Expected IOException");
    }

    internal static void DisconnectionBeforeOrAfterTheCopiedReadRejectsBeforeAnyWrite() {
        foreach (bool afterRead in new[] { false, true }) {
            var fixture = new BoundLocalActorFlagsFixture(1);
            var transport = fixture.Bind();
            if (afterRead) fixture.AfterRead = () => fixture.Connected = false;
            else fixture.Connected = false;
            TestAssert.Throws<IOException>(() => transport.WriteFlags(0x20, 0x21));
            TestAssert.Equal(afterRead ? 1 : 0, fixture.Reads);
            TestAssert.Equal(0, fixture.Writes.Count);
        }
    }

    internal static void DifferentFlagBytesAndNonGodmodeRequestsNeverReachTheWriteCallback() {
        var fixture = new BoundLocalActorFlagsFixture(2);
        var transport = fixture.Bind();
        TestAssert.True(!transport.WriteFlags(0x20, 0x20), "An unchanged value is not a bit toggle");
        TestAssert.True(!transport.WriteFlags(0x20, 0x22), "Only the Godmode bit may change");
        TestAssert.Equal(0, fixture.Reads);
        fixture.Current = new(fixture.Current.Actor, 1001, 0, 0x40);
        TestAssert.True(!transport.WriteFlags(0x20, 0x21), "A changed baseline cannot be overwritten");
        TestAssert.Equal(0, fixture.Writes.Count);
        TestAssert.Equal(0x40U, transport.ReadFlags());
    }

    internal static void MalformedLocalBindingsAndMissingCallbacksRejectDuringConstruction() {
        var fixture = new BoundLocalActorFlagsFixture();
        var original = fixture.Current;
        foreach (var malformed in new[] {
            original.Binding with { Controller = 1 }, original.Binding with { AccountId = 0 },
            original.Binding with { MemberIndex = 2 }, original.Binding with { Session = 0 },
            original.Binding with { Identity = original.Binding.Identity with { Entity = 0x7FFFFFFF0000 } },
            original.Binding with { Identity = original.Binding.Identity with { ServerClient = 0x600000000 } }
        }) {
            fixture.ChangeBinding(malformed);
            TestAssert.Throws<ArgumentException>(() => fixture.Bind());
        }
        TestAssert.Throws<ArgumentNullException>(() => new BoundLocalActorFlagsTransport(null!, () => true, () => BoundLocalActorFlagsFixture.Owned(original), fixture.Write));
        TestAssert.Throws<ArgumentNullException>(() => new BoundLocalActorFlagsTransport(original, null!, () => BoundLocalActorFlagsFixture.Owned(original), fixture.Write));
        TestAssert.Throws<ArgumentNullException>(() => new BoundLocalActorFlagsTransport(original, () => true, null!, fixture.Write));
        TestAssert.Throws<ArgumentNullException>(() => new BoundLocalActorFlagsTransport(original, () => true, () => BoundLocalActorFlagsFixture.Owned(original), null!));
        TestAssert.Equal(0, fixture.Reads);
        TestAssert.Equal(0, fixture.Writes.Count);
    }

    internal static void AWriteCallbackValidationFailurePermanentlyRetiresTheOriginalWriter() {
        var fixture = new BoundLocalActorFlagsFixture(2);
        var transport = new BoundLocalActorFlagsTransport(fixture.Current, () => fixture.Connected,
            fixture.Read, (_, _, _) => throw new IOException("The actor changed inside native write admission"));
        TestAssert.Throws<IOException>(() => transport.WriteFlags(0x20, 0x21));
        TestAssert.True(transport.Connected, "A failed actor check cannot pretend the process has ended");
        TestAssert.Throws<IOException>(() => transport.ReadFlags());
        TestAssert.Equal(0, fixture.Writes.Count);
    }

    internal static void OwnedOffAfterDeathRemainsAvailableButNewEnableRequiresNormalPlay() {
        foreach (int session in new[] { 0, 1, 2, 3, 7 }) {
            var fixture = new BoundLocalActorFlagsFixture(3);
            var transport = fixture.Bind();
            TestAssert.True(transport.WriteFlags(0x20, 0x21), "Living admission can enable the owned flag");
            var dead = new LocalActorSnapshot(fixture.Current.Actor.Binding, 0, session, true);
            fixture.OwnedOverride = new(dead, 1001, 7, 0x21);
            TestAssert.Equal(0x21U, transport.ReadFlags());
            TestAssert.True(transport.WriteFlags(0x21, 0x20), "The exact original flag can be restored after death");
            TestAssert.True(!transport.WriteFlags(0x20, 0x21), "A dead or unsupported state cannot start a new enable");
            TestAssert.Equal(2, fixture.Writes.Count);
            TestAssert.Equal(0x20U, transport.ReadFlags());
            TestAssert.True(transport.Connected, "Life changes do not imply that the original process has ended");
        }
    }
}
