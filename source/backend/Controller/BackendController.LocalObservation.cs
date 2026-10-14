using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // Permission stays with the session's authenticated actor and connection owner
    // Only this menu owner supplies its normal monitor lock, never a guessed retail field
    public LocalPlayerObservation ReadMenuObservation() {
        var observation = session.ReadLocalObservation();
        if (!observation.IsSupported() || observation.ProcessId != processId
            && !(observation.ProcessId == 0 && observation.Identity is null)) {
            return LocalPlayerObservation.BindingUnavailable;
        }
        // Preserve the source's bounded failure status without turning Unknown into a positive fact
        if (observation.Identity is null) return observation;
        return observation with { ControlsDisabled = MenuMonitorControlsDisabled };
    }

    // Dedicated editor input keeps its existing route while the ordinary monitor is disabled
    // Pending game work has a separate Busy guard and does not invent a source permission field
    private bool MenuMonitorControlsDisabled => MenuControlsDisabled(menuUnlock);
    private bool MenuControlsDisabled(LocalMenuUnlockState unlock) => finished || ExitRequested || quickInputsLocked
        || optionCallback is not null || unlock.StartedTimestamp is not null || openTransition is not null
        || designTransition is not null || preferenceRefresh is not null || preferenceRebuild is not null
        || widthEditor is not null || menuPositionEditor is not null
        || instructionPositionEditor is not null || menuCredits is not null;

    private QuickMenuViewContext ReadQuickMenuViewContext() {
        var observation = ReadMenuObservation();
        QuickPlayerLife life = observation.Life switch {
            LocalPlayerLife.Alive => QuickPlayerLife.Living,
            LocalPlayerLife.Dead => QuickPlayerLife.Dead,
            _ => QuickPlayerLife.Unknown
        };
        // The named session predicate proves only its positive role, not full GSC absence
        // Neither a false role nor a borrowed BO3 account list can invent Developer authority
        bool? host = observation.SessionHostRole == true ? true : null;
        return new(observation, new(life, host, null), observation.Identity, processId,
            admittedMenuOwner is not null && HasVerifiedOpeningOwner);
    }
}
