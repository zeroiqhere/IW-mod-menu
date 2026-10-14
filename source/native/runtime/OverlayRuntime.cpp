#include "../shared/OverlayProtocol.h"
#include "ScrollSpacerCounts.h"
#include "RuntimeStartup.h"
#include "OpeningPolicyScope.h"
#include "NativeInputRouter.h"
#include "ControlLabels.h"
#include "RuntimeSchema.h"
#include "SessionFiles.h"
#include "SnapshotHeartbeat.h"
#include "PresentationFocusTracker.h"

#include <algorithm>
#include <atomic>
#include <mutex>
#include <stdexcept>
#include <thread>

namespace iw::overlay {
namespace {
struct ResidentState {
    std::mutex lifecycle;
    std::atomic<bool> running{false};
    std::atomic<std::shared_ptr<const MenuSnapshot>> snapshot;
    std::thread worker;
};

ResidentState& State() {
    // The DLL stays resident and this state is never destroyed under the loader lock
    static ResidentState* state = new ResidentState();
    return *state;
}

void Publish(std::shared_ptr<const MenuSnapshot> snapshot) {
    State().snapshot.store(std::move(snapshot), std::memory_order_release);
}

bool ForegroundMatches(HWND expectedWindow, DWORD expectedProcessId) {
    HWND current = GetForegroundWindow();
    DWORD processId = 0;
    return current == expectedWindow && IsWindow(current)
        && GetWindowThreadProcessId(current, &processId) != 0 && processId == expectedProcessId;
}

bool PositionControlsAvailable(const runtime::Bindings& bindings) {
    return !bindings.moveUp.empty() && !bindings.moveDown.empty()
        && !bindings.previousChoice.empty() && !bindings.nextChoice.empty();
}

bool MenuPositionControlsAvailable(const runtime::Bindings& bindings) {
    return PositionControlsAvailable(bindings) && !bindings.frag.empty() && !bindings.secondaryOffhand.empty();
}

void Worker(std::shared_ptr<runtime::SessionFiles> files, HWND window, DWORD processId,
    runtime::Bindings bindings, unsigned long long initialSequence, unsigned long long generation,
    std::shared_ptr<const MenuSnapshot> failureSnapshot,
    std::shared_ptr<const MenuSnapshot> staleSnapshot) noexcept {
    try {
        const auto controlHints = runtime::DescribeControls(bindings);
        const auto quickExitHints = runtime::DescribeControls(bindings, true, false);
        const auto rootQuickExitHints = runtime::DescribeControls(bindings, true, true);
        const bool widthControlsAvailable = !bindings.attack.empty()
            && !bindings.previousChoice.empty() && !bindings.nextChoice.empty();
        const auto widthEditorHints = widthControlsAvailable ? runtime::DescribeWidthEditorControls(bindings)
            : std::vector<std::string>{};
        const bool positionControlsAvailable = PositionControlsAvailable(bindings);
        const auto positionHints = positionControlsAvailable ? runtime::DescribeInstructionPositionControls(bindings)
            : std::vector<std::string>{};
        const bool menuPositionControlsAvailable = MenuPositionControlsAvailable(bindings);
        const auto menuPositionHints = menuPositionControlsAvailable ? runtime::DescribeMenuPositionControls(bindings)
            : std::vector<std::string>{};
        const auto creditsHints = runtime::DescribeMenuCreditsControls(bindings);
        const auto openingLabels = runtime::DescribeOpeningActions(bindings);
        runtime::NativeInputRouter router(std::move(bindings));
        runtime::CommandGate commands(initialSequence);
        runtime::SnapshotHeartbeat heartbeat(generation, GetTickCount64());
        runtime::PresentationFocusTracker focus;
        runtime::scroll::SpacerScrollReceiptTracker scrollReceipts;
        bool stale = false;
        ULONGLONG lastStatusPoll = 0;
        std::string publishedStatus;
        while (State().running.load(std::memory_order_acquire)) {
            // One focus observation belongs to both the new snapshot and this input poll
            const bool foregroundAllowed = ForegroundMatches(window, processId);
            const auto focusRevision = focus.Observe(foregroundAllowed);
            if (!foregroundAllowed) scrollReceipts.Reset();
            ULONGLONG now = GetTickCount64();
            if (publishedStatus.empty() || now - lastStatusPoll >= 100) {
                // Renderer diagnostics are cached atomics; all serialization and IO stays here
                auto status = runtime::RendererStatusJson(processId, ReadRendererStatus());
                if (status != publishedStatus) {
                    files->WriteRendererStatus(status);
                    publishedStatus = std::move(status);
                }
                lastStatusPoll = now;
            }
            const auto stateBytes = files->ReadState();
            // Timestamp the read report after IO using the same clock as presentation
            const runtime::scroll::SpacerScrollObservation scrollObservation{&scrollReceipts, GetTickCount64()};
            auto state = runtime::ParseState(stateBytes, processId, &controlHints,
                &quickExitHints, &rootQuickExitHints, &widthEditorHints,
                positionControlsAvailable ? &positionHints : nullptr, focusRevision,
                menuPositionControlsAvailable ? &menuPositionHints : nullptr, &creditsHints, &openingLabels, &scrollObservation);
            if (state.snapshot->instructionPositionEditor && !positionControlsAvailable) {
                throw std::runtime_error("Position editing requires all four saved action slots");
            }
            if (state.snapshot->menuPositionEditor && !menuPositionControlsAvailable) {
                throw std::runtime_error("Menu positioning requires four saved slots and both grenade actions");
            }
            commands.ObserveAck(state.lastSequence);
            if (!heartbeat.Observe(state.generation, GetTickCount64())) {
                // Keep reading for owner recovery, while closing the HUD and suppressing input
                Publish(staleSnapshot);
                router.Reset();
                scrollReceipts.Reset();
                stale = true;
                Sleep(20);
                continue;
            }
            if (stale) {
                router.Reset();
                scrollReceipts.Reset();
                stale = false;
            }
            Publish(state.snapshot);
            const auto spacers = runtime::CountScrollSpacers(*state.snapshot);
            const runtime::UseMonitorObservation useObservation{state.snapshot->menuUseContinuation ? &*state.snapshot->menuUseContinuation : nullptr,
                state.snapshot->menuOptionCallback ? &*state.snapshot->menuOptionCallback : nullptr, state.lastSequence};
            // Both causal observations come from this same immutable controller packet
            const runtime::MonitorContinuationObservation monitorObservation{
                state.snapshot->menuMonitorContinuation ? &*state.snapshot->menuMonitorContinuation : nullptr,
                state.lastSequence};
            const auto input = router.Poll(foregroundAllowed, state.snapshot->isOpen,
                commands.Pending(), [](int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; },
                runtime::MonotonicMilliseconds(), state.snapshot->navigationRevision,
                state.snapshot->appearance.quickExit, state.snapshot->isRootMenu,
                runtime::InputModeForSnapshot(*state.snapshot), state.snapshot->appearance.scrollAnimationMilliseconds,
                state.snapshot->menuRevision, state.snapshot->mainOpeningControls ? &*state.snapshot->mainOpeningControls : nullptr, spacers.up, spacers.down,
                state.snapshot->openingPolicies ? &*state.snapshot->openingPolicies : nullptr,
                state.snapshot->localMenuEligibility ? &*state.snapshot->localMenuEligibility : nullptr,
                state.snapshot->menuSurface, state.snapshot->menuMonitorWait ? &*state.snapshot->menuMonitorWait : nullptr,
                &useObservation, &monitorObservation,
                state.snapshot->localMenuEligibility ? ClosedOpeningPolling::SourceLevels : ClosedOpeningPolling::FreshEdges);
            if (input) {
                auto sequence = commands.NextSequence();
                // Local close and editor changes belong to the navigation that produced them
                const bool mechanicalInput = *input == runtime::Input::PreviousChoice || *input == runtime::Input::NextChoice
                    || *input == runtime::Input::Back || *input == runtime::Input::Close || *input == runtime::Input::JumpBack;
                const auto* eligibility = state.snapshot->localMenuEligibility ? &*state.snapshot->localMenuEligibility : nullptr;
                const bool causalMechanical = mechanicalInput && state.snapshot->isOpen
                    && runtime::InputModeForSnapshot(*state.snapshot) == runtime::InputMode::Menu && eligibility
                    && eligibility->hasMenu == true && eligibility->controlsDisabled == false && !eligibility->blocked
                    && (eligibility->life == PlayerLifeState::Alive || eligibility->life == PlayerLifeState::Dead);
                const bool localIntent = causalMechanical || *input == runtime::Input::OpenMain
                    || *input == runtime::Input::OpenQuick || *input == runtime::Input::JumpBack;
                files->WriteCommand(runtime::CommandJson(processId, sequence, *input,
                    state.snapshot->navigationRevision, runtime::ObservedOpeningControlsRevision(*state.snapshot),
                    localIntent && eligibility ? std::optional<std::uint64_t>(eligibility->revision) : std::nullopt,
                    causalMechanical ? std::optional<std::uint64_t>(state.snapshot->menuRevision) : std::nullopt));
                commands.MarkSent(sequence);
                // A failed file write cannot create the input receipt for held Use
                if (*input == runtime::Input::Select) router.CaptureUseDispatch(sequence);
                // Mechanical scope is captured only after the command file write succeeds
                if (causalMechanical) router.CaptureMonitorDispatch(sequence);
                // Only a successfully written command may create private presentation history
                if (*input == runtime::Input::Up || *input == runtime::Input::Down) {
                    scrollReceipts.Capture(*state.snapshot, *input == runtime::Input::Up
                        ? SpacerScrollDirection::Up : SpacerScrollDirection::Down, sequence, GetTickCount64());
                } else {
                    scrollReceipts.Reset();
                }
            }
            // Passive input still reaches the game and the render thread never performs this IO
            Sleep(20);
        }
        // Hook shutdown precedes RuntimeStop, so the final packet records drawing as stopped
        files->WriteRendererStatus(runtime::RendererStatusJson(processId, ReadRendererStatus()));
    } catch (...) {
        // Closing uses the prebuilt snapshot even when allocation or diagnostic IO fails
        Publish(failureSnapshot);
        State().running.store(false, std::memory_order_release);
        try {
            files->WriteRuntimeError(runtime::RuntimeErrorJson(processId));
        } catch (...) {
            // Error publication is best effort and must not escape the worker
        }
    }
}
}

bool RuntimeStart(const BootstrapConfig& config, std::string& error) {
    auto& state = State();
    std::lock_guard<std::mutex> lock(state.lifecycle);
    error.clear();
    if (state.worker.joinable()) {
        error = "The overlay runtime has already started";
        return false;
    }
    try {
        if (config.size != sizeof(BootstrapConfig) || config.version != ProtocolVersion
            || config.expectedProcessId != GetCurrentProcessId() || !IsWindow(config.gameWindow)) {
            throw std::runtime_error("Invalid overlay bootstrap identity");
        }
        DWORD windowProcessId = 0;
        GetWindowThreadProcessId(config.gameWindow, &windowProcessId);
        if (windowProcessId != config.expectedProcessId) throw std::runtime_error("The game window belongs to another process");
        auto end = std::find(std::begin(config.sessionDirectory), std::end(config.sessionDirectory), L'\0');
        if (end == std::end(config.sessionDirectory)) throw std::runtime_error("The session directory is not bounded");
        auto files = std::make_shared<runtime::SessionFiles>(std::wstring(config.sessionDirectory, end));
        auto prepared = runtime::PrepareRuntimeStartup(files->ReadState(), files->ReadBindings(),
            config.expectedProcessId);
        auto bindings = std::move(prepared.bindings);
        auto initial = std::move(prepared.state);
        if (initial.snapshot->instructionPositionEditor && !PositionControlsAvailable(bindings)) {
            throw std::runtime_error("Initial position editor lacks exact saved action slots");
        }
        if (initial.snapshot->menuPositionEditor && !MenuPositionControlsAvailable(bindings)) {
            throw std::runtime_error("Initial menu position editor lacks exact saved actions");
        }
        auto failure = std::make_shared<MenuSnapshot>();
        failure->title = "Menu unavailable";
        failure->context = "RUNTIME ERROR";
        failure->notice = "Overlay runtime stopped after an unexpected failure";
        auto stale = std::make_shared<MenuSnapshot>();
        stale->notice = "Overlay paused: session owner heartbeat is stale";
        Publish(initial.snapshot);
        state.running.store(true, std::memory_order_release);
        state.worker = std::thread(Worker, files, config.gameWindow, config.expectedProcessId,
            std::move(bindings), initial.lastSequence, initial.generation, failure, stale);
        files->WriteReady(runtime::ReadyJson(config.expectedProcessId));
        if (!state.running.load(std::memory_order_acquire)) throw std::runtime_error("Overlay worker failed during initialization");
        return true;
    } catch (const std::exception& exception) {
        error = exception.what();
    } catch (...) {
        error = "Overlay runtime initialization failed";
    }
    state.running.store(false, std::memory_order_release);
    if (state.worker.joinable()) state.worker.join();
    Publish(nullptr);
    return false;
}

std::shared_ptr<const MenuSnapshot> GetOverlaySnapshot() {
    return State().snapshot.load(std::memory_order_acquire);
}

void RuntimeStop() {
    auto& state = State();
    std::lock_guard<std::mutex> lock(state.lifecycle);
    // Stop is called outside DllMain and waits for the last bounded IO operation
    state.running.store(false, std::memory_order_release);
    if (state.worker.joinable()) state.worker.join();
    Publish(nullptr);
}

}
