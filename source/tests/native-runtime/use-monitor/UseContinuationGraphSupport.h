#pragma once
#include "../TestSupport.h"
#include "ControlLabels.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>
#include <set>

namespace iw::overlay::tests::useGraph {
inline runtime::Bindings Bindings() {
    // These groups describe the exported controller fixture, rather than a guessed game binding
    return {{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}, {16}, {17}, {32}};
}
inline std::string Bytes(const std::filesystem::path& path) {
    Check(std::filesystem::file_size(path) <= runtime::MaximumFileBytes, "Use packet exceeds the bounded reader");
    std::ifstream stream(path);
    Check(static_cast<bool>(stream), "Actual Use packet is missing");
    return {std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>()};
}
inline auto Read(const std::filesystem::path& path) {
    const auto labels = runtime::DescribeOpeningActions(Bindings());
    return runtime::ParseState(Bytes(path), 42, nullptr, nullptr, nullptr, nullptr, nullptr,
        std::nullopt, nullptr, nullptr, &labels);
}
struct Router {
    runtime::NativeInputRouter input{Bindings()};
    std::set<int> held;
    bool pending = false;
    std::optional<runtime::Input> Poll(const runtime::ParsedState& state, std::uint64_t now) {
        const auto& frame = *state.snapshot;
        // All three causal fields come from one actual immutable packet
        runtime::UseMonitorObservation use{frame.menuUseContinuation ? &*frame.menuUseContinuation : nullptr,
            frame.menuOptionCallback ? &*frame.menuOptionCallback : nullptr, state.lastSequence};
        return input.Poll(true, frame.isOpen, pending, [&](int key) { return held.contains(key); }, now,
            frame.navigationRevision, frame.appearance.quickExit, frame.isRootMenu,
            runtime::InputModeForSnapshot(frame), frame.appearance.scrollAnimationMilliseconds, frame.menuRevision,
            frame.mainOpeningControls ? &*frame.mainOpeningControls : nullptr, 0, 0,
            frame.openingPolicies ? &*frame.openingPolicies : nullptr,
            frame.localMenuEligibility ? &*frame.localMenuEligibility : nullptr, frame.menuSurface,
            frame.menuMonitorWait ? &*frame.menuMonitorWait : nullptr, &use);
    }
};
void ActualInventoryOwnsExactReceiptsAndBothReaders(const std::filesystem::path& directory);
}
