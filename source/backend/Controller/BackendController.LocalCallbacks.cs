using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private LocalMenuOptionCallbackState? PrepareLocalOptionCallback(string kind, bool immediateBooleanRedraw = false) {
        var state = retainedState ?? session.ReadMenuState();
        // Legacy and inert preview producers retain their existing immediate local behavior
        if (state.Preview || !state.Connected || admittedMenuOwner is null) return null;
        // A retired source visit cannot mutate local presentation under its old grant
        if (!CanPublishBooleanPaint(drawingOpen: false)) {
            throw new InvalidOperationException("The menu owner changed before this local option was admitted");
        }
        if (immediateBooleanRedraw) {
            // Disable Instructions draws inside its handler and again at the monitor deadline
            _ = checked(booleanPaintRevision + 2);
            _ = checked(navigation.MenuRevision + 3);
        }
        return PrepareOptionCallback(kind);
    }

    private static string? LocalOptionCallbackKind(MenuAction action, MenuPage page) {
        // These stable source palette declarations are BOOL even when selection is a no-op
        if (page == MenuPage.ThemeColors && action.Kind is
            MenuActionKind.ChangeAccent or MenuActionKind.EnableSmoothRainbowTheme) return "boolean";
        if (page == MenuPage.Instructions && IsInstructionBooleanAction(action)) return "boolean";
        // The monitor captures slider type before the thread; it never performs the BOOL branch
        if (page == MenuPage.Preferences && action.Kind == MenuActionKind.ChangePreference
            && action.Slot is (int)MenuPreference.BooleanDisplay or (int)MenuPreference.BoolBoxLocation
                or (int)MenuPreference.ScrollAnimationTime) return "plain";
        return null;
    }

    private static bool IsInstructionBooleanAction(MenuAction action) => action.Kind == MenuActionKind.ChangePreference
        && action.Slot is (int)MenuPreference.InstructionsVisible or (int)MenuPreference.AlternateInstructions
            or (int)MenuPreference.InstructionsBackground or (int)MenuPreference.AdaptiveInstructions;
}
