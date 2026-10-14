#include "TestSupport.h"

namespace iw::overlay::tests {
namespace {
Json DesignAppearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
}
}
void MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates() {
    for (const Json& invalid : {Json(nullptr), Json(false), Json(1), Json(1.0), Json::array(), Json::object(),
        Json(""), Json("app"), Json("apparition"), Json("Aio"), Json("Classic "), Json("UNKNOWN")}) {
        auto packet = StateJson(); packet["appearance"] = DesignAppearance(); packet["appearance"]["design"] = invalid;
        // Closed packets use the same strict design boundary as visible packets
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    for (const auto* duplicate : {"\"design\":\"Apparition\",\"design\":\"Classic\"",
        "\"design\":\"Apparition\",\"\\u0064esign\":\"Classic\""}) {
        auto packet = StateJson(); packet["appearance"] = DesignAppearance(); packet["appearance"]["design"] = "Apparition";
        auto bytes = packet.dump(); const std::string field = "\"design\":\"Apparition\"";
        const auto start = bytes.find(field); Check(start != std::string::npos, "Design duplicate fixture lost its field");
        bytes.replace(start, field.size(), duplicate);
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}

void MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames() {
    auto packet = StateJson();
    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.design == MenuDesign::Apparition,
        "Missing legacy appearance changed default design");
    packet["appearance"] = nullptr;
    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.design == MenuDesign::Apparition,
        "Null legacy appearance changed default design");
    packet["appearance"] = DesignAppearance();
    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.design == MenuDesign::Apparition,
        "An older appearance object lost its default design");
    const char* names[] = {"Apparition", "Classic", "Native", "AIO", "Basic"};
    const MenuDesign designs[] = {MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
    for (std::size_t index = 0; index < std::size(names); ++index) {
        packet["appearance"]["design"] = names[index]; packet["appearance"]["coloredCursor"] = false;
        Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance.design == designs[index],
            "An exact source design did not reach its bounded enum");
    }
}
void MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState() {
    // Closed and open states require the same atomic design and flag normalization
    for (bool open : {false, true}) {
        for (const auto* name : {"Classic", "Native", "AIO", "Basic"}) {
            auto packet = StateJson(); packet["isOpen"] = open; packet["appearance"] = DesignAppearance();
            packet["appearance"]["design"] = name;
            const bool colorRestricted = std::string(name) == "Classic" || std::string(name) == "Native";
            packet["appearance"]["coloredCursor"] = colorRestricted;
            packet["appearance"]["optionCounter"] = !colorRestricted;
            Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
            packet["appearance"][colorRestricted ? "coloredCursor" : "optionCounter"] = false;
            const auto appearance = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance;
            Check(!appearance.coloredCursor && !appearance.optionCounter,
                "A normalized source design could not be read unchanged");
        }
    }
}
void MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields() {
    auto packet = StateJson(); packet["appearance"] = DesignAppearance(); packet["appearance"]["design"] = "Classic";
    packet["appearance"]["coloredCursor"] = false; packet["appearance"]["optionCounter"] = true;
    packet["appearance"]["quickExit"] = true; packet["appearance"]["menuWidth"] = 300;
    const auto owned = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
    packet["appearance"]["design"] = "Apparition";
    Check(owned->appearance.design == MenuDesign::Classic && !owned->appearance.coloredCursor
        && owned->appearance.optionCounter && owned->appearance.quickExit && owned->appearance.menuWidth == 300,
        "Design parsing changed independent preferences or retained caller storage");
    // Existing aggregate initializers keep their old final field and the appended default
    const MenuAppearance legacy{"Text", "Left", false, true, false, true, true, true, 300,
        true, false, false, InstructionPosition{1, 2}, 100, MenuPosition{3, 4}};
    Check(legacy.design == MenuDesign::Apparition && legacy.menuPosition == MenuPosition{3, 4}
        && legacy.scrollAnimationMilliseconds == 100 && legacy.booleanDisplay == "Text",
        "The new design field changed older aggregate positions");
}
}
