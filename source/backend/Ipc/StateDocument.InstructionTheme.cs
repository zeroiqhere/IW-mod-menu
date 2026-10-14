using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private static void ValidateInstructionTheme(InstructionThemeState? theme, InstructionHudState? hud) {
        // Omission keeps legacy behavior without claiming an authoritative callback history
        if (theme is null) return;
        // Present windows must belong to the current paired creation; missing rectangles allow only empty history
        if (!theme.IsSupported() || hud is not { Exists: true, CreationAccent: not null, CreationRainbow: not null }
            || !hud.IsSupported() || theme.CreationRevision != hud.CreationRevision
            || hud.CreationBackground != true && theme.Events.Count != 0)
            throw new InvalidOperationException("Instruction theme requires a matching owned creation and supported invocation window");
    }
}
