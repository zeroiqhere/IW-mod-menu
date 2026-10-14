#include "ClosedLevelOpeningGraphSupport.h"

namespace iw::overlay::tests::closedLevelGraph {
void ActualPermissionRecoveryUsesCurrentAuthorityWithoutANewChordEdge(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 69};
    Check(!router.Poll(Read(directory / "permission-before0.json"), 0), "Denied permission opened Quick");
    Check(!router.Poll(Read(directory / "permission-denied0.json"), 1), "Rejected command acknowledgment granted permission");
    const auto recovered = Read(directory / "permission-recovered0.json");
    Check(!router.Poll(recovered, 2), "Current permission bypassed the existing fifty wait");
    Check(router.Poll(recovered, 50) == runtime::Input::OpenQuick, "Current permission recovery required a new chord edge");
    const auto command = OpeningCommand(recovered, runtime::Input::OpenQuick, 1002);
    Check(command.at("localAdmissionRevision") == 12, "Recovered permission reused the denied revision");
    router.pending = true;
    Check(!router.Poll(Read(directory / "permission-pending0.json"), 51), "Permission recovery opened twice");
    router.pending = false;
    Check(!router.Poll(Read(directory / "permission-drawn0.json"), 52), "Recovered Quick skipped its own locked draw");
}
void ActualQuickDisabledKeepsMainPriorityAndDeniesLivingQuick(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 69};
    Check(!router.Poll(Read(directory / "disabled-before0.json"), 0), "Disabled Quick opened from held levels");
    const auto denied = Read(directory / "disabled-denied0.json");
    Check(!router.Poll(denied, 1), "Denied disabled Quick acknowledgment granted readiness");
    // Main keeps priority even when both configured chords are held together
    router.held = {2, 69, 86};
    Check(!router.Poll(denied, 2), "Main chord bypassed the retained source cycle");
    Check(router.Poll(denied, 50) == runtime::Input::OpenMain, "Disable Quick also disabled Main or lost Main priority");
    const auto command = OpeningCommand(denied, runtime::Input::OpenMain, 1024);
    Check(command.at("openingControlsRevision") == 2 && command.at("navigationRevision") == 25,
        "Main did not capture the actual disabled pair epoch");
    router.pending = true;
    Check(!router.Poll(Read(directory / "disabled-main-pending0.json"), 51), "Disabled-pair Main duplicated pending opening");
    router.pending = false;
    Check(!router.Poll(Read(directory / "disabled-main-rebuild0.json"), 52), "Disabled-pair Main skipped rebuild");
    Check(!router.Poll(Read(directory / "disabled-main50.json"), 102), "Disabled-pair Main draw skipped its caller pause");
}
void ActualUnknownAndMissingFactsDenySourceLevelsWithoutLegacyBootstrap(const std::filesystem::path& directory) {
    for (const auto* prefix : {"unknown", "missing"}) {
        Router router;
        router.held = {2, 69, 86, 32};
        Check(!router.Poll(Read(directory / (std::string(prefix) + "-before0.json")), 0),
            "Unknown or missing source facts became living or dead authority");
        Check(!router.Poll(Read(directory / (std::string(prefix) + "-denied0.json")), 10000),
            "A later sample or ACK replaced missing source proof with legacy bootstrap");
    }
}
namespace {
void StaleScope(const std::filesystem::path& directory, const char* prefix, const char* currentName,
                const char* deniedName, const char* changedField, std::uint64_t original,
                runtime::Input actualInput) {
    const auto captured = Read(directory / (std::string(prefix) + "-capture0.json"));
    const auto current = Read(directory / currentName);
    const auto denied = Read(directory / deniedName);
    Router router;
    router.held = actualInput == runtime::Input::OpenMain ? std::set<int>{2, 86} : std::set<int>{2, 69};
    Check(router.Poll(captured, 0) == actualInput, "Original captured opening was not admitted by current source levels");
    const auto command = OpeningCommand(captured, actualInput, denied.lastSequence);
    // The emitted input must match the actual producer command, besides its captured scope
    Check(command.at("input") == runtime::InputName(actualInput),
        "Native stale command differs from the actual producer input");
    Check(command.at(changedField) == original, "Original opening scope was not literal");
    const auto& frame = *current.snapshot;
    const std::uint64_t updated = std::string(changedField) == "navigationRevision" ? frame.navigationRevision
        : std::string(changedField) == "openingControlsRevision" ? *runtime::ObservedOpeningControlsRevision(frame)
        : frame.localMenuEligibility->revision;
    Check(updated != original, "Stale graph did not change the captured admission dimension");
    // The actual producer rejects the old scope without replacing it with the new visible values
    Check(!denied.snapshot->isOpen && !denied.snapshot->localMenuEligibility->blocked
        && denied.snapshot->navigationRevision == frame.navigationRevision && denied.snapshot->menuRevision == frame.menuRevision
        && denied.snapshot->localMenuEligibility == frame.localMenuEligibility && denied.lastSequence > current.lastSequence,
        "Rejected stale opening mutated its current owner or navigation");
    Check(command.at(changedField) != updated,
        "Opening command did not retain its original stale admission dimension");
}
void QueuedReplacement(const std::filesystem::path& directory, const char* prefix) {
    Router router;
    router.held = {2, 69};
    const auto before = Read(directory / (std::string(prefix) + "-before0.json"));
    Check(router.Poll(before, 0) == runtime::Input::OpenQuick, "Queued original opening was not sampled from its actual predecessor");
    const auto original = OpeningCommand(before, runtime::Input::OpenQuick, 1001);
    router.pending = true;
    const auto pending = Read(directory / (std::string(prefix) + "-pending0.json"));
    Check(!router.Poll(pending, 1), "Queued opening repeated before acknowledgment or completion");
    const auto retired = Read(directory / (std::string(prefix) + "-retired0.json"));
    Check(!retired.snapshot->isOpen && !retired.snapshot->localMenuEligibility->blocked
        && !retired.snapshot->menuMonitorContinuation && !retired.snapshot->menuUseContinuation,
        "Canceled pending opening fabricated an ordinary continuation");
    // A new current owner can admit a new intent, never revive the retired command's receipt
    router.pending = false;
    Check(!router.Poll(retired, 2), "Replacement invented an immediate source monitor cycle");
    Check(router.Poll(retired, 50) == runtime::Input::OpenQuick, "Current permitted replacement lost source level opening");
    const auto next = OpeningCommand(retired, runtime::Input::OpenQuick, 1002);
    Check(original.at("localAdmissionRevision") == 10 && next.at("localAdmissionRevision") == 11
        && original.at("sequence") == 1001 && next.at("sequence") == 1002,
        "Retired opening reused old actor scope or sequence");
}
}
void ActualNavigationReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory) {
    StaleScope(directory, "stale-navigation", "stale-navigation-current800.json", "stale-navigation-denied800.json", "navigationRevision", 0, runtime::Input::OpenQuick);
}
void ActualPolicyReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory) {
    StaleScope(directory, "stale-policy", "stale-policy-current1780.json", "stale-policy-denied1780.json", "openingControlsRevision", 1, runtime::Input::OpenMain);
}
void ActualAdmissionReplacementRejectsTheOriginalOpeningScope(const std::filesystem::path& directory) {
    StaleScope(directory, "stale-admission", "stale-admission-current0.json", "stale-admission-denied0.json", "localAdmissionRevision", 10, runtime::Input::OpenQuick);
}
void ActualQueuedFocusLossRetiresThePendingOpenWithoutNativeReplay(const std::filesystem::path& directory) {
    Router router;
    router.held = {2, 69};
    const auto before = Read(directory / "queued-focus-before0.json");
    Check(router.Poll(before, 0) == runtime::Input::OpenQuick, "Original focused opening was not sampled");
    OpeningCommand(before, runtime::Input::OpenQuick, 1001);
    router.pending = true;
    Check(!router.Poll(Read(directory / "queued-focus-pending0.json"), 1), "Focused pending opening repeated");
    router.pending = false;
    router.foreground = false;
    const auto retired = Read(directory / "queued-focus-retired0.json");
    Check(!router.Poll(retired, 2) && !retired.snapshot->isOpen && !retired.snapshot->localMenuEligibility->blocked,
        "Focus-retired pending opening replayed or kept a busy ticket");
    Check(!retired.snapshot->menuMonitorContinuation && !retired.snapshot->menuUseContinuation,
        "Focus retirement fabricated held readiness");
    // Independent host focus samples reuse the closed packet without proving a producer focus return
    router.foreground = true;
    Check(!router.Poll(retired, 3), "Complete hold crossed focus recovery without release");
    router.held.clear();
    Check(!router.Poll(retired, 4), "Focus release itself emitted opening");
    router.held = {2, 69};
    Check(!router.Poll(retired, 5), "Fresh focus hold bypassed its new idle phase");
    Check(router.Poll(retired, 53) == runtime::Input::OpenQuick, "Fresh hold after focus release did not admit a new intent");
}
void ActualQueuedAdmissionReplacementNeverReusesTheOldCommandScope(const std::filesystem::path& directory) {
    QueuedReplacement(directory, "queued-admission");
}
void ActualQueuedOwnerReplacementNeedsItsNewAdmissionScope(const std::filesystem::path& directory) {
    QueuedReplacement(directory, "queued-owner");
}
}
