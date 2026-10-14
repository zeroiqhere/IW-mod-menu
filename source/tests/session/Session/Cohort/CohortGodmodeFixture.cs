using Apparition.Backend.Session;
using Features.Tests;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests.Session.Cohort;

// The fixture owns four actual flag features and copied source facts without a process
internal sealed class CohortGodmodeFixture : IAsyncDisposable {
    internal const int ProcessId = 4242;
    internal PlayerIdentity[] Identities { get; } = Enumerable.Range(0, 4).Select(slot =>
        new PlayerIdentity(slot, 0x200000 + slot * 0x1000, 0x300000 + slot * 0x1000,
            0x400000, 4, "Actor" + slot, (slot + 1).ToString("X16")) {
            ServerClient = 0x400000 + slot * 715416L
        }).ToArray();
    internal Dictionary<PlayerIdentity, RecordingFlagsTransport> Flags { get; } = new();
    internal Dictionary<PlayerIdentity, FlagsPlayerGodmodeFeature> Features { get; } = new();
    internal Dictionary<PlayerIdentity, IPlayerGodmodeFeature> RegistryFeatures { get; } = new();
    internal LocalAccountSessionBinding Binding { get; set; }
    internal PlayerGodmodeRegistry Registry { get; }
    internal CohortGodmodeSession Service { get; }
    internal bool Connected = true;
    internal bool Host = true;
    internal bool Registered = true;
    internal bool Eligible = true;
    internal int Health = 100;
    internal int SessionState = 0;
    internal int CommandTime = 100;
    internal int ActorReads;
    internal int CohortReads;
    internal Func<int, LocalActorSnapshot>? OnActor;
    internal Func<PlayerCohortObservation>? OnCohort;

    internal CohortGodmodeFixture(bool sourcesAvailable = true, TimeSpan? stopTimeout = null) {
        Binding = new(0, 123, 0x600000, 0, 0x700000, 0, Identities[0]);
        foreach (var identity in Identities) {
            Flags.Add(identity, new());
            Features.Add(identity, new(Flags[identity], TimeSpan.FromHours(1)));
            RegistryFeatures.Add(identity, Features[identity]);
        }
        if (PlayerCohortCapture.TryCreate(Observation()) is null)
            throw new InvalidOperationException("The source fixture must start with a valid complete cohort");
        Registry = new(identity => RegistryFeatures[identity], identity => Connected && Flags.ContainsKey(identity));
        Service = new(Registry, ProcessId, sourcesAvailable ? ReadCohort : null,
            sourcesAvailable ? ReadActor : null, ReadLocalView, () => Registered,
            () => Connected, stopTimeout);
    }

    internal LocalGodmodeView ReadLocalView() => new(Binding, Binding.Identity,
        GodmodeValue.Off, true, false, "Fixture owner") { HasBoundTarget = true };

    internal LocalActorSnapshot ReadActor(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        int count = Interlocked.Increment(ref ActorReads);
        return OnActor?.Invoke(count) ?? new(Binding, Health, SessionState, Host);
    }

    internal PlayerCohortObservation ReadCohort() {
        Interlocked.Increment(ref CohortReads);
        return OnCohort?.Invoke() ?? Observation();
    }

    internal PlayerCohortObservation Observation() => new(new(ProcessId, 0x140000000,
        0x400000, 4), Identities.Select(identity => new PlayerSlotObservation(identity.Slot,
            PlayerSlotPresence.Present, new(identity, CommandTime, Flags[identity].Flags,
                Eligible))).ToArray());

    internal int Writes => Flags.Values.Sum(transport => transport.Writes);
    internal int Reads => Flags.Values.Sum(transport => transport.Reads);

    public async ValueTask DisposeAsync() {
        // Remove fault barriers before joining the original owners
        OnActor = null;
        OnCohort = null;
        foreach (var transport in Flags.Values) { transport.OnRead = null; transport.OnWrite = null; }
        await Service.StopAsync();
        var result = await Registry.CloseAsync();
        if (!result.SafeToRelease) throw new InvalidOperationException("The fixture retained cleanup work");
    }
}
