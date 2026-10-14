namespace syndishanx_iw_tool.Players.LocalBinding;

// Account and linked actor fields are a snapshot, not life or menu authorization
public sealed record LocalAccountSessionBinding(int Controller, ulong AccountId,
    long SelectorPointer, byte SelectorByte, long Session, int MemberIndex, PlayerIdentity Identity);
