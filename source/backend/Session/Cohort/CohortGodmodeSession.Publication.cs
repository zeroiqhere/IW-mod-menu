using syndishanx_iw_tool.Features.Godmode;

namespace Apparition.Backend.Session;

// A historical flags snapshot must describe the same cached owners as its completion receipt
public sealed partial class CohortGodmodeSession {
    private static bool ReceiptAgreesWithSnapshot(GodmodeOperationResult result) {
        var members = result.Snapshot.Members;
        var metadata = result.OwnerMetadata?.Members;
        if (metadata is null || metadata.Count != members.Count) return false;
        for (int index = 0; index < members.Count; index++) {
            var member = members[index];
            var owner = metadata[index];
            if (member.Identity != owner.Identity || !member.Connected || !owner.Known
                || owner.Closed || owner.Operating || owner.CleanupRequired || owner.UnconfirmedStop
                || member.Active != owner.Active || member.HasPendingRestore != owner.Pending
                || member.FailureReason != owner.Failure) return false;
            // A monitor stopping after the snapshot cannot remain a current ON receipt
            if (member.Value == GodmodeValue.On && (!owner.Active || !owner.Pending)
                || member.Value == GodmodeValue.Off && (owner.Active || owner.Pending)
                || member.Value is not (GodmodeValue.On or GodmodeValue.Off)) return false;
        }
        return true;
    }
}
