#include "../../native/runtime/chrome/ChromeThemeSchema.h"
#include "../../native/runtime/instructions/InstructionThemeSchema.h"
#include "../../native/runtime/instructions/InstructionHudSchema.h"
#include "../../native/runtime/MenuChromeSchema.h"
#include "../../native/runtime/MenuBooleanPaintSchema.h"
#include "../../native/runtime/MenuOptionCallbackSchema.h"
#include "../../native/runtime/monitor/MenuMonitorWaitSchema.h"
#include "../../native/runtime/monitor/MenuUseContinuationSchema.h"
#include "../../native/runtime/monitor/MenuMonitorContinuationSchema.h"
#include "BackendPreview.h"
#include "../../native/runtime/OpeningControlsSchema.h"
#include "../../native/runtime/LocalMenuEligibilitySchema.h"
#include <fstream>
#include <stdexcept>
#include <limits>
#include <nlohmann/json.hpp>
#include "../../native/runtime/BoundedSessionJson.h"
#include "../../native/shared/MenuRowCapacity.h"

namespace iw::overlay::tests {
namespace {
std::uint64_t UnsignedInteger(const nlohmann::json& value) {
    if (!value.is_number_integer() || (!value.is_number_unsigned() && value.get<std::int64_t>() < 0)) {
        throw std::runtime_error("A preview unsigned integer is required");
    }
    return value.get<std::uint64_t>();
}
MenuDesign Design(const nlohmann::json& value) {
    if (!value.is_string()) throw std::runtime_error("A preview design must be a string");
    const auto design = value.get<std::string>();
    if (design == "Apparition") return MenuDesign::Apparition;
    if (design == "Classic") return MenuDesign::Classic;
    if (design == "Native") return MenuDesign::Native;
    if (design == "AIO") return MenuDesign::Aio;
    if (design == "Basic") return MenuDesign::Basic;
    throw std::runtime_error("Unsupported preview menu design");
}
std::int32_t Coordinate(const nlohmann::json& value) {
    if (!value.is_number_integer()) throw std::runtime_error("A preview coordinate must be a signed32 integer");
    if (value.is_number_unsigned()) {
        const auto number = value.get<std::uint64_t>();
        if (number > static_cast<std::uint64_t>(std::numeric_limits<std::int32_t>::max())) {
            throw std::runtime_error("Preview coordinate exceeds signed32 bounds");
        }
        return static_cast<std::int32_t>(number);
    }
    const auto number = value.get<std::int64_t>();
    if (number < std::numeric_limits<std::int32_t>::min() || number > std::numeric_limits<std::int32_t>::max()) {
        throw std::runtime_error("Preview coordinate exceeds signed32 bounds");
    }
    return static_cast<std::int32_t>(number);
}
}
MenuSnapshot ReadBackendPreview(const std::filesystem::path& path, const runtime::OpeningActionLabels* openingLabels) {
    // Bound test inputs before loading an exported backend fixture
    if (std::filesystem::file_size(path) > 32768) { throw std::runtime_error("Preview fixture is too large"); }
    std::ifstream file(path);
    const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
    // The real bounded parser also rejects duplicate decoded names in preview exports
    const auto value = runtime::ReadBoundedSessionJson(bytes, 32768);
    MenuSnapshot model;
    if (value.contains("menuChromeRevision")) model.menuChromeRevision = runtime::ParseMenuChromeRevision(value.at("menuChromeRevision"));
    // Preview facts cross the same strict boundary without inventing a local actor
    if (value.contains("localMenuEligibility")) model.localMenuEligibility = runtime::ParseLocalMenuEligibility(value.at("localMenuEligibility"));
    if (value.contains("menuSurface")) model.menuSurface = runtime::ParseMenuSurface(value.at("menuSurface"));
    if (value.contains("mainOpeningControls")) model.mainOpeningControls = runtime::ParseMainOpeningControls(value.at("mainOpeningControls"), openingLabels);
    if (value.contains("openingPolicies")) model.openingPolicies = runtime::ParseOpeningPolicies(value.at("openingPolicies"), openingLabels);
    if (!IsValidOpeningPolicyOwnership(model.mainOpeningControls, model.openingPolicies)) {
        throw std::runtime_error("Preview dual opening policies require their exact Main mirror");
    }
    model.isOpen = value.at("isOpen").get<bool>();
    if (value.contains("isRootMenu")) { model.isRootMenu = value.at("isRootMenu").get<bool>(); }
    model.title = value.at("title").get<std::string>();
    model.context = value.at("context").get<std::string>();
    model.notice = value.at("notice").get<std::string>();
    model.accent = value.at("accent").get<std::string>();
    // Animation retains the same required fixed-color boundary as the production parser
    if (model.accent.size() != 7 || model.accent[0] != '#') {
        throw std::runtime_error("Invalid preview menu accent");
    }
    for (std::size_t index = 1; index < model.accent.size(); ++index) {
        const char character = model.accent[index];
        if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')
            || (character >= 'A' && character <= 'F'))) {
            throw std::runtime_error("Invalid preview menu accent");
        }
    }
    if (value.contains("smoothRainbowTheme")) {
        // Closed previews still reject null and every supplied non-boolean mode
        const auto& mode = value.at("smoothRainbowTheme");
        if (!mode.is_boolean()) throw std::runtime_error("A preview rainbow mode must be a boolean");
        model.smoothRainbowTheme = mode.get<bool>();
    }
    model.selectedIndex = value.at("selectedIndex").get<std::size_t>();
    // Draft and committed snapshots keep the controller's exact navigation epoch
    if (value.contains("navigationRevision")) { model.navigationRevision = value.at("navigationRevision").get<std::uint64_t>(); }
    // Absence preserves legacy snapping while zero is a present and valid page revision
    if (value.contains("menuRevision")) model.menuRevision = UnsignedInteger(value.at("menuRevision"));
    if (value.contains("appearance") && !value.at("appearance").is_null()) {
        const auto& appearance = value.at("appearance");
        model.appearance.booleanDisplay = appearance.at("booleanDisplay").get<std::string>();
        model.appearance.boolBoxLocation = appearance.at("boolBoxLocation").get<std::string>();
        model.appearance.instructionsVisible = appearance.at("instructionsVisible").get<bool>();
        model.appearance.optionCounter = appearance.at("optionCounter").get<bool>();
        model.appearance.coloredCursor = appearance.at("coloredCursor").get<bool>();
        model.appearance.spotlightCursor = appearance.at("spotlightCursor").get<bool>();
        model.appearance.largeCursor = appearance.at("largeCursor").get<bool>();
        if (appearance.contains("design")) {
            model.appearance.design = Design(appearance.at("design"));
        }
        // Preview refuses incompatible producer flags instead of hiding a broken packet
        if ((model.appearance.design == MenuDesign::Classic || model.appearance.design == MenuDesign::Native)
            && model.appearance.coloredCursor) throw std::runtime_error("Preview design cannot use a colored cursor");
        if ((model.appearance.design == MenuDesign::Aio || model.appearance.design == MenuDesign::Basic)
            && model.appearance.optionCounter) throw std::runtime_error("Preview design cannot use an option counter");
        if (appearance.contains("menuWidth")) { model.appearance.menuWidth = appearance.at("menuWidth").get<int>(); }
        if (appearance.contains("scrollAnimationMilliseconds")) {
            const auto duration = UnsignedInteger(appearance.at("scrollAnimationMilliseconds"));
            if (duration < 100 || duration > 250 || duration % 10 != 0) {
                throw std::runtime_error("Unsupported preview scroll animation duration");
            }
            model.appearance.scrollAnimationMilliseconds = static_cast<int>(duration);
        }
        if (appearance.contains("quickExit")) { model.appearance.quickExit = appearance.at("quickExit").get<bool>(); }
        // Preview choices use the same strict optional booleans as the runtime contract
        const auto instructionChoice = [&](const char* field, bool& destination) {
            if (!appearance.contains(field)) return;
            const auto& choice = appearance.at(field);
            if (!choice.is_boolean()) { throw std::runtime_error("An instruction choice must be a boolean"); }
            destination = choice.get<bool>();
        };
        instructionChoice("stealthUI", model.appearance.stealthUI);
        if (model.appearance.stealthUI && (model.appearance.booleanDisplay == "Boxes" || model.appearance.optionCounter)) {
            throw std::runtime_error("Preview Stealth requires text display without an option counter");
        }
        instructionChoice("alternateInstructions", model.appearance.alternateInstructions);
        instructionChoice("instructionsBackground", model.appearance.instructionsBackground);
        instructionChoice("adaptiveInstructions", model.appearance.adaptiveInstructions);
        if (model.appearance.alternateInstructions && model.appearance.adaptiveInstructions) {
            throw std::runtime_error("Alternate and adaptive preview instructions cannot be combined");
        }
        const auto position = appearance.find("instructionPosition");
        if (position != appearance.end() && !position->is_null()) {
            if (!position->is_object() || position->size() != 2 || !position->contains("x") || !position->contains("y")) {
                throw std::runtime_error("Preview position requires exactly x and y");
            }
            model.appearance.instructionPosition = InstructionPosition{Coordinate(position->at("x")), Coordinate(position->at("y"))};
        }
    }
    if (value.contains("appearance") && !value.at("appearance").is_null()) {
        const auto& appearance = value.at("appearance");
        const auto menuPosition = appearance.find("menuPosition");
        if (menuPosition != appearance.end() && !menuPosition->is_null()) {
            // Preview preserves the same exact signed panel origin as production parsing
            if (!menuPosition->is_object() || menuPosition->size() != 2
                || !menuPosition->contains("x") || !menuPosition->contains("y")) {
                throw std::runtime_error("Preview menu position requires exactly x and y");
            }
            model.appearance.menuPosition = MenuPosition{Coordinate(menuPosition->at("x")), Coordinate(menuPosition->at("y"))};
        }
    }
    if (value.contains("widthEditor") && !value.at("widthEditor").is_null()) {
        // Actual metadata owns the displayed editor step; ordinary rows cannot override it
        model.widthEditor = WidthEditor{};
        model.widthEditor->step = value.at("widthEditor").at("step").get<int>();
    }
    if (value.contains("menuUnlock") && !value.at("menuUnlock").is_null()) {
        const auto phase = value.at("menuUnlock").get<std::string>();
        if (phase == "rebuild") model.menuUnlock = MenuUnlockPhase::Rebuild;
        else if (phase == "pause") model.menuUnlock = MenuUnlockPhase::Pause;
        else throw std::runtime_error("Unsupported preview unlock phase");
    }
    const auto positionEditor = value.find("instructionPositionEditor");
    if (positionEditor != value.end() && !positionEditor->is_null()) {
        if (!model.isOpen || model.isRootMenu || model.widthEditor || model.menuUnlock
            || !model.appearance.instructionsVisible || !value.at("rows").empty()
            || model.selectedIndex != 0 || !positionEditor->is_object()
            || positionEditor->size() != 1 || !positionEditor->contains("phase") || !positionEditor->at("phase").is_string()) {
            throw std::runtime_error("Invalid preview instruction position editor context");
        }
        InstructionPositionEditor editor;
        const auto phase = positionEditor->at("phase").get<std::string>();
        if (phase == "entering") editor.phase = InstructionPositionEditorPhase::Entering;
        else if (phase == "editing") editor.phase = InstructionPositionEditorPhase::Editing;
        else if (phase == "exiting") editor.phase = InstructionPositionEditorPhase::Exiting;
        else throw std::runtime_error("Unsupported preview position editor phase");
        model.instructionPositionEditor = std::move(editor);
    }
    const auto menuPositionEditor = value.find("menuPositionEditor");
    if (menuPositionEditor != value.end() && !menuPositionEditor->is_null()) {
        if (!model.isOpen || model.isRootMenu || model.widthEditor || model.instructionPositionEditor || model.menuUnlock
            || !value.at("rows").is_array() || !value.at("rows").empty() || model.selectedIndex != 0
            || !menuPositionEditor->is_object() || menuPositionEditor->size() != 1 || !menuPositionEditor->contains("step")) {
            throw std::runtime_error("Invalid preview menu position editor context");
        }
        const auto step = UnsignedInteger(menuPositionEditor->at("step"));
        if (step < 1 || step > 10) throw std::runtime_error("Unsupported preview menu position offset");
        model.menuPositionEditor = MenuPositionEditor{};
        model.menuPositionEditor->step = static_cast<int>(step);
    }
    const auto credits = value.find("menuCredits");
    if (credits != value.end() && !credits->is_null()) {
        if (!model.isOpen || model.isRootMenu || model.widthEditor || model.instructionPositionEditor
            || model.menuPositionEditor || model.menuUnlock || !value.at("rows").is_array()
            || !value.at("rows").empty() || model.selectedIndex != 0
            || !credits->is_object() || credits->size() != 1 || !credits->contains("elapsedMilliseconds")) {
            throw std::runtime_error("Invalid preview menu credits context");
        }
        const auto elapsed = UnsignedInteger(credits->at("elapsedMilliseconds"));
        if (elapsed >= 40000) throw std::runtime_error("Unsupported preview menu credits progress");
        model.menuCredits = MenuCredits{};
        model.menuCredits->elapsedMilliseconds = static_cast<int>(elapsed);
    }
    // This copies display fields only and ignores action identifiers
    for (const auto& row : value.at("rows")) {
        if (model.rows.size() == 128) { throw std::runtime_error("Preview fixture has too many rows"); }
        MenuRow parsed{row.at("label").get<std::string>(), row.at("value").get<std::string>(), row.at("enabled").get<bool>()};
        runtime::ParseOpeningRowMetadata(row, parsed, openingLabels);
        model.rows.push_back(std::move(parsed));
    }
    if (model.rows.empty() ? model.selectedIndex != 0 : model.selectedIndex >= model.rows.size())
        throw std::runtime_error("A preview selection is outside its ordinary rows");
    if (!model.rows.empty() && model.rows[model.selectedIndex].isSpacer)
        throw std::runtime_error("A final preview selection cannot be a spacer");
    const auto refresh = value.find("menuPreferenceRefresh");
    if (refresh != value.end() && !refresh->is_null()) {
        const auto& rows = value.at("rows");
        const auto selected = UnsignedInteger(value.at("selectedIndex"));
        // CPU previews enforce the same ordinary child context as production parsing
        if (!model.isOpen || model.isRootMenu || model.widthEditor || model.instructionPositionEditor
            || model.menuPositionEditor || model.menuCredits || model.menuUnlock || !rows.is_array() || rows.empty()
            || selected >= rows.size() || !refresh->is_object() || refresh->size() != 1
            || !refresh->contains("phase") || !refresh->at("phase").is_string()) {
            throw std::runtime_error("Invalid preview preference refresh context");
        }
        const auto phase = refresh->at("phase").get<std::string>();
        if (phase == "pending") model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Pending;
        else if (phase == "cooldown") model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Cooldown;
        else if (phase == "selector" && runtime::IsOpeningSelectorRefreshContext(model))
            model.menuPreferenceRefresh = MenuPreferenceRefreshPhase::Selector;
        else throw std::runtime_error("Unsupported preview preference refresh phase");
    }
    const auto designTransition = value.find("menuDesignTransition");
    if (designTransition != value.end() && !designTransition->is_null()) {
        const auto& rows = value.at("rows");
        const auto selected = UnsignedInteger(value.at("selectedIndex"));
        if (!model.isOpen || model.isRootMenu || model.widthEditor || model.instructionPositionEditor
            || model.menuPositionEditor || model.menuCredits || model.menuUnlock || model.menuPreferenceRefresh
            || !rows.is_array() || rows.empty() || selected >= rows.size() || !designTransition->is_object()
            || designTransition->size() != (designTransition->contains("footerDesign") ? 2u : 1u)
            || !designTransition->contains("phase") || !designTransition->at("phase").is_string()) {
            throw std::runtime_error("Invalid preview menu design transition context");
        }
        const auto phase = designTransition->at("phase").get<std::string>();
        if (phase == "rebuild") model.menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
        else if (phase == "cooldown") model.menuDesignTransition = MenuDesignTransitionPhase::Cooldown;
        else throw std::runtime_error("Unsupported preview menu design transition phase");
        const auto footer = designTransition->find("footerDesign");
        if (footer != designTransition->end() && !footer->is_null()) {
            // Optional legacy descriptors keep captured geometry; active targets belong only to rebuild
            if (model.menuDesignTransition != MenuDesignTransitionPhase::Rebuild) {
                throw std::runtime_error("Preview footer design requires a menu design rebuild");
            }
            const auto target = Design(*footer);
            if ((target == MenuDesign::Classic || target == MenuDesign::Native) && model.appearance.coloredCursor) {
                throw std::runtime_error("This preview footer design cannot use a colored cursor");
            }
            if ((target == MenuDesign::Aio || target == MenuDesign::Basic) && model.appearance.optionCounter) {
                throw std::runtime_error("This preview footer design cannot use an option counter");
            }
            model.menuDesignFooterDesign = target;
        }
    }
    const auto preferenceRebuild = value.find("menuPreferenceRebuild");
    if (preferenceRebuild != value.end()) {
        const auto& rows = value.at("rows");
        const auto selected = UnsignedInteger(value.at("selectedIndex"));
        // Preview applies the same populated child and single-owner boundary as the runtime
        if (!model.isOpen || model.isRootMenu || !rows.is_array() || rows.empty() || selected >= rows.size()
            || model.widthEditor || model.instructionPositionEditor || model.menuPositionEditor
            || model.menuCredits || model.menuUnlock || model.menuPreferenceRefresh || model.menuDesignTransition
            || !preferenceRebuild->is_object() || preferenceRebuild->size() != 1
            || !preferenceRebuild->contains("phase") || !preferenceRebuild->at("phase").is_string()) {
            throw std::runtime_error("Invalid preview preference rebuild context");
        }
        const auto phase = preferenceRebuild->at("phase").get<std::string>();
        if (phase == "rebuild") model.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Rebuild;
        else if (phase == "cooldown") model.menuPreferenceRebuild = MenuPreferenceRebuildPhase::Cooldown;
        else throw std::runtime_error("Unsupported preview preference rebuild phase");
    }
    const auto openTransition = value.find("menuOpenTransition");
    if (openTransition != value.end() && !openTransition->is_null()) {
        const auto& rows = value.at("rows");
        const auto selected = UnsignedInteger(value.at("selectedIndex"));
        // Empty retained pages are ordinary menus; their only valid cursor remains zero
        if (!model.isOpen || model.widthEditor || model.instructionPositionEditor || model.menuPositionEditor
            || model.menuCredits || model.menuUnlock || model.menuPreferenceRefresh || model.menuDesignTransition || model.menuPreferenceRebuild
            || !rows.is_array() || (rows.empty() ? selected != 0 : selected >= rows.size())
            || !openTransition->is_object() || openTransition->size() != 1 || !openTransition->contains("phase")
            || !openTransition->at("phase").is_string()) {
            throw std::runtime_error("Invalid preview normal menu open context");
        }
        const auto phase = openTransition->at("phase").get<std::string>();
        if (phase == "rebuild") model.menuOpenTransition = MenuOpenTransitionPhase::Rebuild;
        else if (phase == "cooldown") model.menuOpenTransition = MenuOpenTransitionPhase::Cooldown;
        else throw std::runtime_error("Unsupported preview normal menu open phase");
    }
    const auto callback = value.find("menuOptionCallback");
    if (callback != value.end()) {
        // Explicit null is malformed; omission is the legacy ordinary-menu path
        model.menuOptionCallback = runtime::ParseMenuOptionCallback(*callback, model);
    }
    const auto opening = value.find("menuOpening");
    if (opening != value.end() && !opening->is_null()) {
        const auto& rows = value.at("rows");
        const auto selected = UnsignedInteger(value.at("selectedIndex"));
        if (!model.isOpen || (model.menuOptionCallback && !CallbackKeepsOpening(*model.menuOptionCallback)) || model.widthEditor || model.instructionPositionEditor || model.menuPositionEditor
            || model.menuCredits || model.menuUnlock || model.menuPreferenceRefresh == MenuPreferenceRefreshPhase::Cooldown
            || model.menuPreferenceRebuild == MenuPreferenceRebuildPhase::Rebuild
            || model.menuOpenTransition == MenuOpenTransitionPhase::Rebuild || (model.menuDesignTransition
                && *model.menuDesignTransition != MenuDesignTransitionPhase::Cooldown)
            || !rows.is_array() || rows.empty() || selected >= rows.size() || !opening->is_object()
            || opening->size() != 2 || !opening->contains("revision") || !opening->contains("elapsedMilliseconds")) {
            throw std::runtime_error("Invalid preview menu opening context");
        }
        const auto revision = UnsignedInteger(opening->at("revision"));
        const auto elapsed = UnsignedInteger(opening->at("elapsedMilliseconds"));
        const auto duration = MenuOpeningDurationMilliseconds(model.appearance, rows.size());
        if (revision == 0 || duration == 0 || elapsed >= static_cast<std::uint64_t>(duration)) {
            throw std::runtime_error("Unsupported preview menu opening progress");
        }
        // The shared source capacity keeps parser and preview duration limits identical
        model.menuOpening = MenuOpening{revision, static_cast<int>(elapsed)};
    }
    const auto paint = value.find("menuBooleanPaint");
    if (paint != value.end()) model.menuBooleanPaint = runtime::ParseMenuBooleanPaint(*paint, model);
    if (value.contains("instructionHud")) model.instructionHud = runtime::ParseInstructionHud(value.at("instructionHud"), model, openingLabels);
    if (value.contains("instructionTheme")) model.instructionTheme = runtime::ParseInstructionTheme(value.at("instructionTheme"), model);
    if (value.contains("chromeTheme")) model.chromeTheme = runtime::ParseChromeTheme(value.at("chromeTheme"), model);
    if (value.contains("menuMonitorWait") && !value.at("menuMonitorWait").is_null()) {
        model.menuMonitorWait = runtime::ParseMenuMonitorWait(value.at("menuMonitorWait"), model);
    }
    if (value.contains("menuUseContinuation") && !value.at("menuUseContinuation").is_null()) {
        model.menuUseContinuation = runtime::ParseMenuUseContinuation(value.at("menuUseContinuation"), model,
            UnsignedInteger(value.at("lastSequence")));
    }
    if (value.contains("menuMonitorContinuation") && !value.at("menuMonitorContinuation").is_null()) {
        model.menuMonitorContinuation = runtime::ParseMenuMonitorContinuation(value.at("menuMonitorContinuation"), model,
            UnsignedInteger(value.at("lastSequence")));
    }
    runtime::ValidateMenuDesignUseContinuation(model);
    return model;
}
}
