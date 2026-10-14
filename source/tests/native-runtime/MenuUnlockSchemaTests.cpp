#include "TestSupport.h"
#include <set>

namespace iw::overlay::tests {
namespace {
Json ParentState() {
    auto value = StateJson();
    value["isOpen"] = true;
    value["isRootMenu"] = false;
    return value;
}
}
void MissingAndNullUnlockPhasesPreserveLegacyMenus() {
    auto value = ParentState();
    Check(!runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot->menuUnlock,
        "An older producer must retain ordinary menu controls");
    value["menuUnlock"] = nullptr;
    Check(!runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot->menuUnlock,
        "An expired pause must restore ordinary menu controls");
}
void ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues() {
    auto value = ParentState(); value["menuUnlock"] = "rebuild";
    const auto hidden = runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot;
    Check(hidden->menuUnlock == MenuUnlockPhase::Rebuild && hidden->rows[0].value == "OFF"
        && hidden->isOpen && !hidden->widthEditor, "Panel recreation must not change feature state or logical open state");
    value["menuUnlock"] = "pause";
    const auto paused = runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot;
    Check(paused->menuUnlock == MenuUnlockPhase::Pause && hidden->menuUnlock == MenuUnlockPhase::Rebuild,
        "The visible pause phase must not mutate an older immutable snapshot");
}
void UnsupportedUnlockNamesAndTypesCannotLockMenuControls() {
    for (const Json& phase : {Json(""), Json("Rebuild"), Json("waiting"), Json(0), Json(1.0),
        Json(true), Json::array(), Json::object()}) {
        auto value = ParentState(); value["menuUnlock"] = phase;
        Reject([&] { runtime::ParseState(value.dump(), GetCurrentProcessId()); });
    }
}
void UnlockPhasesRequireAnOpenNonEditorChildContext() {
    for (const char* phase : {"rebuild", "pause"}) {
        auto closed = ParentState(); closed["menuUnlock"] = phase; closed["isOpen"] = false;
        Reject([&] { runtime::ParseState(closed.dump(), GetCurrentProcessId()); });
        auto root = ParentState(); root["menuUnlock"] = phase; root["isRootMenu"] = true;
        Reject([&] { runtime::ParseState(root.dump(), GetCurrentProcessId()); });
        auto editor = ParentState(); editor["menuUnlock"] = phase;
        editor["rows"] = Json::array(); editor["widthEditor"] = {{"step", 1}};
        Reject([&] { runtime::ParseState(editor.dump(), GetCurrentProcessId()); });
    }
}
void ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume() {
    auto value = ParentState(); value["menuUnlock"] = "pause";
    const auto paused = runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot;
    value["menuUnlock"] = nullptr;
    const auto resumed = runtime::ParseState(value.dump(), GetCurrentProcessId()).snapshot;
    runtime::NativeInputRouter router(runtime::ParseBindings(BindingsJson().dump()));
    std::set<int> held;
    const auto poll = [&](const MenuSnapshot& model) {
        return router.Poll(true, model.isOpen, false, [&](int key) { return held.contains(key); },
            0, model.navigationRevision, model.appearance.quickExit, model.isRootMenu,
            model.menuUnlock ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu);
    };
    poll(*paused); held = {70};
    Check(!poll(*paused), "A parsed pause must suppress selection");
    Check(!poll(*resumed) && !poll(*resumed), "Removing the parsed pause cannot replay a held selection");
    held.clear(); poll(*resumed); held = {70};
    Check(poll(*resumed) == runtime::Input::Select, "A fresh selection must work after the parsed pause ends");
}
}
