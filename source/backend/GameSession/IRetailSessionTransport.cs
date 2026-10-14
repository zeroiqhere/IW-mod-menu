using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// The session owns one opened transport until all feature workers have stopped
public interface IRetailSessionTransport : IFeatureTransport, IDisposable {
    bool Open(int processId);
    int ProcessId { get; }
    PlayerState ReadTeammateState(int slot);
    ITeammateFlagsTransport BindTeammate(PlayerIdentity identity);
    IReadOnlyList<DetectedPlayer> ReadPlayerNames();
}
