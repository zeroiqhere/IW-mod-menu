#include "ClosedLevelOpeningGraphSupport.h"

namespace iw::overlay::tests::closedLevelGraph {
namespace {
void OrdinaryUseAfterReady(Router& router, const runtime::ParsedState& state, std::uint64_t now) {
    // The held level is evaluated at readiness without manufacturing a second press
    router.held = {70};
    Check(router.Poll(state, now) == runtime::Input::Select, "Actual completed opening still locks ordinary Use");
}
}
void ActualMainBackResumesHeldOpeningAtTwoFiftyAndCreatesChromeAtFifty(const std::filesystem::path& directory) {
    Router router;
    const auto before = Read(directory / "main-before0.json");
    Check(!router.Poll(before, 0), "Unpressed Main emitted input");
    router.held = {86};
    Check(!router.Poll(before, 49), "Main root knife escaped the idle monitor cycle");
    Check(router.Poll(before, 50) == runtime::Input::Back, "Main root knife lost its actual Back");
    router.input.CaptureMonitorDispatch(1002);
    // Back owns its full pause even though the resulting menu is already closed
    Check(!router.Poll(Read(directory / "main-back0.json"), 50), "Closed Main replayed its Back");
    router.held = {2, 86};
    Check(!router.Poll(Read(directory / "main-back249.json"), 299), "Held Main escaped the final Back pause");
    const auto ready = Read(directory / "main-back250.json");
    Check(router.Poll(ready, 300) == runtime::Input::OpenMain, "Held Main added a fresh edge or extra fifty after Back250");
    const auto command = OpeningCommand(ready, runtime::Input::OpenMain, 1003);
    Check(command.at("navigationRevision") == 2 && command.at("openingControlsRevision") == 1
        && command.at("localAdmissionRevision") == 10, "Main reopening borrowed original root epochs");
    router.pending = true;
    Check(!router.Poll(Read(directory / "main-pending250.json"), 300), "Pending Main opening duplicated its command");
    router.pending = false;
    // Main's draw phase and later input cooldown are separate source waits
    Check(!router.Poll(Read(directory / "main-rebuild250.json"), 300), "Main rebuild admitted ordinary input");
    Check(!router.Poll(Read(directory / "main-rebuild299.json"), 349), "Main created or unlocked before its fifty boundary");
    Check(!router.Poll(Read(directory / "main-drawn300.json"), 350), "Main draw ended its separate caller wait");
    router.held = {70};
    Check(!router.Poll(Read(directory / "main-wait849.json"), 899), "Main cooldown ended at599 since invocation");
    OrdinaryUseAfterReady(router, Read(directory / "main-ready850.json"), 900);
}
void ActualLivingQuickCreatesImmediatelyAndKeepsItsOwnFiveFiftyWait(const std::filesystem::path& directory) {
    // Quick starts from its configured saved chord and has no Main rebuild phase
    Router router;
    router.held = {2, 69};
    const auto before = Read(directory / "quick-before0.json");
    Check(router.Poll(before, 0) == runtime::Input::OpenQuick, "Current held Quick required another edge or a native fifty delay");
    const auto command = OpeningCommand(before, runtime::Input::OpenQuick, 1001);
    Check(command.at("navigationRevision") == 0 && command.at("localAdmissionRevision") == 10,
        "Quick command did not use its actual closed predecessor");
    router.pending = true;
    Check(!router.Poll(Read(directory / "quick-pending0.json"), 0), "Quick ACK delay repeated opening");
    router.pending = false;
    Check(!router.Poll(Read(directory / "quick-drawn0.json"), 0), "Immediate Quick draw granted ordinary input");
    router.held = {70};
    Check(!router.Poll(Read(directory / "quick-wait549.json"), 549), "Living Quick ended before550");
    OrdinaryUseAfterReady(router, Read(directory / "quick-ready550.json"), 550);
}
void ActualDeadAimJumpReopensOnlyAfterItsOwnBackWait(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 32};
    const auto before = Read(directory / "dead-before0.json");
    Check(router.Poll(before, 0) == runtime::Input::OpenQuick, "Dead opening ignored fixed saved Aim and Jump");
    OpeningCommand(before, runtime::Input::OpenQuick, 1001);
    router.pending = true;
    Check(!router.Poll(Read(directory / "dead-pending0.json"), 0), "Pending dead Quick duplicated opening");
    router.pending = false;
    router.held = {32};
    Check(!router.Poll(Read(directory / "dead-drawn0.json"), 0), "Dead Quick draw skipped550");
    Check(!router.Poll(Read(directory / "dead-wait549.json"), 549), "Dead Quick ended549");
    const auto ready = Read(directory / "dead-ready550.json");
    Check(router.Poll(ready, 550) == runtime::Input::JumpBack, "Ready dead Quick lost its actual Jump branch");
    router.input.CaptureMonitorDispatch(1002);
    // A second dead opening must wait for its own Jump Back, not the first opening
    Check(!router.Poll(Read(directory / "dead-jump550.json"), 550), "Closed dead Quick replayed Jump");
    router.held = {2, 32};
    Check(!router.Poll(Read(directory / "dead-jump799.json"), 799), "Dead reopening borrowed an incomplete Back wait");
    const auto closed = Read(directory / "dead-jump800.json");
    Check(router.Poll(closed, 800) == runtime::Input::OpenQuick, "Dead held Aim+Jump failed after its own250 pause");
    const auto command = OpeningCommand(closed, runtime::Input::OpenQuick, 1003);
    Check(command.at("navigationRevision") == 2 && command.at("localAdmissionRevision") == 11,
        "Dead reopening reused its first opening scope");
    router.pending = true;
    Check(!router.Poll(Read(directory / "dead-reopen-pending800.json"), 800), "Dead reopening duplicated while pending");
    router.pending = false;
    Check(!router.Poll(Read(directory / "dead-reopened800.json"), 800), "Reopened dead Quick borrowed prior550 readiness");
}
}
