#include "CallbackFrameTests.h"
#include "../../../native/render/ScrollRows.h"
#include "../../../native/render/InstructionPanel.h"
#include <stdexcept>

namespace iw::overlay::tests {
void AddCallbackFrameChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Callback presentation pauses controls and rejects foreign frame scopes without repainting game results", [] {
        const auto require = [](bool condition, const char* message) {
            if (!condition) throw std::runtime_error(message);
        };
        MenuSnapshot snapshot;
        snapshot.isOpen = true; snapshot.menuRevision = 6; snapshot.presentationFocusRevision = 8;
        snapshot.rows = {{"Godmode", "UNKNOWN", false}, {"Second", "OFF", false}};
        for (bool plain : {false, true}) {
            snapshot.menuOptionCallback = MenuOptionCallback{7,
                plain ? MenuOptionCallbackKind::Plain : MenuOptionCallbackKind::Boolean,
                plain ? MenuOptionCallbackPhase::Cooldown : MenuOptionCallbackPhase::Pending};
            const auto request = MakeScrollRequest(snapshot, 640, 480);
            const auto hud = layout::MakeHud({640, 480, 2, 0, 260, 3, false, 0, snapshot.appearance});
            const auto frame = StaticScrollFrame(request);
            require(!request.active && request.menuOptionCallback == snapshot.menuOptionCallback
                && MatchesScrollFrame(frame, hud, request), "Callback was not an owned paused presentation");
            const auto help = MakeInstructionContent(snapshot);
            require(help.LineCount() == 3 && help.Line(1) == "Menu controls paused",
                "Callback displayed active ordinary control hints");
            for (int change = 0; change < 4; ++change) {
                auto stale = frame;
                if (change == 0) ++stale.menuOptionCallback->revision;
                if (change == 1) stale.menuOptionCallback->phase = plain ? MenuOptionCallbackPhase::Pending : MenuOptionCallbackPhase::Cooldown;
                if (change == 2) stale.menuOptionCallback->kind = plain ? MenuOptionCallbackKind::Boolean : MenuOptionCallbackKind::Plain;
                if (change == 3) stale.menuOptionCallback.reset();
                require(!MatchesScrollFrame(stale, hud, request), "A stale callback frame borrowed current paint scope");
            }
            // Direct frame callers cannot bypass the same pause and descriptor bounds
            auto invalid = request; invalid.active = true;
            require(!MatchesScrollFrame(StaticScrollFrame(invalid), hud, invalid),
                "A callback admitted active cursor interpolation");
            invalid = request; invalid.menuOptionCallback->revision = 0;
            require(!MatchesScrollFrame(StaticScrollFrame(invalid), hud, invalid),
                "A zero callback revision entered current presentation");
            require(snapshot.rows[0].value == "UNKNOWN" && snapshot.rows[1].value == "OFF",
                "Callback presentation fabricated a game result");
        }
    });
}
}
