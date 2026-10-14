namespace syndishanx_iw_tool.Players.LocalObservation;

// Pure interpretation for an already verified current local entity and client binding
// This result neither establishes identity nor grants menu or feature authorization
public static class NativeLocalPlayerLife {
    // The original session getter maps 0..3 to playing, dead, spectator and intermission
    private const int LastDefinedSession = 3;
    private const int SpectatorSession = 2;

    // Pinned Apparition utilities.gsc Is_Alive requires engine life and a defined nonspectator session
    // https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L1296
    public static LocalPlayerLife Evaluate(int signedHealth, int nativeSessionState) {
        // Unsupported session values cannot turn an incomplete observation into known death
        if (nativeSessionState is < 0 or > LastDefinedSession) return LocalPlayerLife.Unknown;

        // Captured retail IsAlive uses signed health > 0, so downed health 1 still passes
        // The session labels dead and intermission are not separate rejection conditions
        return signedHealth > 0 && nativeSessionState != SpectatorSession
            ? LocalPlayerLife.Alive
            : LocalPlayerLife.Dead;
    }
}
