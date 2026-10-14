using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Only copied actor fixtures are used; no test opens the game or grants a feature writer
internal static class OwnedLocalPlayerObservationProviderTests {
    private sealed class Source : ILocalActorSnapshotSource {
        internal int Reads;
        internal int CurrentProcessId = 4242;
        internal bool IsConnected = true;
        internal Action<CancellationToken>? OnRead;
        internal LocalActorSnapshot Snapshot = Actor();
        public bool Connected => IsConnected;
        public int ProcessId => CurrentProcessId;
        public LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken token) {
            Reads++;
            OnRead?.Invoke(token);
            return Snapshot;
        }
    }

    private static LocalActorSnapshot Actor(bool host = true, int health = 100, int nativeSession = 0,
        int slot = 0, ulong account = 1) {
        var identity = new PlayerIdentity(slot, 0x200000 + slot * 0x3F8, 0x300000 + slot * 0x10000,
            0x400000, 4, "Local actor", "0123456789ABCDEF") { ServerClient = 0x400000 + slot * 715416 };
        var binding = new LocalAccountSessionBinding(0, account, 0x500000, 0, 0x600000, slot, identity);
        return new(binding, health, nativeSession, host);
    }

    private static LocalPlayerObservation Read(OwnedLocalPlayerObservationProvider provider,
        CancellationToken token = default) => provider.ReadAsync(token).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("A completed source read returned no observation");

    public static void VerifiedActorPublishesLifeAndRoleWithoutFabricatingControlLocks() {
        var source = new Source();
        var observed = Read(new(4242, source));
        TestAssert.Equal(1, source.Reads);
        TestAssert.Equal(4242, observed.ProcessId);
        TestAssert.Equal(source.Snapshot.Binding.Identity, observed.Identity);
        TestAssert.Equal(LocalPlayerLife.Alive, observed.Life);
        TestAssert.Equal<bool?>(true, observed.SessionHostRole);
        TestAssert.Equal<bool?>(true, observed.HasMenu);
        TestAssert.Equal<bool?>(null, observed.ControlsDisabled);
    }

    public static void AbsentOrDifferentConnectionNeverReadsOrReturnsAnActor() {
        foreach (bool disconnected in new[] { true, false }) {
            var source = new Source { IsConnected = !disconnected, CurrentProcessId = disconnected ? 4242 : 99 };
            var observed = Read(new(4242, source));
            TestAssert.Equal(0, source.Reads);
            TestAssert.Equal<PlayerIdentity?>(null, observed.Identity);
            TestAssert.Equal<bool?>(null, observed.HasMenu);
            TestAssert.Equal<bool?>(null, observed.SessionHostRole);
        }
    }

    public static void LostConnectionAfterReadCannotPublishOrGrantThePriorActor() {
        foreach (bool closed in new[] { true, false }) {
            var source = new Source();
            var provider = new OwnedLocalPlayerObservationProvider(4242, source);
            source.OnRead = _ => { if (closed) source.IsConnected = false; else source.CurrentProcessId = 99; };
            TestAssert.Equal<PlayerIdentity?>(null, Read(provider).Identity);
            source.IsConnected = true;
            source.CurrentProcessId = 4242;
            source.OnRead = null;
            source.Snapshot = Actor(host: false);
            TestAssert.Equal<bool?>(null, Read(provider).HasMenu);
        }
    }

    public static void CancellationBeforeAndAfterCopiedReadCannotGrantMenuAccess() {
        using var cancellation = new CancellationTokenSource();
        var source = new Source();
        var provider = new OwnedLocalPlayerObservationProvider(4242, source);
        cancellation.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => Read(provider, cancellation.Token));
        TestAssert.Equal(0, source.Reads);
        using var duringRead = new CancellationTokenSource();
        source.OnRead = _ => duringRead.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => Read(provider, duringRead.Token));
        source.OnRead = null;
        source.Snapshot = Actor(host: false);
        TestAssert.Equal<bool?>(null, Read(provider).HasMenu);
    }

    public static void ReadFailuresRevokePublishedFactsWithoutReplacingActorOwnership() {
        var source = new Source();
        var provider = new OwnedLocalPlayerObservationProvider(4242, source);
        TestAssert.Equal<bool?>(true, Read(provider).HasMenu);
        foreach (Exception exception in new Exception[] { new IOException("Incomplete field"),
            new InvalidOperationException("Binding changed"), new System.ComponentModel.Win32Exception(5) }) {
            source.OnRead = _ => throw exception;
            var failed = Read(provider);
            TestAssert.Equal<PlayerIdentity?>(null, failed.Identity);
            TestAssert.Equal(LocalPlayerLife.Unknown, failed.Life);
            TestAssert.Equal<bool?>(null, failed.HasMenu);
        }
        source.OnRead = null;
        source.Snapshot = Actor(host: false, health: 0);
        TestAssert.Equal<bool?>(true, Read(provider).HasMenu);
    }

    public static void AccessGrantPersistsThroughLifeAndRoleChangesButResetsForAnotherActor() {
        var state = new LocalMenuAccessState();
        TestAssert.Equal<bool?>(null, state.Observe(Actor(host: false)));
        TestAssert.Equal<bool?>(true, state.Observe(Actor()));
        foreach (var actor in new[] { Actor(host: false), Actor(host: false, health: 0),
            Actor(host: false, nativeSession: 2), Actor(host: false, nativeSession: 99) }) {
            TestAssert.Equal<bool?>(true, state.Observe(actor));
        }
        TestAssert.Equal<bool?>(null, state.Observe(Actor(host: false, account: 2)));
        TestAssert.Equal<bool?>(true, state.Observe(Actor(host: true, slot: 2, account: 2)));
        TestAssert.Equal<bool?>(null, state.Observe(Actor(host: false)));
        TestAssert.Equal<bool?>(null, new LocalMenuAccessState().Observe(Actor(host: false)));
    }

    public static void UnsupportedLifeRemainsUnknownAndNegativeRoleDoesNotInventScriptPermission() {
        var source = new Source { Snapshot = Actor(host: false, health: 0, nativeSession: 99) };
        var observed = Read(new(4242, source));
        TestAssert.Equal(source.Snapshot.Binding.Identity, observed.Identity);
        TestAssert.Equal(LocalPlayerLife.Unknown, observed.Life);
        TestAssert.Equal<bool?>(false, observed.SessionHostRole);
        TestAssert.Equal<bool?>(null, observed.HasMenu);
        TestAssert.Equal<bool?>(null, observed.ControlsDisabled);
    }

    public static void InvalidObservationShapeAndConstructorArgumentsCannotPublishFacts() {
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new OwnedLocalPlayerObservationProvider(0, new Source()));
        TestAssert.Throws<ArgumentNullException>(() => new OwnedLocalPlayerObservationProvider(4242, null!));
        foreach (bool role in new[] { true, false }) {
            TestAssert.Throws<ArgumentException>(() => new LocalPlayerObservation(0, null,
                LocalPlayerLife.Unknown, null, null, "Unbound", role));
        }
        var original = Actor();
        foreach (var binding in new[] {
            original.Binding with { Controller = 1 },
            original.Binding with { AccountId = 0 },
            original.Binding with { MemberIndex = 1 },
            original.Binding with { Session = 0 },
            original.Binding with { SelectorPointer = 0x500001 },
            Actor(slot: 4).Binding,
            original.Binding with { Identity = original.Binding.Identity with { ServerCount = 3 } },
            original.Binding with { Identity = original.Binding.Identity with { ServerClient = 0 } },
            original.Binding with { Identity = original.Binding.Identity with { ServerClient = 0x400008 } }
        }) {
            var source = new Source { Snapshot = new(binding, 100, 0, true) };
            var observed = Read(new(4242, source));
            TestAssert.Equal<PlayerIdentity?>(null, observed.Identity);
            TestAssert.Equal<bool?>(null, observed.HasMenu);
            TestAssert.Equal<bool?>(null, observed.SessionHostRole);
        }
    }
}
