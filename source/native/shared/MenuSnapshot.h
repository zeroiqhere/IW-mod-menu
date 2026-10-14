#pragma once
#include "MenuAppearance.h"
#include "Chrome/ChromeThemeState.h"
#include "Instructions/InstructionHud.h"
#include "Instructions/InstructionThemeState.h"
#include "MenuOptionCallback.h"
#include "monitor/MenuMonitorWait.h"
#include "monitor/MenuUseContinuation.h"
#include "monitor/MenuMonitorContinuation.h"
#include "paint/MenuBooleanPaint.h"
#include "WidthEditor.h"
#include "MenuUnlock.h"
#include "InstructionPositionEditor.h"
#include "MenuPositionEditor.h"
#include "MenuCredits.h"
#include "MenuPreferenceRefresh.h"
#include "MenuDesignTransition.h"
#include "MenuOpening.h"
#include "MenuOpenTransition.h"
#include "MenuPreferenceRebuild.h"
#include "OpenControls/MainOpeningControls.h"
#include "OpenControls/OpeningPolicyIdentity.h"
#include "scroll/SpacerScrollReceipt.h"
#include "QuickMenu/LocalMenuEligibility.h"
#include <optional>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace iw::overlay {
struct MenuRow {
    std::string label;
    std::string value;
    bool enabled;
    // Blank source spacers are distinct from unavailable capability rows
    bool isSpacer = false;
    std::optional<OpeningAction> openingAction = std::nullopt;
};

// Presentation data stays portable and contains no process or graphics handles
struct MenuSnapshot {
    bool isOpen = false;
    // A legacy producer without page identity retains the immediate root back path
    bool isRootMenu = true;
    std::string title;
    std::string context;
    std::string notice;
    std::string accent = "#3998FE";
    MenuAppearance appearance;
    // A dedicated editor replaces the ordinary title and option list when present
    std::optional<WidthEditor> widthEditor;
    // A missing phase keeps older producers on their ordinary menu path
    std::optional<MenuUnlockPhase> menuUnlock;
    std::vector<MenuRow> rows;
    std::size_t selectedIndex = 0;
    // Navigation changes invalidate held actions while draft updates leave their epoch intact
    std::uint64_t navigationRevision = 0;
    // Runtime-owned hints share the same saved bindings as input routing
    std::vector<std::string> instructions{
        "Aim / fire: Scroll", "Use: Select", "Melee: Go back / exit"};
    // Append the position editor so older aggregate snapshot callers retain field order
    std::optional<InstructionPositionEditor> instructionPositionEditor = std::nullopt;
    // Theme mode is separate from appearance and retains the fixed accent as its fallback
    bool smoothRainbowTheme = false;
    // Page identity is separate from cursor changes; missing legacy identity snaps presentation
    std::optional<std::uint64_t> menuRevision = std::nullopt;
    // Only the native worker stamps observed focus changes into an owned snapshot
    std::optional<std::uint64_t> presentationFocusRevision = std::nullopt;
    // A separate editor cannot inherit another modal input route
    std::optional<MenuPositionEditor> menuPositionEditor = std::nullopt;
    // Append local credits without changing the order of existing snapshot fields
    std::optional<MenuCredits> menuCredits = std::nullopt;
    // Append the local preference pause so existing snapshot aggregates retain their order
    std::optional<MenuPreferenceRefreshPhase> menuPreferenceRefresh = std::nullopt;
    // Design rebuilding is independent from editor unlock and boolean refresh timing
    std::optional<MenuDesignTransitionPhase> menuDesignTransition = std::nullopt;
    // Opening alpha is presentation data and does not change input mode or public epochs
    std::optional<MenuOpening> menuOpening = std::nullopt;
    // Append normal opening so retained root and child visits share one paused input route
    std::optional<MenuOpenTransitionPhase> menuOpenTransition = std::nullopt;
    // A committed rebuild target changes footer geometry without replacing captured body styling
    std::optional<MenuDesign> menuDesignFooterDesign = std::nullopt;
    // Append the local layout rebuild without changing earlier aggregate fields
    std::optional<MenuPreferenceRebuildPhase> menuPreferenceRebuild = std::nullopt;
    // The global opening policy remains available while the ordinary menu is closed
    std::optional<MainOpeningControls> mainOpeningControls = std::nullopt;
    // Only the native worker supplies a matched local scroll receipt
    std::optional<SpacerScrollReceipt> spacerScrollReceipt = std::nullopt;
    // The global pair is authoritative while the Main mirror preserves older readers
    std::optional<ClosedMenuOpeningPolicies> openingPolicies = std::nullopt;
    // Absent legacy admission data cannot authorize a new Quick opening path
    std::optional<LocalMenuEligibility> localMenuEligibility = std::nullopt;
    // Append family identity without changing older aggregate snapshot fields
    MenuSurface menuSurface = MenuSurface::Main;
    // Append callback ownership without changing earlier aggregate fields
    std::optional<MenuOptionCallback> menuOptionCallback = std::nullopt;
    // Cached checkbox paint is separate from every current feature value
    std::optional<MenuBooleanPaint> menuBooleanPaint = std::nullopt;
    // Source chrome creation is independent from row and navigation epochs
    std::optional<std::uint64_t> menuChromeRevision = std::nullopt;
    // Independent instruction ownership can survive an ordinary menu close
    std::optional<InstructionHud> instructionHud = std::nullopt;
    std::optional<InstructionThemeState> instructionTheme = std::nullopt;
    // Source chrome history is independent from current row values and graphics resources
    std::optional<ChromeThemeState> chromeTheme = std::nullopt;
    // Mechanical waits retain the ordinary monitor without changing feature or modal ownership
    std::optional<MenuMonitorWait> menuMonitorWait = std::nullopt;
    // A causal invocation receipt retains private action identity behind the backend boundary
    std::optional<MenuUseContinuation> menuUseContinuation = std::nullopt;
    // Normal mechanical expiry remains distinct from cancellation and opening permission
    std::optional<MenuMonitorContinuation> menuMonitorContinuation = std::nullopt;
};
}
