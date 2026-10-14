using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Themes;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private LocalChromeThemeJournal chromeTheme = new();
    internal ulong ChromeThemeCounter => chromeTheme.Counter;

    private (LocalChromeThemeJournal State, ChromeThemeState? Frame) PrepareChromeTheme(
        LocalMenuChromeState chrome, LocalMenuAdmissionSnapshot? proof, bool preview) {
        if (preview || !session.RequiresLocalMenuAdmission && proof is null) return (chromeTheme.Retire(), null);
        // A reserved creation represents actual destroyed chrome until its successful replacement draw
        bool owns = proof is not null && proof.Observation.HasMenu == true
            && ReferenceEquals(chrome.Owner, proof.OwnerToken) && chrome.Revision > 0;
        if (!navigation.IsOpen || !owns || chrome.ReservedRevision is not null || finished || ExitRequested || cleanupRecovery)
            return (chromeTheme.Retire(), ChromeThemeState.Absent);
        var projected = chromeTheme.Project(proof!.OwnerToken, chrome.Revision, proof.Revision, appearance.Design,
            accent, smoothRainbowTheme, navigation.Page == MenuPage.WidthEditor, clock);
        // Unknown life withdraws wire authority but still captures an actual new source creation
        return proof.Observation.Life == LocalPlayerLife.Unknown ? (projected.State, null) : projected;
    }

    private LocalChromeThemeJournal PrepareChromeThemeInvocation(StateDocument frame, string targetAccent,
        LocalMenuOptionCallbackState? callback) {
        var proof = ReadInstructionAdmission();
        var header = frame.ChromeTheme;
        if (proof is null || header is not { Exists: true } || header.AdmissionRevision != proof.Revision
            || !ReferenceEquals(chromeTheme.Owner, proof.OwnerToken) || header.CreationDesign != appearance.Design)
            return chromeTheme;
        // Existing source objects supply planner membership; opaque keys are local instance-role names
        var roles = new[] { ThemeWidgetRole.Scroller, ThemeWidgetRole.Banner, ThemeWidgetRole.Title,
            ThemeWidgetRole.Separator, ThemeWidgetRole.BottomLine };
        var widgets = roles.Where(role => (header.CurrentRoleMask & ChromeThemeRoles.Bit(role)) != 0)
            .Select(role => new ObservedThemeWidget(new ThemeWidgetIdentity(
                header.CreationRevision!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + role,
                role, ThemeWidgetLifetime.MenuChrome)));
        // The current native menu has no source keyboard widget, so no kbString membership is invented
        var palette = new ThemeRgb(Convert.ToByte(targetAccent.Substring(1, 2), 16) / 255.0,
            Convert.ToByte(targetAccent.Substring(3, 2), 16) / 255.0, Convert.ToByte(targetAccent.Substring(5, 2), 16) / 255.0);
        var plan = ThemeCallbackPlan.Fixed(new(appearance.Design, keyboardStringExists: false), palette, widgets);
        int mask = plan.Commands.Aggregate(0, (current, command) => current | ChromeThemeRoles.Bit(command.Widget.Role));
        long timestamp = callback?.StartedTimestamp ?? clock.GetTimestamp();
        return chromeTheme.Append(proof.OwnerToken, header, mask, targetAccent, smoothRainbowTheme, timestamp, clock);
    }
}
