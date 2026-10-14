#include "TestSupport.h"
#include "PresentationFocusTracker.h"
#include <array>
#include <limits>

namespace iw::overlay::tests {
namespace {
Json Appearance() {
    return {{"booleanDisplay", "Boxes"}, {"boolBoxLocation", "Right"}, {"instructionsVisible", true},
        {"optionCounter", false}, {"coloredCursor", true}, {"spotlightCursor", true}, {"largeCursor", false}};
}
}
void ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps() {
    auto source = StateJson();
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.scrollAnimationMilliseconds == 120,
        "Legacy scroll duration changed");
    source["appearance"] = nullptr;
    Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->appearance.scrollAnimationMilliseconds == 120,
        "Null legacy appearance changed the scroll duration");
    source["appearance"] = Appearance();
    for (int duration = 100; duration <= 250; duration += 10) {
        source["appearance"]["scrollAnimationMilliseconds"] = duration;
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        Check(snapshot->appearance.scrollAnimationMilliseconds == duration, "The committed scroll duration was ignored");
        // The immutable appearance cannot retain a mutable packet field
        source["appearance"]["scrollAnimationMilliseconds"] = 120;
        Check(snapshot->appearance.scrollAnimationMilliseconds == duration, "Scroll duration retained mutable JSON");
    }
}
void ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates() {
    const std::array<Json, 12> invalid{nullptr, true, "120", 120.0, -1, 0, 99, 101, 251,
        std::numeric_limits<std::uint64_t>::max(), Json::array(), Json::object()};
    for (bool open : {false, true}) {
        for (const auto& value : invalid) {
            auto source = StateJson(); source["isOpen"] = open; source["appearance"] = Appearance();
            source["appearance"]["scrollAnimationMilliseconds"] = value;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
    auto source = StateJson(); source["appearance"] = Appearance();
    std::string bytes = source.dump();
    const auto start = bytes.find("\"appearance\":{");
    Check(start != std::string::npos, "Duration duplicate fixture lost appearance");
    // Escaped decoded names cannot replace a committed value
    bytes.insert(start + 14, "\"scrollAnimationMilliseconds\":100,\"scrollAnimation\\u004dilliseconds\":250,");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void MenuRevisionPreservesAbsenceAndUnsignedBoundaries() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->menuRevision,
        "Legacy presentation gained a menu revision");
    for (auto revision : {std::uint64_t{0}, std::uint64_t{47}, std::numeric_limits<std::uint64_t>::max()}) {
        source["menuRevision"] = revision; source["navigationRevision"] = 9;
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
        Check(snapshot->menuRevision == revision && snapshot->navigationRevision == 9,
            "Menu identity lost its presence or changed the cursor epoch");
        source["menuRevision"] = 1;
        Check(snapshot->menuRevision == revision, "Menu revision retained mutable JSON");
    }
}
void MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates() {
    for (bool open : {false, true}) {
        for (const Json& revision : {Json(nullptr), Json(true), Json(-1), Json("1"), Json(1.0), Json::array(), Json::object()}) {
            auto source = StateJson(); source["isOpen"] = open; source["menuRevision"] = revision;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
    for (const auto* duplicate : {"menuRevision", "menu\\u0052evision"}) {
        std::string bytes = StateJson().dump();
        bytes.insert(1, std::string("\"menuRevision\":0,\"") + duplicate + "\":1,");
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}
void PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson() {
    auto source = StateJson();
    // Even a plausible session field cannot replace the native observation
    for (const Json& supplied : {Json(99), Json(nullptr), Json("untrusted"), Json(true)}) {
        source["presentationFocusRevision"] = supplied;
        Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->presentationFocusRevision,
            "JSON invented a private focus epoch");
        const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId(), nullptr, nullptr,
            nullptr, nullptr, nullptr, std::uint64_t{7}).snapshot;
        Check(snapshot->presentationFocusRevision == 7, "The parser ignored the native focus observation");
        source["presentationFocusRevision"] = 100;
        Check(snapshot->presentationFocusRevision == 7, "Private focus state retained mutable packet data");
    }
}
void ObservedFocusTransitionsStampAnOwnedPresentationContext() {
    runtime::PresentationFocusTracker tracker;
    Check(tracker.Observe(true) == 0 && tracker.Observe(true) == 0, "Stable focus changed its private epoch");
    Check(tracker.Observe(false) == 1 && tracker.Observe(false) == 1, "Focus loss did not establish one private change");
    const auto returned = tracker.Observe(true);
    Check(returned == 2, "Focus return lost the observed gap");
    const auto snapshot = runtime::ParseState(StateJson().dump(), GetCurrentProcessId(), nullptr, nullptr,
        nullptr, nullptr, nullptr, returned).snapshot;
    Check(snapshot->presentationFocusRevision == 2 && snapshot->rows[0].value == "OFF",
        "The newly parsed context lost focus identity or changed a feature row");
}
void FocusEpochExhaustionPermanentlyFailsClosed() {
    runtime::PresentationFocusTracker tracker(std::numeric_limits<std::uint64_t>::max() - 1);
    tracker.Observe(false);
    Check(tracker.Observe(true) == std::numeric_limits<std::uint64_t>::max(), "The final focus epoch was rejected");
    Reject([&] { tracker.Observe(false); });
    // A caught failure cannot later make the exhausted epoch look usable again
    Reject([&] { tracker.Observe(true); });
}
}
