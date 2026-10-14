#include "../../native/shared/MenuRowCapacity.h"
#include "TestSupport.h"

namespace iw::overlay::tests {
namespace {
Json AppearancePacket() {
    auto packet = StateJson();
    // Text is a valid starting view before Stealth reduces the visible window
    packet["appearance"] = {{"booleanDisplay", "Text"},    {"boolBoxLocation", "Right"},
                            {"instructionsVisible", true}, {"optionCounter", false},
                            {"coloredCursor", true},       {"spotlightCursor", true},
                            {"largeCursor", false}};
    return packet;
}
} // namespace
void StealthAppearanceRejectsExplicitNullBeforeReachingLayout() {
    auto packet = AppearancePacket();
    packet["appearance"]["stealthUI"] = nullptr;
    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
}
void StealthAppearancePreservesLegacyAndOwnsBothBooleanValues() {
    auto packet = AppearancePacket();
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.stealthUI,
          "Missing Stealth flag changed legacy appearance");
    for (bool enabled : {false, true}) {
        packet["appearance"]["stealthUI"] = enabled;
        const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
        packet["appearance"]["stealthUI"] = !enabled;
        Check(state->appearance.stealthUI == enabled && state->appearance.menuWidth == 260,
              "Stealth retained caller storage or changed width");
    }
    packet["appearance"] = nullptr;
    Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.stealthUI,
          "Null legacy appearance enabled Stealth");
}
void StealthAppearanceRejectsWrongTypesAndDecodedDuplicates() {
    for (const Json &invalid : {Json(0), Json(1), Json(1.0), Json("true"), Json::array(), Json::object()}) {
        auto packet = AppearancePacket();
        packet["appearance"]["stealthUI"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = AppearancePacket();
    packet["appearance"]["stealthUI"] = true;
    for (const auto *duplicate : {"\"stealthUI\":false", "\"stealth\\u0055I\":false"}) {
        auto bytes = packet.dump();
        const std::string field = "\"stealthUI\":true";
        bytes.replace(bytes.find(field), field.size(), field + "," + duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}
void StealthAppearanceRejectsBoxesAndCounterWithoutNormalizingFlags() {
    for (bool open : {false, true})
        for (int invalid = 0; invalid < 3; ++invalid) {
            auto packet = AppearancePacket();
            packet["isOpen"] = open;
            packet["appearance"]["stealthUI"] = true;
            if (invalid != 1)
                packet["appearance"]["booleanDisplay"] = "Boxes";
            if (invalid != 0)
                packet["appearance"]["optionCounter"] = true;
            Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
        }
}
void StealthCapacityIsFiveAcrossEveryDesignAndInstructionVariant() {
    for (const auto design : {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native,
                              MenuDesign::Aio, MenuDesign::Basic}) {
        for (bool visible : {false, true})
            for (bool alternate : {false, true}) {
                MenuAppearance appearance;
                appearance.stealthUI = true;
                appearance.booleanDisplay = "Text";
                appearance.design = design;
                appearance.instructionsVisible = visible;
                appearance.alternateInstructions = alternate;
                Check(MenuRowCapacity(appearance) == 5 && MenuRowsShown(appearance, 128) == 5 &&
                          MenuOpeningDurationMilliseconds(appearance, 128) == 400,
                      "Stealth capacity inherited an ordinary Design or footer adjustment");
                Check(MenuRowsShown(appearance, 3) == 3 &&
                          MenuOpeningDurationMilliseconds(appearance, 1) == 0,
                      "Stealth capacity invented rows on a short page");
            }
    }
}
} // namespace iw::overlay::tests
