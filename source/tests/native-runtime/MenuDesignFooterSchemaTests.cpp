#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
Json FooterAppearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
}
// A changed design retains the captured body style while its footer uses the committed geometry
Json FooterPacket() {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["title"] = "Menu Preferences"; packet["navigationRevision"] = 7; packet["menuRevision"] = 6;
    packet["menuDesignTransition"] = {{"phase", "rebuild"}, {"footerDesign", "AIO"}};
    return packet;
}
}
void MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance() {
    auto packet = FooterPacket();
    const auto owned = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    packet["menuDesignTransition"]["footerDesign"] = "Basic";
    Check(owned->appearance == MenuAppearance{} && owned->menuDesignTransition == MenuDesignTransitionPhase::Rebuild
        && runtime::InputModeForSnapshot(*owned) == runtime::InputMode::MenuLocked
        && owned->navigationRevision == 7 && owned->menuRevision == 6 && owned->menuDesignFooterDesign == MenuDesign::Aio,
        "The committed footer target changed captured appearance, public epochs or locked input");
}

void MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets() {
    auto packet = FooterPacket(); packet.erase("menuDesignTransition");
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuDesignFooterDesign,
        "An ordinary legacy menu invented a footer target");
    packet["menuDesignTransition"] = nullptr;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuDesignFooterDesign,
        "A null legacy transition invented a footer target");
    // Legacy phase-only and explicit null descriptors retain captured geometry in both phases
    for (const auto* phase : {"rebuild", "cooldown"}) for (bool supplied : {false, true}) {
        packet["menuDesignTransition"] = {{"phase", phase}};
        if (supplied) packet["menuDesignTransition"]["footerDesign"] = nullptr;
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        Check(!state->menuDesignFooterDesign && state->appearance == MenuAppearance{}
            && runtime::InputModeForSnapshot(*state) == runtime::InputMode::MenuLocked,
            "Inactive footer metadata changed legacy appearance or locked input");
    }
    const char* names[] = {"Apparition", "Classic", "Native", "AIO", "Basic"};
    const MenuDesign designs[] = {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
    for (std::size_t index = 0; index < std::size(names); ++index) {
        packet = FooterPacket(); packet["menuDesignTransition"]["footerDesign"] = names[index];
        MenuAppearance expected; expected.coloredCursor = index != 1 && index != 2;
        packet["appearance"] = FooterAppearance(); packet["appearance"]["coloredCursor"] = expected.coloredCursor;
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        packet["menuDesignTransition"]["footerDesign"] = "changed";
        Check(state->menuDesignFooterDesign == designs[index] && state->appearance == expected
            && state->rows.size() == 1 && state->selectedIndex == 0 && state->rows[0].value == "OFF"
            && state->navigationRevision == 7 && state->menuRevision == 6,
            "An exact footer target was not owned or replaced captured body data");
    }
    // Geometry data alone cannot create an input lock for typed callers
    MenuSnapshot typed; typed.menuDesignFooterDesign = MenuDesign::Aio;
    Check(runtime::InputModeForSnapshot(typed) == runtime::InputMode::Menu,
        "An unrelated footer geometry value changed ordinary input routing");
}

void MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates() {
    for (const Json& invalid : {Json(false), Json(1), Json(1.0), Json::array(), Json::object(),
        Json(""), Json("App"), Json("aio"), Json("Aio"), Json("Unknown")}) {
        auto packet = FooterPacket(); packet["menuDesignTransition"]["footerDesign"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    // No additional transition member can smuggle a separate layout or timing policy
    for (const auto* field : {"duration", "instructions", "design"}) {
        auto packet = FooterPacket(); packet["menuDesignTransition"][field] = nullptr;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        packet["menuDesignTransition"].erase("footerDesign");
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = FooterPacket(); packet["menuDesignTransition"].erase("phase");
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    // Both ordinary and escaped names must be unique before their enum is accepted
    for (const auto* duplicate : {"\"footerDesign\":\"Basic\"", "\"\\u0066ooterDesign\":\"Basic\""}) {
        auto bytes = FooterPacket().dump(); const std::string field = "\"footerDesign\":\"AIO\"";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Footer duplicate fixture lost its target");
        bytes.replace(start, field.size(), field + "," + duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}

void MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts() {
    // Even an equal, normalized target cannot introduce a descriptor outside the rebuild gap
    for (const auto* target : {"Apparition", "Classic", "Native", "AIO", "Basic"}) {
        auto packet = FooterPacket(); packet["menuDesignTransition"] = {{"phase", "cooldown"}, {"footerDesign", target}};
        packet["appearance"] = FooterAppearance(); packet["appearance"]["design"] = target;
        packet["appearance"]["coloredCursor"] = false;
        auto legacy = packet; legacy["menuDesignTransition"].erase("footerDesign");
        runtime::ParseState(legacy.dump(), GetCurrentProcessId());
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const auto* target : {"Classic", "Native", "AIO", "Basic"}) {
        auto packet = FooterPacket(); packet["menuDesignTransition"]["footerDesign"] = target;
        const bool counter = std::string(target) == "AIO" || std::string(target) == "Basic";
        packet["appearance"] = FooterAppearance(); packet["appearance"]["coloredCursor"] = !counter;
        packet["appearance"]["optionCounter"] = counter;
        auto legacy = packet; legacy["menuDesignTransition"].erase("footerDesign");
        runtime::ParseState(legacy.dump(), GetCurrentProcessId());
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (int conflict = 0; conflict < 11; ++conflict) {
        auto packet = FooterPacket();
        if (conflict == 0) packet["isOpen"] = false;
        if (conflict == 1) packet["isRootMenu"] = true;
        if (conflict == 2) packet["rows"] = Json::array();
        if (conflict == 3) packet["widthEditor"] = {{"step", 1}};
        if (conflict == 4) packet["instructionPositionEditor"] = {{"phase", "editing"}};
        if (conflict == 5) packet["menuPositionEditor"] = {{"step", 1}};
        if (conflict == 6) packet["menuCredits"] = {{"elapsedMilliseconds", 0}};
        if (conflict == 7) packet["menuUnlock"] = "pause";
        if (conflict == 8) packet["menuPreferenceRefresh"] = {{"phase", "pending"}};
        if (conflict == 9) packet["menuOpenTransition"] = {{"phase", "cooldown"}};
        if (conflict == 10) packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
        // Conflicting views must be valid on their own so their shape cannot hide the exclusion
        if (conflict >= 3) {
            if (conflict <= 6) packet["rows"] = Json::array();
            if (conflict == 10) packet["rows"].push_back(packet["rows"][0]);
            auto standalone = packet; standalone.erase("menuDesignTransition");
            runtime::ParseState(standalone.dump(), GetCurrentProcessId());
        }
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
}
}
