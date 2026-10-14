namespace syndishanx_iw_tool.Players;

// A retained actor may remain readable after life or movement disables a new ON
public sealed record OwnedPlayerFlagsState(PlayerIdentity Identity, int CommandTime, uint Flags, bool IsEnableEligible);
