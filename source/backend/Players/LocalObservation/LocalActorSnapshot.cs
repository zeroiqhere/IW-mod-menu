using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// Copied actor facts do not establish menu permission or current command authorization
public sealed record LocalActorSnapshot {
    public LocalAccountSessionBinding Binding { get; }
    public int SignedHealth { get; }
    public int NativeSessionState { get; }
    public LocalPlayerLife Life { get; }
    public bool IsSessionHost { get; }

    public LocalActorSnapshot(LocalAccountSessionBinding binding, int signedHealth,
        int nativeSessionState, bool isSessionHost) {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        SignedHealth = signedHealth;
        NativeSessionState = nativeSessionState;
        Life = NativeLocalPlayerLife.Evaluate(signedHealth, nativeSessionState);
        // Session_IsHost is distinct from the script actor ishost method and its override
        IsSessionHost = isSessionHost;
    }
}
