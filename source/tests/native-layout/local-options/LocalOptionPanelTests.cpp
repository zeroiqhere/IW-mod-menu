#include "LocalOptionPanelTests.h"
#include "../../../native/render/InstructionPanel.h"
#include <stdexcept>

namespace iw::overlay::tests {
void AddLocalOptionPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Local option waits show plain paused help for both callback kinds", [] {
        MenuSnapshot state;
        state.isOpen = true;
        state.rows = {{"Local option", "ON", true}};
        for (const auto kind : {MenuOptionCallbackKind::Boolean, MenuOptionCallbackKind::Plain}) {
            // Both waits describe the same paused controls without exposing implementation terms
            state.menuOptionCallback = MenuOptionCallback{1, kind, MenuOptionCallbackPhase::Cooldown};
            const auto help = MakeInstructionContent(state);
            if (help.LineCount() != 3 || help.Line(0) != "Updating option"
                || help.Line(1) != "Menu controls paused" || help.Line(2) != "Please wait")
                throw std::runtime_error("Local option footer does not use the plain paused wording");
            if (state.rows[0].value != "ON" || !state.rows[0].enabled)
                throw std::runtime_error("Footer wording changed current option state");
        }
    });
}
}
