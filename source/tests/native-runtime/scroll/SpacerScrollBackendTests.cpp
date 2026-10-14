#include "SpacerScrollTestModels.h"
#include <fstream>
#include <iterator>
namespace iw::overlay::tests {
using namespace runtime::scroll;
namespace {
std::string ReadPacket(const std::filesystem::path &directory, const std::string &name) {
    const auto path = directory / name;
    Check(std::filesystem::is_regular_file(path) && std::filesystem::file_size(path) <= runtime::MaximumFileBytes,
          "A real spacer controller packet is missing or oversized");
    std::ifstream input(path, std::ios::binary);
    Check(static_cast<bool>(input), "Cannot read a real spacer controller packet");
    return {std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
}
runtime::ParsedState ParsePacket(const std::string &bytes, const SpacerScrollObservation *observation = nullptr) {
    return runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr, 8,
                               nullptr, nullptr, nullptr, observation);
}
}
void BackendSpacerScrollPromotesExactControllerAcksAcrossAllProductionBranches(const std::filesystem::path &directory) {
    // Legacy export sets remain valid and carry no invented private scroll event
    if (!std::filesystem::exists(directory / "spacer-scroll-ordinary-down-before.json")) return;
    struct Profile {
        const char *name;
        std::uint64_t sourceNav;
        std::uint64_t menu;
        std::uint64_t sourceAck;
        bool stealth;
        bool counter;
    };
    const Profile profiles[] = {{"ordinary", 9, 6, 7, false, false},
                                {"counter", 33, 15, 28, false, true},
                                {"stealth", 34, 15, 29, true, false}};
    for (const auto &profile : profiles) {
        for (bool up : {false, true}) {
            const std::string prefix = std::string("spacer-scroll-") + profile.name + (up ? "-up-" : "-down-");
            const auto beforeBytes = ReadPacket(directory, prefix + "before.json");
            const auto afterBytes = ReadPacket(directory, prefix + "after.json");
            const auto before = ParsePacket(beforeBytes);
            const auto after = ParsePacket(afterBytes);
            const auto &source = *before.snapshot;
            const auto &target = *after.snapshot;
            const auto fromNav = profile.sourceNav + (up ? 1 : 0);
            const auto fromAck = profile.sourceAck + (up ? 1 : 0);
            Check(source.navigationRevision == fromNav && target.navigationRevision == fromNav + 1 &&
                      before.lastSequence == fromAck && after.lastSequence == fromAck + 1 &&
                      source.menuRevision == profile.menu && target.menuRevision == profile.menu &&
                      source.selectedIndex == (up ? 3u : 1u) && target.selectedIndex == (up ? 1u : 3u),
                  "Real spacer controller ACK, selection or independent epochs changed");
            Check(source.isOpen && target.isOpen && !source.isRootMenu && !target.isRootMenu &&
                      source.context == "PREVIEW" && target.context == "PREVIEW" &&
                      source.rows.size() == 16 && target.rows.size() == 16 && source.rows[2].isSpacer &&
                      target.rows[2].isSpacer && source.rows[2].label.empty() && source.rows[2].value.empty() &&
                      !source.rows[2].enabled && !source.rows[2].openingAction &&
                      source.rows[1].label == "Bind Slot" && source.rows[3].openingAction == OpeningAction::ActionSlot1,
                  "Actual logical row identity or strict blank topology changed");
            Check(source.mainOpeningControls == MainOpeningControls{1, {OpeningAction::Aim, OpeningAction::Melee}} &&
                      target.mainOpeningControls == source.mainOpeningControls &&
                      source.appearance == target.appearance && source.appearance.stealthUI == profile.stealth &&
                      source.appearance.optionCounter == profile.counter &&
                      source.appearance.booleanDisplay == (profile.stealth ? "Text" : "Boxes") &&
                      source.appearance.scrollAnimationMilliseconds == 120 && source.accent == "#3998FE" &&
                      !source.spacerScrollReceipt && !target.spacerScrollReceipt,
                  "Actual policy, appearance or legacy parser authority changed");
            SpacerScrollReceiptTracker tracker;
            const auto direction = up ? SpacerScrollDirection::Up : SpacerScrollDirection::Down;
            Check(tracker.Capture(source, direction, after.lastSequence, 100), "Real source capture failed");
            const SpacerScrollObservation oldSource{&tracker, 116};
            Check(!ParsePacket(beforeBytes, &oldSource).snapshot->spacerScrollReceipt,
                  "Real pending source ACK published speculative history");
            const SpacerScrollObservation observed{&tracker, 120};
            const auto promoted = ParsePacket(afterBytes, &observed);
            const auto &receipt = promoted.snapshot->spacerScrollReceipt;
            const auto branch = !profile.stealth ? SpacerScrollBranch::StableWindow
                                : up ? SpacerScrollBranch::InitialWindowShiftThenRebuild
                                     : SpacerScrollBranch::FinalWindowShift;
            Check(receipt && receipt->sequence == after.lastSequence && receipt->plan.branch == branch &&
                      receipt->plan.capacity == (profile.stealth ? 5u : profile.counter ? 8u : 10u) &&
                      receipt->plan.fromIndex == source.selectedIndex && receipt->plan.targetIndex == target.selectedIndex &&
                      receipt->issuedMilliseconds == 100 && receipt->observedMilliseconds == 120,
                  "Actual raw parser ACK did not promote the correct private source branch");
            // A larger raw ACK is a drop or another command, even when final selection happens to match
            Check(tracker.Capture(source, direction, after.lastSequence, 100), "Real recapture failed");
            auto jumped = Json::parse(afterBytes);
            jumped["lastSequence"] = after.lastSequence + 1;
            Check(!ParsePacket(jumped.dump(), &observed).snapshot->spacerScrollReceipt,
                  "An ACK jump fabricated real-controller animation history");
        }
    }
}
} // namespace iw::overlay::tests
