#include "MenuChromeTests.h"
#include "../TestSupport.h"
#include <limits>
#include <string_view>

namespace iw::overlay::tests::chrome {
void OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs() {
    for (bool open : {false, true}) {
        auto packet = StateJson();
        packet["isOpen"] = open;
        packet["navigationRevision"] = 7;
        packet["menuRevision"] = 6;
        Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuChromeRevision,
            "Legacy invented a chrome lifetime");
        packet["menuChromeRevision"] = nullptr;
        Check(!runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot->menuChromeRevision,
            "Null legacy chrome invented a lifetime");
        // Creation identity remains owned even when a later packet changes its scalar
        for (std::uint64_t revision : {std::uint64_t{1}, std::numeric_limits<std::uint64_t>::max()}) {
            packet["menuChromeRevision"] = revision;
            const auto state = runtime::ParseState(packet.dump(), GetCurrentProcessId()).snapshot;
            packet["menuChromeRevision"] = 2;
            Check(state->menuChromeRevision == revision && state->navigationRevision == 7 && state->menuRevision == 6
                && state->rows[0].value == "OFF" && runtime::InputModeForSnapshot(*state) == runtime::InputMode::Menu,
                "Chrome creation changed data, input mode or owned epoch");
        }
    }
}

void RejectsMalformedChromeRevisionsAndDecodedDuplicates() {
    for (const Json& invalid : {Json(0), Json(-1), Json(1.0), Json(true), Json("1"), Json::array(), Json::object()}) {
        auto packet = StateJson(); packet["menuChromeRevision"] = invalid;
        Reject([&] { runtime::ParseState(packet.dump(), GetCurrentProcessId()); });
    }
    auto packet = StateJson(); packet["menuChromeRevision"] = 1;
    const std::string field = "\"menuChromeRevision\":1";
    // Keep literal JSON escapes so duplicate decoded names cross the real parser
    for (const auto* duplicate : {"\"menuChromeRevision\":2,", "\"menuChrome\\u0052evision\":2,"}) {
        auto bytes = packet.dump(); const auto start = bytes.find(field);
        Check(start != std::string::npos, "Chrome duplicate fixture lost its field");
        bytes.insert(start, duplicate);
        if (std::string_view(duplicate).find("\\u") != std::string_view::npos)
            Check(bytes.find("\\u0052") != std::string::npos, "Duplicate fixture lost the JSON escape");
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
    auto bytes = packet.dump(); const auto start = bytes.find(field);
    bytes.replace(start, field.size(), "\"menuChromeRevision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
}
