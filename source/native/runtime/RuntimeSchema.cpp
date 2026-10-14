#include "chrome/ChromeThemeSchema.h"
#include "instructions/InstructionThemeSchema.h"
#include "instructions/InstructionHudSchema.h"
#include "MenuChromeSchema.h"
#include "MenuBooleanPaintSchema.h"
#include "MenuOptionCallbackSchema.h"
#include "monitor/MenuMonitorWaitSchema.h"
#include "monitor/MenuUseContinuationSchema.h"
#include "monitor/MenuMonitorContinuationSchema.h"
#include "RuntimeSchema.h"
#include "OpeningControlsSchema.h"
#include "LocalMenuEligibilitySchema.h"
#include "BoundedSessionJson.h"
#include "../shared/MenuRowCapacity.h"

#include <algorithm>
#include <array>
#include <limits>
#include <stdexcept>
#include <string_view>
#include <nlohmann/json.hpp>

namespace iw::overlay::runtime {
namespace {
using Json = nlohmann::json;

Json Parse(const std::string& bytes) {
    Json value = ReadBoundedSessionJson(bytes, MaximumFileBytes);
    if (!value.is_object()) throw std::runtime_error("Session JSON must be an object");
    const auto& version = value.at("version");
    if (!version.is_number_integer() || version != ProtocolVersion) {
        throw std::runtime_error("Unsupported session protocol version");
    }
    return value;
}

unsigned long long Unsigned(const Json& value) {
    if (!value.is_number_unsigned()) throw std::runtime_error("An unsigned integer is required");
    return value.get<unsigned long long>();
}

std::string Text(const Json& value, std::size_t maximumBytes = 1500) {
    if (!value.is_string()) throw std::runtime_error("Menu text must be a string");
    auto text = value.get<std::string>();
    if (text.size() > maximumBytes || text.find('\0') != std::string::npos) {
        throw std::runtime_error("Menu text exceeds its allowed span");
    }
    return text;
}

MenuDesign Design(const Json& value) {
    // Both body appearance and the independent footer target share exact source names
    const auto design = Text(value, 10);
    if (design == "Apparition") return MenuDesign::Apparition;
    if (design == "Classic") return MenuDesign::Classic;
    if (design == "Native") return MenuDesign::Native;
    if (design == "AIO") return MenuDesign::Aio;
    if (design == "Basic") return MenuDesign::Basic;
    throw std::runtime_error("Unsupported menu design");
}

bool Boolean(const Json& value) {
    if (!value.is_boolean()) throw std::runtime_error("A boolean is required");
    return value.get<bool>();
}

std::int32_t SignedCoordinate(const Json& value) {
    if (!value.is_number_integer()) throw std::runtime_error("A signed32 coordinate is required");
    // Positive JSON integers may be unsigned and must be bounded before narrowing
    if (value.is_number_unsigned()) {
        const auto number = value.get<std::uint64_t>();
        if (number > static_cast<std::uint64_t>(std::numeric_limits<std::int32_t>::max())) {
            throw std::runtime_error("Position coordinate exceeds signed32 bounds");
        }
        return static_cast<std::int32_t>(number);
    }
    const auto number = value.get<std::int64_t>();
    if (number < std::numeric_limits<std::int32_t>::min() || number > std::numeric_limits<std::int32_t>::max()) {
        throw std::runtime_error("Position coordinate exceeds signed32 bounds");
    }
    return static_cast<std::int32_t>(number);
}

MenuAppearance Appearance(const Json& value) {
    if (!value.is_object()) throw std::runtime_error("Menu appearance must be an object");
    MenuAppearance appearance;
    // Only source-supported display choices can change the panel geometry
    appearance.booleanDisplay = Text(value.at("booleanDisplay"), 10);
    if (appearance.booleanDisplay != "Boxes" && appearance.booleanDisplay != "Text"
        && appearance.booleanDisplay != "Text Color") {
        throw std::runtime_error("Unsupported boolean display");
    }
    appearance.boolBoxLocation = Text(value.at("boolBoxLocation"), 5);
    if (appearance.boolBoxLocation != "Right" && appearance.boolBoxLocation != "Left") {
        throw std::runtime_error("Unsupported boolean box location");
    }
    // Strict boolean reads prevent strings and numbers from silently changing settings
    appearance.instructionsVisible = Boolean(value.at("instructionsVisible"));
    appearance.optionCounter = Boolean(value.at("optionCounter"));
    appearance.coloredCursor = Boolean(value.at("coloredCursor"));
    appearance.spotlightCursor = Boolean(value.at("spotlightCursor"));
    appearance.largeCursor = Boolean(value.at("largeCursor"));
    // The eighth setting is optional so explicit older seven-field objects remain valid
    if (value.contains("quickExit")) { appearance.quickExit = Boolean(value.at("quickExit")); }
    if (value.contains("menuWidth")) {
        // Source bounds are checked before narrowing the integer into portable geometry
        const auto width = Unsigned(value.at("menuWidth"));
        if (width < 200 || width > 500) throw std::runtime_error("Unsupported menu width");
        appearance.menuWidth = static_cast<int>(width);
    }
    if (value.contains("scrollAnimationMilliseconds")) {
        // Only the source slider's exact ten-millisecond steps can reach input and rendering
        const auto duration = Unsigned(value.at("scrollAnimationMilliseconds"));
        if (duration < 100 || duration > 250 || duration % 10 != 0) {
            throw std::runtime_error("Unsupported scroll animation duration");
        }
        appearance.scrollAnimationMilliseconds = static_cast<int>(duration);
    }
    // Older packets omit these choices and retain independent instruction defaults
    if (value.contains("alternateInstructions")) {
        appearance.alternateInstructions = Boolean(value.at("alternateInstructions"));
    }
    if (value.contains("instructionsBackground")) {
        appearance.instructionsBackground = Boolean(value.at("instructionsBackground"));
    }
    if (value.contains("adaptiveInstructions")) {
        appearance.adaptiveInstructions = Boolean(value.at("adaptiveInstructions"));
    }
    // Source handlers clear adaptive placement when the alternate style is enabled
    if (appearance.alternateInstructions && appearance.adaptiveInstructions) {
        throw std::runtime_error("Alternate and adaptive instructions cannot be combined");
    }
    const auto position = value.find("instructionPosition");
    if (position != value.end() && !position->is_null()) {
        // An exact coordinate object cannot smuggle an extra field or partial saved position
        if (!position->is_object() || position->size() != 2 || !position->contains("x") || !position->contains("y")) {
            throw std::runtime_error("Instruction position requires exactly x and y");
        }
        appearance.instructionPosition = InstructionPosition{SignedCoordinate(position->at("x")), SignedCoordinate(position->at("y"))};
    }
    const auto menuPosition = value.find("menuPosition");
    if (menuPosition != value.end() && !menuPosition->is_null()) {
        // Panel coordinates share the exact signed bounds of saved instruction coordinates
        if (!menuPosition->is_object() || menuPosition->size() != 2
            || !menuPosition->contains("x") || !menuPosition->contains("y")) {
            throw std::runtime_error("Menu position requires exactly x and y");
        }
        appearance.menuPosition = MenuPosition{SignedCoordinate(menuPosition->at("x")), SignedCoordinate(menuPosition->at("y"))};
    }
    if (value.contains("design")) {
        // Exact source names map to a bounded enum instead of unchecked renderer text
        appearance.design = Design(value.at("design"));
    }
    if (value.contains("stealthUI")) appearance.stealthUI = Boolean(value.at("stealthUI"));
    if (appearance.stealthUI && (appearance.booleanDisplay == "Boxes" || appearance.optionCounter)) {
        throw std::runtime_error("Stealth requires text display without an option counter");
    }
    // Producers must publish normalized flags atomically rather than asking readers to change them
    if ((appearance.design == MenuDesign::Classic || appearance.design == MenuDesign::Native) && appearance.coloredCursor) {
        throw std::runtime_error("This menu design cannot use a colored cursor");
    }
    if ((appearance.design == MenuDesign::Aio || appearance.design == MenuDesign::Basic) && appearance.optionCounter) {
        throw std::runtime_error("This menu design cannot use an option counter");
    }
    return appearance;
}
}

ParsedState ParseState(const std::string& bytes, DWORD expectedProcessId,
    const std::vector<std::string>* controlHints, const std::vector<std::string>* quickExitHints,
    const std::vector<std::string>* rootQuickExitHints, const std::vector<std::string>* widthEditorHints,
    const std::vector<std::string>* positionHints,
    std::optional<std::uint64_t> presentationFocusRevision,
    const std::vector<std::string>* menuPositionHints, const std::vector<std::string>* creditsHints,
    const OpeningActionLabels* openingActionLabels,
    const scroll::SpacerScrollObservation* spacerObservation) {
    const Json value = Parse(bytes);
    if (expectedProcessId == 0 || Unsigned(value.at("processId")) != expectedProcessId) {
        throw std::runtime_error("The menu state belongs to another process");
    }
    auto snapshot = std::make_shared<MenuSnapshot>();
    if (value.contains("menuChromeRevision")) snapshot->menuChromeRevision = ParseMenuChromeRevision(value.at("menuChromeRevision"));
    // Native focus identity is copied before publication and is never read from JSON
    snapshot->presentationFocusRevision = presentationFocusRevision;
    // Eligibility is owned independently of open state or configured policy availability
    if (value.contains("localMenuEligibility")) snapshot->localMenuEligibility = ParseLocalMenuEligibility(value.at("localMenuEligibility"));
    if (value.contains("menuSurface")) snapshot->menuSurface = ParseMenuSurface(value.at("menuSurface"));
    if (value.contains("menuRevision")) snapshot->menuRevision = Unsigned(value.at("menuRevision"));
    if (value.contains("mainOpeningControls")) snapshot->mainOpeningControls = ParseMainOpeningControls(value.at("mainOpeningControls"), openingActionLabels);
    if (value.contains("openingPolicies")) {
        snapshot->openingPolicies = ParseOpeningPolicies(value.at("openingPolicies"), openingActionLabels);
    }
    if (!IsValidOpeningPolicyOwnership(snapshot->mainOpeningControls, snapshot->openingPolicies)) {
        throw std::runtime_error("Dual opening policies require their exact Main mirror");
    }
    snapshot->isOpen = Boolean(value.at("isOpen"));
    // Missing identity uses the legacy root path; titles never determine a page
    if (value.contains("isRootMenu")) { snapshot->isRootMenu = Boolean(value.at("isRootMenu")); }
    // Older producers omit preferences or send null and retain the pinned defaults
    const auto appearance = value.find("appearance");
    if (appearance != value.end() && !appearance->is_null()) {
        snapshot->appearance = Appearance(*appearance);
    }
    // Matched private hints follow parsed preferences and explicit page identity
    const auto* selectedHints = controlHints;
    if (snapshot->appearance.quickExit) {
        const auto* variant = snapshot->isRootMenu ? rootQuickExitHints : quickExitHints;
        if (variant) { selectedHints = variant; }
    }
    // Missing variants retain normal help rather than describing a different page behavior
    if (selectedHints) { snapshot->instructions = *selectedHints; }
    snapshot->title = Text(value.at("title"));
    snapshot->context = Text(value.at("context"));
    // Managed notices can contain 1500 Unicode characters within a 6000-byte bound
    snapshot->notice = Text(value.at("notice"), 6000);
    // Accent is a bounded RGB value rather than arbitrary renderer instructions
    snapshot->accent = Text(value.at("accent"));
    if (snapshot->accent.size() != 7 || snapshot->accent[0] != '#') {
        throw std::runtime_error("Invalid menu accent");
    }
    for (std::size_t index = 1; index < snapshot->accent.size(); ++index) {
        char character = snapshot->accent[index];
        if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')
            || (character >= 'A' && character <= 'F'))) throw std::runtime_error("Invalid menu accent");
    }
    // Missing mode keeps legacy fixed colors; supplied values stay strict even while closed
    if (value.contains("smoothRainbowTheme")) {
        snapshot->smoothRainbowTheme = Boolean(value.at("smoothRainbowTheme"));
    }
    const auto& rows = value.at("rows");
    if (!rows.is_array() || rows.size() > 128) throw std::runtime_error("Invalid menu row count");
    for (const auto& row : rows) {
        if (!row.is_object()) throw std::runtime_error("A menu row must be an object");
        MenuRow parsed{ Text(row.at("label")), Text(row.at("value")), Boolean(row.at("enabled")) };
        ParseOpeningRowMetadata(row, parsed, openingActionLabels);
        snapshot->rows.push_back(std::move(parsed));
    }
    auto index = Unsigned(value.at("selectedIndex"));
    if ((snapshot->rows.empty() && index != 0)
        || (!snapshot->rows.empty() && index >= snapshot->rows.size())) {
        throw std::runtime_error("The selected menu row is outside the snapshot");
    }
    snapshot->selectedIndex = static_cast<std::size_t>(index);
    if (!snapshot->rows.empty() && snapshot->rows[snapshot->selectedIndex].isSpacer)
        throw std::runtime_error("A final selected row cannot be a spacer");
    const auto editor = value.find("widthEditor");
    if (editor != value.end() && !editor->is_null()) {
        // A dedicated editor cannot share normal rows or describe a hidden root menu
        if (!snapshot->isOpen || snapshot->isRootMenu || !snapshot->rows.empty()
            || !editor->is_object() || editor->size() != 1 || !editor->contains("step")) {
            throw std::runtime_error("Invalid width editor context");
        }
        const auto step = Unsigned(editor->at("step"));
        if (step < 1 || step > 10) throw std::runtime_error("Unsupported width editor offset");
        WidthEditor owned;
        owned.step = static_cast<int>(step);
        // Private saved hints remain independent from ordinary footer instructions
        if (widthEditorHints) {
            owned.instructions = *widthEditorHints;
            if (widthEditorHints->size() == 5) owned.footerInstructions[2] = widthEditorHints->at(4);
        }
        snapshot->widthEditor = std::move(owned);
    }
    const auto unlock = value.find("menuUnlock");
    if (unlock != value.end() && !unlock->is_null()) {
        // The parent stays logically open while its panel and controls return in stages
        if (!snapshot->isOpen || snapshot->isRootMenu || snapshot->widthEditor) {
            throw std::runtime_error("Invalid menu unlock context");
        }
        const auto phase = Text(*unlock, 16);
        if (phase == "rebuild") snapshot->menuUnlock = MenuUnlockPhase::Rebuild;
        else if (phase == "pause") snapshot->menuUnlock = MenuUnlockPhase::Pause;
        else throw std::runtime_error("Unsupported menu unlock phase");
    }
    // Actual controller navigation, rather than a draft's display text, ends held adjustments
    if (value.contains("navigationRevision")) {
        snapshot->navigationRevision = Unsigned(value.at("navigationRevision"));
    }
    const auto positionEditor = value.find("instructionPositionEditor");
    if (positionEditor != value.end() && !positionEditor->is_null()) {
        if (!snapshot->isOpen || snapshot->isRootMenu || !snapshot->rows.empty() || snapshot->widthEditor
            || snapshot->menuUnlock || !snapshot->appearance.instructionsVisible || !positionEditor->is_object()
            || positionEditor->size() != 1 || !positionEditor->contains("phase")) {
            throw std::runtime_error("Invalid instruction position editor context");
        }
        InstructionPositionEditor owned;
        const auto phase = Text(positionEditor->at("phase"), 8);
        if (phase == "entering") owned.phase = InstructionPositionEditorPhase::Entering;
        else if (phase == "editing") owned.phase = InstructionPositionEditorPhase::Editing;
        else if (phase == "exiting") owned.phase = InstructionPositionEditorPhase::Exiting;
        else throw std::runtime_error("Unsupported instruction position editor phase");
        // Editor hints are a copied five-action legend, separate from normal footer controls
        if (positionHints) {
            if (positionHints->size() != 5) throw std::runtime_error("Position editor needs five private control hints");
            owned.instructions = *positionHints;
        }
        snapshot->instructionPositionEditor = std::move(owned);
    }
    const auto menuPositionEditor = value.find("menuPositionEditor");
    if (menuPositionEditor != value.end() && !menuPositionEditor->is_null()) {
        // Immediate entry is exclusive with every other editor and parent unlock phase
        if (!snapshot->isOpen || snapshot->isRootMenu || !snapshot->rows.empty() || snapshot->widthEditor
            || snapshot->instructionPositionEditor || snapshot->menuUnlock || !menuPositionEditor->is_object()
            || menuPositionEditor->size() != 1 || !menuPositionEditor->contains("step")) {
            throw std::runtime_error("Invalid menu position editor context");
        }
        const auto step = Unsigned(menuPositionEditor->at("step"));
        if (step < 1 || step > 10) throw std::runtime_error("Unsupported menu position offset");
        MenuPositionEditor owned;
        owned.step = static_cast<int>(step);
        if (menuPositionHints) {
            // Only the private formatter supplies body and contextual exit help
            if (menuPositionHints->size() != 7) throw std::runtime_error("Menu position needs seven private hints");
            owned.instructions = *menuPositionHints;
            owned.footerInstructions[2] = menuPositionHints->at(6);
        }
        snapshot->menuPositionEditor = std::move(owned);
    }
    const auto credits = value.find("menuCredits");
    if (credits != value.end() && !credits->is_null()) {
        // A timed credits visit is separate from normal options and every editor transition
        if (!snapshot->isOpen || snapshot->isRootMenu || !snapshot->rows.empty() || snapshot->widthEditor
            || snapshot->instructionPositionEditor || snapshot->menuPositionEditor || snapshot->menuUnlock
            || !credits->is_object() || credits->size() != 1 || !credits->contains("elapsedMilliseconds")) {
            throw std::runtime_error("Invalid menu credits context");
        }
        const auto elapsed = Unsigned(credits->at("elapsedMilliseconds"));
        if (elapsed >= 40000) throw std::runtime_error("Unsupported menu credits progress");
        MenuCredits owned;
        owned.elapsedMilliseconds = static_cast<int>(elapsed);
        if (creditsHints) {
            // A single private saved-melee line replaces the footer only during this visit
            if (creditsHints->size() != 1) throw std::runtime_error("Credits need one private exit hint");
            owned.instructions = *creditsHints;
        }
        snapshot->menuCredits = std::move(owned);
    }
    const auto refresh = value.find("menuPreferenceRefresh");
    if (refresh != value.end() && !refresh->is_null()) {
        // A local preference refresh keeps ordinary child rows and excludes every modal view
        if (!snapshot->isOpen || snapshot->isRootMenu || snapshot->rows.empty() || snapshot->widthEditor
            || snapshot->instructionPositionEditor || snapshot->menuPositionEditor || snapshot->menuCredits
            || snapshot->menuUnlock || !refresh->is_object() || refresh->size() != 1 || !refresh->contains("phase")) {
            throw std::runtime_error("Invalid menu preference refresh context");
        }
        const auto phase = Text(refresh->at("phase"), 8);
        if (phase == "pending") snapshot->menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        else if (phase == "cooldown") snapshot->menuPreferenceRefresh = MenuPreferenceRefreshPhase::Cooldown;
        else if (phase == "selector" && IsOpeningSelectorRefreshContext(*snapshot))
            snapshot->menuPreferenceRefresh = MenuPreferenceRefreshPhase::Selector;
        else throw std::runtime_error("Unsupported menu preference refresh phase");
    }
    const auto designTransition = value.find("menuDesignTransition");
    if (designTransition != value.end() && !designTransition->is_null()) {
        // A design transition retains ordinary child rows and cannot share another modal lifetime
        if (!snapshot->isOpen || snapshot->isRootMenu || snapshot->rows.empty() || snapshot->widthEditor
            || snapshot->instructionPositionEditor || snapshot->menuPositionEditor || snapshot->menuCredits
            || snapshot->menuUnlock || snapshot->menuPreferenceRefresh || !designTransition->is_object()
            || designTransition->size() != (designTransition->contains("footerDesign") ? 2u : 1u)
            || !designTransition->contains("phase")) {
            throw std::runtime_error("Invalid menu design transition context");
        }
        const auto phase = Text(designTransition->at("phase"), 8);
        if (phase == "rebuild") snapshot->menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
        else if (phase == "cooldown") snapshot->menuDesignTransition = MenuDesignTransitionPhase::Cooldown;
        else throw std::runtime_error("Unsupported menu design transition phase");
        const auto footer = designTransition->find("footerDesign");
        if (footer != designTransition->end() && !footer->is_null()) {
            // The target describes only the absent-HUD rebuild gap, never its visible cooldown
            if (snapshot->menuDesignTransition != MenuDesignTransitionPhase::Rebuild) {
                throw std::runtime_error("Footer design requires a menu design rebuild");
            }
            const auto target = Design(*footer);
            // Committed target flags remain normalized even while body styling is captured
            if ((target == MenuDesign::Classic || target == MenuDesign::Native) && snapshot->appearance.coloredCursor) {
                throw std::runtime_error("This footer design cannot use a colored cursor");
            }
            if ((target == MenuDesign::Aio || target == MenuDesign::Basic) && snapshot->appearance.optionCounter) {
                throw std::runtime_error("This footer design cannot use an option counter");
            }
            snapshot->menuDesignFooterDesign = target;
        }
    }
    const auto preferenceRebuild = value.find("menuPreferenceRebuild");
    if (preferenceRebuild != value.end()) {
        // One populated child owns the local layout pause without borrowing another modal lifetime
        if (!snapshot->isOpen || snapshot->isRootMenu || snapshot->rows.empty()
            || snapshot->widthEditor || snapshot->instructionPositionEditor || snapshot->menuPositionEditor
            || snapshot->menuCredits || snapshot->menuUnlock || snapshot->menuPreferenceRefresh
            || snapshot->menuDesignTransition || !preferenceRebuild->is_object()
            || preferenceRebuild->size() != 1 || !preferenceRebuild->contains("phase")) {
            throw std::runtime_error("Invalid preference rebuild context");
        }
        const auto phase = Text(preferenceRebuild->at("phase"), 8);
        if (phase == "rebuild") snapshot->menuPreferenceRebuild = MenuPreferenceRebuildPhase::Rebuild;
        else if (phase == "cooldown") snapshot->menuPreferenceRebuild = MenuPreferenceRebuildPhase::Cooldown;
        else throw std::runtime_error("Unsupported preference rebuild phase");
    }
    const auto openTransition = value.find("menuOpenTransition");
    if (openTransition != value.end() && !openTransition->is_null()) {
        // Normal opening retains root or child rows, including empty history and one-row pages
        if (!snapshot->isOpen || snapshot->widthEditor || snapshot->instructionPositionEditor
            || snapshot->menuPositionEditor || snapshot->menuCredits || snapshot->menuUnlock
            || snapshot->menuPreferenceRefresh || snapshot->menuDesignTransition || snapshot->menuPreferenceRebuild || !openTransition->is_object()
            || openTransition->size() != 1 || !openTransition->contains("phase")) {
            throw std::runtime_error("Invalid normal menu open context");
        }
        const auto phase = Text(openTransition->at("phase"), 8);
        if (phase == "rebuild") snapshot->menuOpenTransition = MenuOpenTransitionPhase::Rebuild;
        else if (phase == "cooldown") snapshot->menuOpenTransition = MenuOpenTransitionPhase::Cooldown;
        else throw std::runtime_error("Unsupported normal menu open phase");
    }
    const auto callback = value.find("menuOptionCallback");
    if (callback != value.end()) {
        // Explicit null is malformed; omission is the legacy ordinary-menu path
        snapshot->menuOptionCallback = ParseMenuOptionCallback(*callback, *snapshot);
    }
    const auto opening = value.find("menuOpening");
    if (opening != value.end() && !opening->is_null()) {
        // Ordinary root and child rows may fade; modal views own separate presentation lifetimes
        if (!snapshot->isOpen || (snapshot->menuOptionCallback && !CallbackKeepsOpening(*snapshot->menuOptionCallback)) || snapshot->widthEditor || snapshot->instructionPositionEditor
            || snapshot->menuPositionEditor || snapshot->menuCredits || snapshot->menuUnlock
            || snapshot->menuPreferenceRefresh == MenuPreferenceRefreshPhase::Cooldown || snapshot->menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild
            || snapshot->menuOpenTransition == MenuOpenTransitionPhase::Rebuild
            || (snapshot->menuDesignTransition
                && *snapshot->menuDesignTransition != MenuDesignTransitionPhase::Cooldown)
            || !opening->is_object() || opening->size() != 2
            || !opening->contains("revision") || !opening->contains("elapsedMilliseconds")) {
            throw std::runtime_error("Invalid menu opening context");
        }
        const auto revision = Unsigned(opening->at("revision"));
        const auto elapsed = Unsigned(opening->at("elapsedMilliseconds"));
        const auto duration = MenuOpeningDurationMilliseconds(snapshot->appearance, snapshot->rows.size());
        if (revision == 0 || duration == 0 || elapsed >= static_cast<std::uint64_t>(duration)) {
            throw std::runtime_error("Unsupported menu opening progress");
        }
        // Bounds are checked before narrowing; neither event field can modify cursor or page identity
        snapshot->menuOpening = MenuOpening{revision, static_cast<int>(elapsed)};
    }
    const auto paint = value.find("menuBooleanPaint");
    if (paint != value.end()) snapshot->menuBooleanPaint = ParseMenuBooleanPaint(*paint, *snapshot);
    if (value.contains("instructionHud")) snapshot->instructionHud = ParseInstructionHud(value.at("instructionHud"), *snapshot, openingActionLabels);
    if (value.contains("instructionTheme")) snapshot->instructionTheme = ParseInstructionTheme(value.at("instructionTheme"), *snapshot);
    if (value.contains("chromeTheme")) snapshot->chromeTheme = ParseChromeTheme(value.at("chromeTheme"), *snapshot);
    if (value.contains("menuMonitorWait") && !value.at("menuMonitorWait").is_null()) {
        snapshot->menuMonitorWait = ParseMenuMonitorWait(value.at("menuMonitorWait"), *snapshot);
    }
    // Validate the full packet before the private tracker can accept its acknowledgment
    const auto lastSequence = Unsigned(value.at("lastSequence"));
    if (value.contains("menuUseContinuation") && !value.at("menuUseContinuation").is_null()) {
        snapshot->menuUseContinuation = ParseMenuUseContinuation(value.at("menuUseContinuation"), *snapshot, lastSequence);
    }
    if (value.contains("menuMonitorContinuation") && !value.at("menuMonitorContinuation").is_null()) {
        snapshot->menuMonitorContinuation = ParseMenuMonitorContinuation(value.at("menuMonitorContinuation"), *snapshot, lastSequence);
    }
    // Design callbacks and Use receipts form one exact pair before private state advances
    ValidateMenuDesignUseContinuation(*snapshot);
    const auto generation = Unsigned(value.at("generation"));
    if (spacerObservation && spacerObservation->tracker) {
        snapshot->spacerScrollReceipt = spacerObservation->tracker->Observe(
            *snapshot, lastSequence, spacerObservation->observedMilliseconds);
    }
    // The private receipt is copied while owned data is mutable, before const publication
    return { snapshot, lastSequence, generation };
}

