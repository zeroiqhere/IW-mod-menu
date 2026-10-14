using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private static void ValidateInstructionHud(InstructionHudState? hud, LocalMenuEligibilityState? eligibility, MenuAppearance? appearance) {
        if (hud is null) return;
        if (!hud.IsSupported()) throw new InvalidOperationException("Unsupported instruction HUD shape");
        if (!hud.Exists) return;
        // A declared footer cannot borrow a different actor epoch or unknown life permission
        if (eligibility is null || eligibility.Revision != hud.AdmissionRevision
            || eligibility.Life is not ("alive" or "dead") || eligibility.HasMenu != true
            || !(appearance ?? MenuAppearance.Default).InstructionsVisible)
            throw new InvalidOperationException("Instruction HUD requires matching known local menu eligibility");
    }
}
