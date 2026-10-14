#include "TestSupport.h"
#include "../../native/shared/MenuRowCapacity.h"
#include <limits>

namespace iw::overlay::tests {
void MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration() {
    struct Case { const char* display; bool instructions, counter; std::size_t capacity; };
    // Expected capacities are an explicit source table rather than a copy of the helper formula
    const Case cases[] = {{"Boxes", true, false, 10}, {"Boxes", false, false, 11},
        {"Text", true, false, 12}, {"Text", false, false, 14}, {"Text Color", true, false, 12},
        {"Text Color", false, false, 14}, {"Boxes", true, true, 8}, {"Boxes", false, true, 9},
        {"Text", true, true, 10}, {"Text", false, true, 12}, {"Text Color", true, true, 10}, {"Text Color", false, true, 12}};
    for (const auto& sample : cases) {
        for (const auto* design : {"Apparition", "Classic", "Native", "AIO", "Basic"}) {
            if (sample.counter && (std::string(design) == "AIO" || std::string(design) == "Basic")) continue;
            auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
            packet["appearance"] = {{"booleanDisplay", sample.display}, {"boolBoxLocation", "Right"},
                {"instructionsVisible", sample.instructions}, {"optionCounter", sample.counter}, {"coloredCursor", false},
                {"spotlightCursor", true}, {"largeCursor", false}, {"design", design}};
            const auto appearance = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->appearance;
            Check(MenuRowCapacity(appearance) == sample.capacity && sample.capacity >= 8 && sample.capacity <= MaximumMenuRowsShown,
                "Shared capacity differs from the source instruction, display and counter table");
            // Short, full, clipped and maximum protocol row counts use the same strict time boundary
            for (std::size_t count : {std::size_t{0}, std::size_t{1}, std::size_t{2}, std::size_t{8},
                std::size_t{10}, std::size_t{14}, std::size_t{128}}) {
                packet["rows"] = Json::array();
                for (std::size_t row = 0; row < count; ++row) packet["rows"].push_back({{"label", "Row"}, {"value", "OFF"}, {"enabled", true}});
                const auto shown = std::min(count, sample.capacity);
                const int duration = shown < 2 ? 0 : static_cast<int>((shown - 1) * 100);
                Check(MenuRowsShown(appearance, count) == shown && MenuOpeningDurationMilliseconds(appearance, count) == duration
                    && duration <= MaximumMenuOpeningMilliseconds, "Shared opening duration exceeded its source bounds");
                packet["menuOpening"] = {{"revision", 1}, {"elapsedMilliseconds", 0}};
                if (duration == 0) {
                    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
                } else {
                    packet["menuOpening"]["elapsedMilliseconds"] = duration - 1;
                    Check(runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuOpening->elapsedMilliseconds == duration - 1,
                        "The last active opening millisecond was rejected");
                    packet["menuOpening"]["elapsedMilliseconds"] = duration;
                    Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
                }
            }
            // Capacity bounds the largest row count before subtraction or multiplication
            Check(MenuOpeningDurationMilliseconds(appearance, std::numeric_limits<std::size_t>::max())
                == static_cast<int>((sample.capacity - 1) * 100), "Largest row count overflowed the opening duration");
        }
    }
}
}