InputMode InputModeForSnapshot(const MenuSnapshot& snapshot) {
    // Normal opening and local rebuild phases share the existing paused input route
    if (snapshot.menuOptionCallback || snapshot.menuUnlock || snapshot.menuPreferenceRefresh || snapshot.menuDesignTransition
        || snapshot.menuOpenTransition || snapshot.menuPreferenceRebuild) return InputMode::MenuLocked;
    if (snapshot.widthEditor) return InputMode::WidthEditor;
    if (snapshot.instructionPositionEditor) {
        // Both transition phases consume keys; only editing routes position commands
        return snapshot.instructionPositionEditor->phase == InstructionPositionEditorPhase::Editing
            ? InputMode::InstructionPositionEditor : InputMode::MenuLocked;
    }
    if (snapshot.menuPositionEditor) return InputMode::MenuPositionEditor;
    if (snapshot.menuCredits) return InputMode::MenuCredits;
    return InputMode::Menu;
}

Bindings ParseBindings(const std::string& bytes) {
    const Json value = Parse(bytes);
    constexpr std::array<std::string_view, 16> fields{
        "version", "aim", "melee", "up", "down", "select", "previousChoice", "nextChoice", "attack", "moveUp", "moveDown", "frag", "secondaryOffhand", "sprint", "stance", "jump"};
    // Binding aliases must not imply capabilities that the saved packet did not confirm
    for (auto item = value.begin(); item != value.end(); ++item) {
        if (std::find(fields.begin(), fields.end(), item.key()) == fields.end()) {
            throw std::runtime_error("Unknown binding property");
        }
    }
    Bindings bindings;
    auto load = [&](const char* name, std::vector<int>& keys) {
        const auto& array = value.at(name);
        if (!array.is_array() || array.size() > 16) throw std::runtime_error("Invalid binding array");
        for (const auto& key : array) {
            auto number = Unsigned(key);
            if (number > 254) throw std::runtime_error("A binding key is outside its supported range");
            keys.push_back(static_cast<int>(number));
        }
    };
    load("aim", bindings.aim);
    load("melee", bindings.melee);
    load("up", bindings.up);
    load("down", bindings.down);
    load("select", bindings.select);
    // Legacy packets omit these groups; no default key can represent a missing game action
    if (value.contains("previousChoice")) load("previousChoice", bindings.previousChoice);
    if (value.contains("nextChoice")) load("nextChoice", bindings.nextChoice);
    if (value.contains("attack")) load("attack", bindings.attack);
    if (value.contains("moveUp")) load("moveUp", bindings.moveUp);
    if (value.contains("moveDown")) load("moveDown", bindings.moveDown);
    if (value.contains("frag")) load("frag", bindings.frag);
    if (value.contains("secondaryOffhand")) load("secondaryOffhand", bindings.secondaryOffhand);
    if (value.contains("sprint")) load("sprint", bindings.sprint);
    if (value.contains("stance")) load("stance", bindings.stance);
    if (value.contains("jump")) load("jump", bindings.jump);
    ValidateBindings(bindings);
    return bindings;
}

