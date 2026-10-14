using Apparition.Backend.Ipc;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // This owner-loop state is separate from rows, menu chrome, and footer creation
    private LocalInstructionThemeJournal instructionTheme = new();
    internal ulong InstructionThemeCounter => instructionTheme.Counter;

    private LocalInstructionThemeJournal PrepareInstructionThemeInvocation(StateDocument frame,
        string targetAccent, LocalMenuOptionCallbackState? callback) {
        // Revalidate the cached actor scope before preparing any palette side effect
        var proof = ReadInstructionAdmission();
        var hud = frame.InstructionHud;
        if (proof is null || !ReferenceEquals(proof.OwnerToken, instructionHud.Owner)
            || hud is not { Exists: true, CreationBackground: true, CreationAccent: not null, CreationRainbow: not null }
            || hud.AdmissionRevision != proof.Revision || hud.CreationRevision != instructionHud.Created?.CreationRevision) return instructionTheme;
        // The accepted action selects the current design role, not the later displayed or creation design
        string target = appearance.Design == "AIO" ? "background" : "outline";
        // The prepared caller clock keeps invocation and BOOL wait admission transactional
        long timestamp = callback?.StartedTimestamp ?? clock.GetTimestamp();
        return instructionTheme.Append(proof.OwnerToken, hud, target, targetAccent, smoothRainbowTheme, timestamp, clock);
    }
}
