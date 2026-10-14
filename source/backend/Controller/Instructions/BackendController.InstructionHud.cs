using syndishanx_iw_tool.Overlay;
using Apparition.Backend.Session;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private LocalInstructionHudLifetime instructionHud = new();
    internal ulong InstructionCreationCounter => instructionHud.Counter;

    private LocalInstructionHudLifetime PrepareInstructionPreference(int preference, MenuAppearance before, MenuAppearance after) {
        if (before == after) return instructionHud;
        if (preference == (int)MenuPreference.InstructionsVisible && !after.InstructionsVisible) return instructionHud.Retire();
        if (preference is not ((int)MenuPreference.InstructionsVisible) and not ((int)MenuPreference.AlternateInstructions)
            and not ((int)MenuPreference.InstructionsBackground)) return instructionHud;
        // These source handlers request a footer refresh independently of the row caller wait
        var proof = session.ReadLocalMenuAdmission();
        return instructionHud.Request(proof?.Observation.ProcessId == processId ? proof.OwnerToken : null, true);
    }

    private LocalMenuAdmissionSnapshot? ReadInstructionAdmission() {
        // This cached copied receipt supplies both footer and eligibility facts for the published frame
        var proof = session.ReadLocalMenuAdmission();
        return proof?.Observation.ProcessId == processId ? proof : null;
    }
}
