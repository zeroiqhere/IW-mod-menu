using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

// The fixture owns every adapter and validates identities without a process
internal sealed class GodmodeRegistryFixture(int count = 4) {
    public PlayerIdentity[] Players { get; } = Enumerable.Range(0, count)
        .Select(PlayerGodmodeRegistryTests.Identity).ToArray();
    public Dictionary<PlayerIdentity, RecordingFlagsTransport> Transports { get; } = new();
    public HashSet<PlayerIdentity> Valid { get; } = new();
    public int Factories { get; private set; }

    public PlayerGodmodeRegistry Create() {
        foreach (PlayerIdentity identity in Players) {
            Transports.Add(identity, new());
            Valid.Add(identity);
        }
        return new(identity => {
            Factories++;
            return new FlagsPlayerGodmodeFeature(Transports[identity], TimeSpan.FromSeconds(10));
        }, identity => Valid.Contains(identity));
    }
}
