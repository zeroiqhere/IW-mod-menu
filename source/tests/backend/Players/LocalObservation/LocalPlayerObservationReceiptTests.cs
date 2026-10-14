using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The receipt uses the compiled actor reader over owned bytes, not an account identity imitation
internal static class LocalPlayerObservationReceiptTests {
    private sealed class Source : ILocalActorSnapshotSource {
        internal readonly LocalActorMemoryFixture Actor = new();
        internal int Reads;
        internal Action? BeforeRead;
        public bool Connected => true;
        public int ProcessId => 4242;
        public LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Reads++;
            BeforeRead?.Invoke();
            return Actor.Read();
        }
    }

    public static void OneCopiedActorReadPublishesItsExactBindingAndObservationTogether() {
        var source = new Source();
        var provider = new OwnedLocalPlayerObservationProvider(4242, source);
        var receipt = provider.ReadReceiptAsync(default).GetAwaiter().GetResult();
        TestAssert.True(receipt is not null, "A complete actor read returned no receipt");
        TestAssert.Equal(1, source.Reads);
        TestAssert.Equal(source.Actor.Read().Binding, receipt!.Binding);
        TestAssert.Equal(receipt.Binding.Identity, receipt.Observation.Identity);
        TestAssert.Equal<bool?>(true, receipt.Observation.HasMenu);
        TestAssert.Equal<bool?>(null, receipt.Observation.ControlsDisabled);
    }

    public static void EqualDisplayedIdentityDoesNotHideAChangedFullAccountReceipt() {
        var source = new Source();
        var provider = new OwnedLocalPlayerObservationProvider(4242, source);
        var first = provider.ReadReceiptAsync(default).GetAwaiter().GetResult();
        var memory = source.Actor.Memory;
        memory.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1);
        memory.Registered(memory.Session, memory.Member, LocalBindingMemoryFixture.Account + 1);
        var second = provider.ReadReceiptAsync(default).GetAwaiter().GetResult();
        TestAssert.True(first is not null && second is not null, "The copied actor receipts are missing");
        TestAssert.Equal(first!.Observation.Identity, second!.Observation.Identity);
        TestAssert.True(first.Binding != second.Binding, "The full account replacement was hidden");
        TestAssert.Equal(2, source.Reads);
    }

    public static void FailedCopiedReadReturnsNoReceiptAndCancellationCannotGrantPermission() {
        var source = new Source();
        var provider = new OwnedLocalPlayerObservationProvider(4242, source);
        source.BeforeRead = () => throw new IOException("Owned actor read is incomplete");
        TestAssert.Equal<LocalPlayerObservationReceipt?>(null, provider.ReadReceiptAsync(default).GetAwaiter().GetResult());
        source.BeforeRead = null;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => provider.ReadReceiptAsync(cancellation.Token).GetAwaiter().GetResult());
        TestAssert.Equal(1, source.Reads);
    }

    public static void ReceiptConstructorRejectsMismatchedOrIncompletePrivateBindings() {
        var actor = new LocalActorMemoryFixture().Read();
        var observed = new LocalPlayerObservation(4242, actor.Binding.Identity, actor.Life, true, null, "Verified actor");
        foreach (var binding in new[] { actor.Binding with { AccountId = 0 },
            actor.Binding with { Controller = 1 }, actor.Binding with { Session = 0 },
            actor.Binding with { Identity = actor.Binding.Identity with { Guid = "FEDCBA9876543210" } } }) {
            TestAssert.Throws<ArgumentException>(() => new LocalPlayerObservationReceipt(binding, observed));
        }
        TestAssert.Throws<ArgumentNullException>(() => new LocalPlayerObservationReceipt(null!, observed));
        TestAssert.Throws<ArgumentNullException>(() => new LocalPlayerObservationReceipt(actor.Binding, null!));
    }
}
