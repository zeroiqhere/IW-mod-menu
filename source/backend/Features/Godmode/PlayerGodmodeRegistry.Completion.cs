using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Completion stays inside the batch rollback boundary and returns its one copied receipt
public sealed partial class PlayerGodmodeRegistry {
    private GodmodeRegistrySnapshot CompleteBatch(PlayerIdentity[] captured, bool desired) {
        GodmodeRegistrySnapshot snapshot = BuildSnapshot(captured);
        if (snapshot.Closed || snapshot.Members.Count != captured.Length) {
            throw new InvalidOperationException("The captured batch could not be confirmed at completion");
        }
        if (desired && snapshot.HasPendingCleanup) {
            throw new InvalidOperationException("An owned change needs cleanup at batch completion");
        }

        for (int index = 0; index < captured.Length; index++) {
            PlayerGodmodeSnapshot member = snapshot.Members[index];
            // No name or slot substitution can stand in for the complete captured identity
            if (member.Identity != captured[index]) {
                throw new InvalidOperationException("The captured player identity changed at completion");
            }
            bool confirmed = desired
                ? member.Connected && member.Active && member.Value == GodmodeValue.On
                : !member.Active && !member.HasPendingRestore
                    && (!member.Connected || member.Value == GodmodeValue.Off);
            if (!confirmed) {
                throw new InvalidOperationException(member.FailureReason
                    ?? "The captured player change was not confirmed at completion");
            }
        }

        // A disconnected restored owner has no live OFF display, but owns no remaining work
        return snapshot;
    }
}
