using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

// These membership facts and flag bytes are fixture-owned, including explicit absence
internal sealed class CohortGodmodeFixture : IAsyncDisposable {
    internal int Population = 2;
    internal Func<PlayerCohortObservation>? OnRead;
    internal Func<PlayerIdentity, bool>? OnValidate;
    internal Dictionary<PlayerIdentity, RecordingFlagsTransport> Flags { get; } =
        Enumerable.Range(0, 4).ToDictionary(PlayerCohortCaptureTests.Identity, _ => new RecordingFlagsTransport());
    internal int Factories;
    internal PlayerGodmodeRegistry Registry { get; }
    internal PlayerCohortCapture Capture => PlayerCohortCaptureTests.Capture(Population);
    internal PlayerIdentity Identity(int slot) => PlayerCohortCaptureTests.Identity(slot);
    internal PlayerCohortObservation Read() => OnRead?.Invoke() ?? PlayerCohortCaptureTests.Observation(Population);

    internal CohortGodmodeFixture() {
        Registry = new(identity => {
            Factories++;
            return new FlagsPlayerGodmodeFeature(Flags[identity], TimeSpan.FromHours(1));
        }, identity => OnValidate?.Invoke(identity) ?? Flags.ContainsKey(identity));
    }

    public async ValueTask DisposeAsync() {
        foreach (var flags in Flags.Values) { flags.OnRead = null; flags.OnWrite = null; }
        OnValidate = null;
        var result = await Registry.CloseAsync();
        if (!result.SafeToRelease) throw new InvalidOperationException("Cohort fixture cleanup failed");
    }
}
