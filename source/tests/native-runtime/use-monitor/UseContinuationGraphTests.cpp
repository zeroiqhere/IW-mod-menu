#include "UseContinuationGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::useGraph {
namespace {
void Begin(Router& router, const runtime::ParsedState& before, std::uint64_t sequence) {
    Check(!router.Poll(before, 0), "Pre-admission frame emitted an unpressed command");
    router.held = {70};
    Check(router.Poll(before, 0) == runtime::Input::Select, "Actual pre-admission frame could not dispatch Use");
    router.input.CaptureUseDispatch(sequence);
}
void ActualPlainCallbacksResumeTwoConsecutiveHeldIterations(const std::filesystem::path& directory) {
    Router router; Begin(router, Read(directory / "plain-before0.json"), 13);
    Check(!router.Poll(Read(directory / "plain-first0.json"),0), "Plain callback resumed at admission");
    Check(!router.Poll(Read(directory / "plain-first249.json"),249), "Plain callback resumed before250ms");
    Check(router.Poll(Read(directory / "plain-first250.json"),250) == runtime::Input::Select,
        "Actual plain retirement did not resume held Use");
    router.input.CaptureUseDispatch(14);
    Check(!router.Poll(Read(directory / "plain-second250.json"),250), "Second plain callback caused a same-frame burst");
    Check(!router.Poll(Read(directory / "plain-second499.json"),499), "Second plain callback resumed early");
    Check(router.Poll(Read(directory / "plain-second500.json"),500) == runtime::Input::Select,
        "Second actual callback did not resume held Use");
    router.held.clear();
    Check(!router.Poll(Read(directory / "plain-release750.json"),750), "Release replayed a completed plain invocation");
}
void ActualBooleanCallbacksKeepBothRedrawsAndIndependentWaits(const std::filesystem::path& directory) {
    Router router; Begin(router, Read(directory / "boolean-before0.json"), 11);
    const std::array first{std::pair{"boolean-first0.json",0u}, std::pair{"boolean-first179.json",179u},
        std::pair{"boolean-first180.json",180u}, std::pair{"boolean-first429.json",429u}};
    for (const auto& [name, now] : first) Check(!router.Poll(Read(directory/name),now), "First Boolean redraw became a repeat");
    Check(router.Poll(Read(directory / "boolean-first430.json"),430) == runtime::Input::Select,
        "Actual Boolean430ms retirement lost held Use");
    router.input.CaptureUseDispatch(12);
    const std::array second{std::pair{"boolean-second430.json",430u}, std::pair{"boolean-second609.json",609u},
        std::pair{"boolean-second610.json",610u}, std::pair{"boolean-second859.json",859u}};
    for (const auto& [name, now] : second) Check(!router.Poll(Read(directory/name),now), "Second Boolean redraw bypassed its own wait");
    Check(router.Poll(Read(directory / "boolean-second860.json"),860) == runtime::Input::Select,
        "Second Boolean callback reused the first wait or lost its held iteration");
    router.held.clear(); Check(!router.Poll(Read(directory / "boolean-release1000.json"),1000), "Released Boolean Use repeated");
}
void ActualInstructionRedrawsKeepOriginalUseWithoutTransferringOwnership(const std::filesystem::path& directory) {
    Router router; Begin(router, Read(directory / "instructions-before0.json"), 10);
    Check(!router.Poll(Read(directory / "instructions-first0.json"),0), "Immediate instruction redraw resumed Use");
    Check(!router.Poll(Read(directory / "instructions-first180.json"),180), "Scheduled instruction redraw resumed Use");
    Check(router.Poll(Read(directory / "instructions-first430.json"),430) == runtime::Input::Select,
        "Immediate and scheduled instruction redraws lost the original receipt");
    router.input.CaptureUseDispatch(11);
    Check(!router.Poll(Read(directory / "instructions-second430.json"),430)
        && !router.Poll(Read(directory / "instructions-second610.json"),610), "Second instruction callback did not own its wait");
    Check(router.Poll(Read(directory / "instructions-second860.json"),860) == runtime::Input::Select,
        "Second instruction callback could not resume held Use");
}
void ActualQueuedAckCannotRewriteTheCapturedUseSequence(const std::filesystem::path& directory) {
    Router router; Begin(router, Read(directory / "queued-before0.json"),4);
    Check(!router.Poll(Read(directory / "queued-intent0.json"),0), "Busy original ACK admitted another Use");
    Check(!router.Poll(Read(directory / "queued-later-ack0.json"),1), "Foreign ACK rearmed original held Use");
    Check(!router.Poll(Read(directory / "queued-start0.json"),2), "Queued start transferred the later ACK to the old receipt");
    Check(!router.Poll(Read(directory / "queued-expired-busy430.json"),430), "Busy expiry admitted Use");
    Check(!router.Poll(Read(directory / "queued-complete430.json"),431), "Completion borrowed a receipt after a foreign ACK");
}
void ActualReplacementPacketsWithdrawEvenAtTheSameNavigation(const std::filesystem::path& directory) {
    for (const char* prefix : {"peer", "cohort"}) {
        const auto ready = Read(directory / (std::string(prefix)+"-ready430.json"));
        const auto replaced = Read(directory / (std::string(prefix)+"-replaced430.json"));
        Check(ready.snapshot->navigationRevision == replaced.snapshot->navigationRevision
            && ready.lastSequence == replaced.lastSequence && ready.snapshot->menuUseContinuation
            && !replaced.snapshot->menuUseContinuation, "Same-navigation target change retained private action ownership");
    }
    // These graphs begin after admission and prove withdrawal, rather than inventing a native predecessor
    for (const char* name : {"navigation-moved430.json", "owner-replaced0.json", "focus-hidden0.json", "focus-return430.json"})
        Check(!Read(directory/name).snapshot->menuUseContinuation, "Canceled source visit retained a resumable receipt");
    const auto before = Read(directory/"rollback-before100.json"), recovered = Read(directory/"rollback-recovered10.json"),
        ready = Read(directory/"rollback-ready440.json");
    Check(before.snapshot->menuOptionCallback && recovered.snapshot->menuOptionCallback && !ready.snapshot->menuOptionCallback
        && before.snapshot->menuUseContinuation == ready.snapshot->menuUseContinuation,
        "Backend clock recovery fabricated cancellation or early callback retirement");
}
}
}
int main(int argc, char** argv) {
    if (argc != 2) return 2;
    using namespace iw::overlay::tests::useGraph;
    const std::array tests{
        std::pair{"ActualInventoryOwnsExactReceiptsAndBothReaders", ActualInventoryOwnsExactReceiptsAndBothReaders},
        std::pair{"ActualPlainCallbacksResumeTwoConsecutiveHeldIterations", ActualPlainCallbacksResumeTwoConsecutiveHeldIterations},
        std::pair{"ActualBooleanCallbacksKeepBothRedrawsAndIndependentWaits", ActualBooleanCallbacksKeepBothRedrawsAndIndependentWaits},
        std::pair{"ActualInstructionRedrawsKeepOriginalUseWithoutTransferringOwnership", ActualInstructionRedrawsKeepOriginalUseWithoutTransferringOwnership},
        std::pair{"ActualQueuedAckCannotRewriteTheCapturedUseSequence", ActualQueuedAckCannotRewriteTheCapturedUseSequence},
        std::pair{"ActualReplacementPacketsWithdrawEvenAtTheSameNavigation", ActualReplacementPacketsWithdrawEvenAtTheSameNavigation}};
    int failures = 0;
    for (const auto& [name, run] : tests) {
        try { run(argv[1]); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failures; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << tests.size()-failures << '/' << tests.size() << " actual Use graph checks passed\n";
    return failures ? 1 : 0;
}
