#include "TestSupport.h"
#include <array>

namespace iw::overlay::tests {
namespace {
Json LegacyAppearance() {
    // The seven original settings remain required when an object is supplied
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"},
        {"instructionsVisible", true}, {"optionCounter", false}, {"coloredCursor", true},
        {"spotlightCursor", true}, {"largeCursor", false}};
}

void CheckInstructionDefaults(const MenuAppearance& appearance) {
    Check(!appearance.alternateInstructions && appearance.instructionsBackground
        && !appearance.adaptiveInstructions, "Legacy packets changed instruction defaults");
}
}

void InstructionAppearanceDefaultsPreserveLegacyPackets() {
    // Existing aggregate callers still place width after their original quick-exit setting
    const MenuAppearance legacyAggregate{"Text", "Left", false, true, false, false, true, true, 500};
    Check(legacyAggregate.menuWidth == 500 && legacyAggregate.quickExit && legacyAggregate.largeCursor,
        "Appended instruction fields changed an existing aggregate initializer");
    CheckInstructionDefaults(legacyAggregate);
    auto source = StateJson();
    CheckInstructionDefaults(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance);
    source["appearance"] = nullptr;
    CheckInstructionDefaults(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance);
    source["appearance"] = LegacyAppearance();
    CheckInstructionDefaults(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance);

    // Each optional setting keeps its own default when another setting is supplied
    source["appearance"]["alternateInstructions"] = true;
    auto appearance = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance;
    Check(appearance.alternateInstructions && appearance.instructionsBackground
        && !appearance.adaptiveInstructions, "One optional field overwrote omitted defaults");
    source["appearance"] = LegacyAppearance();
    source["appearance"]["instructionsBackground"] = false;
    appearance = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance;
    Check(!appearance.alternateInstructions && !appearance.instructionsBackground
        && !appearance.adaptiveInstructions, "Background choice changed omitted instruction defaults");
    source["appearance"] = LegacyAppearance();
    source["appearance"]["adaptiveInstructions"] = true;
    appearance = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance;
    Check(!appearance.alternateInstructions && appearance.instructionsBackground
        && appearance.adaptiveInstructions, "Adaptive choice changed omitted instruction defaults");
}

void InstructionAppearanceChoicesAreOwnedWithoutChangingState() {
    for (bool alternate : {false, true}) {
        for (bool background : {false, true}) {
            for (bool adaptive : {false, true}) {
                // Source handlers do not allow alternate style and adaptive placement together
                if (alternate && adaptive) continue;
                auto source = StateJson();
                source["appearance"] = LegacyAppearance();
                source["appearance"]["alternateInstructions"] = alternate;
                source["appearance"]["instructionsBackground"] = background;
                source["appearance"]["adaptiveInstructions"] = adaptive;
                source["navigationRevision"] = 73;
                const std::vector<std::string> hints{"Saved scroll", "Saved select", "Saved back"};
                const auto parsed = runtime::ParseState(source.dump(), GetCurrentProcessId(), &hints);
                const auto& snapshot = *parsed.snapshot;
                MenuAppearance expected;
                expected.alternateInstructions = alternate;
                expected.instructionsBackground = background;
                expected.adaptiveInstructions = adaptive;
                Check(snapshot.appearance == expected, "A supplied instruction choice was lost or coerced");

                // Presentation cannot alter feature values, private controls or navigation identity
                Check(snapshot.rows.size() == 1 && snapshot.rows[0].label == "Row"
                    && snapshot.rows[0].value == "OFF" && snapshot.rows[0].enabled
                    && snapshot.instructions == hints && snapshot.navigationRevision == 73
                    && snapshot.selectedIndex == 0 && parsed.lastSequence == 0 && parsed.generation == 1,
                    "Instruction choices changed unrelated state or controller identity");
                source["appearance"]["alternateInstructions"] = !alternate;
                source["appearance"]["instructionsBackground"] = !background;
                source["appearance"]["adaptiveInstructions"] = !adaptive;
                Check(snapshot.appearance == expected, "A parsed instruction choice retained mutable JSON state");
            }
        }
    }
}

void InstructionAppearanceRejectsAlternateAdaptiveConflict() {
    for (bool background : {false, true}) {
        auto source = StateJson();
        source["appearance"] = LegacyAppearance();
        source["appearance"]["alternateInstructions"] = true;
        source["appearance"]["adaptiveInstructions"] = true;
        source["appearance"]["instructionsBackground"] = background;
        // Background visibility cannot make the two exclusive styles valid
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
    }
}

void InstructionAppearanceRejectsEverySuppliedNonBooleanType() {
    constexpr std::array fields{"alternateInstructions", "instructionsBackground", "adaptiveInstructions"};
    const std::array<Json, 10> invalid{nullptr, "true", "false", 0, 1, -1, 0.5,
        Json::array(), Json::object(), Json::array({true})};
    for (const auto* field : fields) {
        for (const auto& value : invalid) {
            auto source = StateJson();
            source["appearance"] = LegacyAppearance();
            source["appearance"][field] = value;
            // Null optional members are malformed even though a null whole appearance is legacy
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
}

}