std::string CommandJson(DWORD processId, unsigned long long sequence, Input input,
    std::optional<std::uint64_t> navigationRevision, std::optional<std::uint64_t> openingControlsRevision,
    std::optional<std::uint64_t> localAdmissionRevision, std::optional<std::uint64_t> menuRevision) {
    // JSON escaping and integer formatting come from the same parser library
    Json command({ {"version", ProtocolVersion}, {"processId", processId},
        {"sequence", sequence}, {"input", InputName(input)} });
    // Scoped intents carry the exact observed owner epochs, including navigation zero
    const bool scopedOpen = input == Input::OpenMain || input == Input::OpenQuick;
    const bool jumpBack = input == Input::JumpBack;
    // Actorless Toggle keeps legacy defaults but carries every supplied opening context
    if (input == Input::Toggle && navigationRevision) command["navigationRevision"] = *navigationRevision;
    if (localAdmissionRevision && !*localAdmissionRevision) {
        throw std::runtime_error("Local admission revision must be positive");
    }
    if (scopedOpen || jumpBack) {
        if (!navigationRevision || !localAdmissionRevision) {
            throw std::runtime_error("Scoped menu intent requires captured navigation and local admission");
        }
        command["navigationRevision"] = *navigationRevision;
        command["localAdmissionRevision"] = *localAdmissionRevision;
        if (scopedOpen) {
            if (!openingControlsRevision || !*openingControlsRevision) {
                throw std::runtime_error("Scoped opening requires its captured policy revision");
            }
            command["openingControlsRevision"] = *openingControlsRevision;
        }
    }
    if (input == Input::Close || input == Input::IncreaseWidth || input == Input::DecreaseWidth
        || input == Input::IncreaseWidthStep || input == Input::DecreaseWidthStep || input == Input::FinishWidthEditor
        || input == Input::MoveInstructionsUp || input == Input::MoveInstructionsDown
        || input == Input::MoveInstructionsLeft || input == Input::MoveInstructionsRight
        || input == Input::FinishInstructionPositionEditor || input == Input::MoveMenuUp || input == Input::MoveMenuDown
        || input == Input::MoveMenuLeft || input == Input::MoveMenuRight || input == Input::IncreaseMenuPositionStep
        || input == Input::DecreaseMenuPositionStep || input == Input::FinishMenuPositionEditor
        || input == Input::FinishMenuCredits) {
        // A held close can only apply to the exact navigation snapshot that produced it
        if (!navigationRevision) { throw std::runtime_error("Context input requires its navigation revision"); }
        command["navigationRevision"] = *navigationRevision;
    }
    if (openingControlsRevision) {
        if (!*openingControlsRevision) throw std::runtime_error("Opening policy revision must be positive");
        if (input == Input::Toggle || input == Input::Select || input == Input::PreviousChoice || input == Input::NextChoice) {
            if (!navigationRevision) throw std::runtime_error("Opening mutation requires its captured navigation revision");
            command["navigationRevision"] = *navigationRevision;
            command["openingControlsRevision"] = *openingControlsRevision;
        }
    }
    if (menuRevision) {
        // Mechanical scopes carry all three observed epochs and retain legitimate zero menu revisions
        const bool mechanical = input == Input::PreviousChoice || input == Input::NextChoice
            || input == Input::Back || input == Input::Close || input == Input::JumpBack;
        if (!mechanical || !navigationRevision || !localAdmissionRevision || !*localAdmissionRevision) {
            throw std::runtime_error("Mechanical menu scope requires navigation, menu and positive admission revisions");
        }
        command["navigationRevision"] = *navigationRevision;
        command["localAdmissionRevision"] = *localAdmissionRevision;
        command["menuRevision"] = *menuRevision;
    }
    return command.dump();
}

std::string ReadyJson(DWORD processId) {
    return Json({ {"version", ProtocolVersion}, {"processId", processId}, {"ready", true} }).dump();
}

std::string RendererStatusJson(DWORD processId, const RendererStatus& status) {
    // Fixed field names and numeric values keep this diagnostic packet bounded
    return Json({ {"version", ProtocolVersion}, {"processId", processId},
        {"initialized", status.initialized}, {"drawnFrames", status.drawnFrames},
        {"faults", status.faults}, {"featureLevel", status.featureLevel},
        {"width", status.width}, {"height", status.height} }).dump();
}

std::string RuntimeErrorJson(DWORD processId) {
    // Fixed text cannot echo private state, exception contents, paths, or memory values
    return Json({ {"version", ProtocolVersion}, {"processId", processId},
        {"error", "Overlay runtime worker stopped after a protocol or IO failure"} }).dump();
}

}
