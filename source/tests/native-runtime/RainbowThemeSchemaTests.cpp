#include "TestSupport.h"
#include <array>
#include <string>

namespace iw::overlay::tests {
void RainbowModeDefaultsAndBooleanChoicesPreserveOwnedState() {
    auto source = StateJson();
    Check(!runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->smoothRainbowTheme,
        "A legacy packet enabled rainbow mode");
    for (bool open : {false, true}) {
        for (bool enabled : {false, true}) {
            source["isOpen"] = open;
            source["smoothRainbowTheme"] = enabled;
            source["navigationRevision"] = 47;
            const auto snapshot = runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot;
            Check(snapshot->smoothRainbowTheme == enabled, "The committed rainbow mode was ignored");
            // Mode selection cannot change the static fallback, feature rows or navigation epoch
            Check(snapshot->accent == "#3998FE" && snapshot->rows.size() == 1
                && snapshot->rows[0].value == "OFF" && snapshot->navigationRevision == 47,
                "Rainbow mode changed unrelated committed state");
            source["smoothRainbowTheme"] = !enabled;
            Check(snapshot->smoothRainbowTheme == enabled, "The snapshot retained mutable theme JSON");
        }
    }
}

void RainbowModeRejectsEverySuppliedNonBooleanIncludingClosedMenus() {
    const std::array<Json, 8> invalid{nullptr, "true", "false", 0, 1, 1.0, Json::array(), Json::object()};
    for (bool open : {false, true}) {
        for (const auto& value : invalid) {
            auto source = StateJson();
            source["isOpen"] = open;
            source["smoothRainbowTheme"] = value;
            // Closed presentation does not weaken the private packet contract
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
    }
}

void RainbowModeRejectsPlainAndEscapedDuplicateProperties() {
    for (const auto* duplicate : {"smoothRainbowTheme", "smoothRainbow\\u0054heme"}) {
        auto source = StateJson();
        std::string bytes = source.dump();
        // The bounded parser compares decoded names before a later value could overwrite one
        bytes.insert(1, std::string("\"smoothRainbowTheme\":true,\"") + duplicate + "\":false,");
        Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    }
}

void RainbowModeStillRequiresAnExactStaticAccent() {
    for (bool enabled : {false, true}) {
        auto source = StateJson();
        source["smoothRainbowTheme"] = enabled;
        // Every mode retains the old required hex-color boundary
        for (const Json& invalid : {Json(nullptr), Json(true), Json(123), Json("3998FE"),
            Json("#3998F"), Json("#3998FE00"), Json("#zzzzzz")}) {
            source["accent"] = invalid;
            Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        }
        source.erase("accent");
        Reject([&] { runtime::ParseState(source.dump(), GetCurrentProcessId()); });
        source["accent"] = "#aBcDeF";
        Check(runtime::ParseState(source.dump(), GetCurrentProcessId()).snapshot->accent == "#aBcDeF",
            "The theme flag changed valid legacy accent spelling");
    }
}
}
