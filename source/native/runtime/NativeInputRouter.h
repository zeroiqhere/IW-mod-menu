#pragma once
#include "ChoiceRepeat.h"
#include "../shared/monitor/MenuMonitorWait.h"
#include "monitor/UseMonitorObservation.h"
#include "monitor/SourceUseInput.h"
#include "monitor/SourceMonitorInput.h"
#include "QuickExitGesture.h"
#include "WidthEditorInput.h"
#include "InstructionPositionInput.h"
#include "MenuPositionInput.h"
#include "MenuCreditsInput.h"
#include "MainOpeningInput.h"
#include "../shared/OpenControls/MainOpeningControls.h"
#include "../shared/QuickMenu/ClosedMenuOpening.h"
#include "../shared/QuickMenu/ClosedOpeningPolling.h"
#include "../shared/QuickMenu/LocalMenuEligibility.h"

#include <array>
#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <vector>

namespace iw::overlay::runtime {
namespace quick { class ClosedMenuOpeningArbitrator; }

enum class Input { Toggle, Up, Down, Select, Back, PreviousChoice, NextChoice, Close,
    IncreaseWidth, DecreaseWidth, IncreaseWidthStep, DecreaseWidthStep, FinishWidthEditor,
    MoveInstructionsUp, MoveInstructionsDown, MoveInstructionsLeft, MoveInstructionsRight,
    FinishInstructionPositionEditor, MoveMenuUp, MoveMenuDown, MoveMenuLeft, MoveMenuRight,
    IncreaseMenuPositionStep, DecreaseMenuPositionStep, FinishMenuPositionEditor, FinishMenuCredits, OpenMain, OpenQuick, JumpBack };
enum class InputMode { Menu, WidthEditor, MenuLocked, InstructionPositionEditor, MenuPositionEditor, MenuCredits };

std::uint64_t MonotonicMilliseconds();

// Bindings are game actions rather than fixed keyboard assumptions
struct Bindings {
    std::vector<int> aim;
    std::vector<int> melee;
    std::vector<int> up;
    std::vector<int> down;
    std::vector<int> select;
    std::vector<int> previousChoice = {};
    std::vector<int> nextChoice = {};
    std::vector<int> attack = {};
    std::vector<int> moveUp = {};
    std::vector<int> moveDown = {};
    // Grenade actions are optional and never receive guessed keys
    std::vector<int> frag = {};
    std::vector<int> secondaryOffhand = {};
    std::vector<int> sprint = {};
    std::vector<int> stance = {};
    std::vector<int> jump = {};
};

class NativeInputRouter {
public:
    explicit NativeInputRouter(Bindings bindings);
    ~NativeInputRouter();
    NativeInputRouter(NativeInputRouter&&) noexcept;
    NativeInputRouter& operator=(NativeInputRouter&&) noexcept;
    NativeInputRouter(const NativeInputRouter&) = delete;
    NativeInputRouter& operator=(const NativeInputRouter&) = delete;
    std::optional<Input> Poll(bool foregroundAllowed, bool menuOpen,
        bool commandPending, const std::function<bool(int)>& isDown,
        std::uint64_t nowMilliseconds = MonotonicMilliseconds(),
        std::uint64_t choiceContext = 0, bool quickExit = false, bool isRootMenu = true,
        InputMode mode = InputMode::Menu, int scrollAnimationMilliseconds = 120,
        std::optional<std::uint64_t> menuRevision = std::nullopt,
        const MainOpeningControls* mainOpeningControls = nullptr,
        unsigned skippedUpSpacers = 0, unsigned skippedDownSpacers = 0,
        const ClosedMenuOpeningPolicies* openingPolicies = nullptr,
        const LocalMenuEligibility* localMenuEligibility = nullptr, MenuSurface menuSurface = MenuSurface::Main,
        const MenuMonitorWait* monitorWait = nullptr,
        const UseMonitorObservation* useObservation = nullptr,
        const MonitorContinuationObservation* monitorObservation = nullptr,
        ClosedOpeningPolling openingPolling = ClosedOpeningPolling::FreshEdges);
    // Only a successfully written Select command may claim its sequence
    void CaptureUseDispatch(std::uint64_t sequence);
    // A written choice or Back result claims its staged ordinary visit
    void CaptureMonitorDispatch(std::uint64_t sequence);
    void Reset();
private:
    Bindings bindings_;
    std::vector<int> keys_;
    std::array<bool, 256> previous_{};
    bool seeded_ = false;
    bool previousMenuOpen_ = false;
    std::optional<Input> repeatDirection_;
    std::uint64_t lastScrollMilliseconds_ = 0;
    // Legacy edge polling keeps the dispatched scroll wait after every key releases
    std::uint64_t scrollIntervalMilliseconds_ = 195;
    bool scrollCooldownActive_ = false;
    // Legacy polling cannot reuse a knife edge discarded by an earlier branch
    bool suppressedMelee_ = false;
    std::optional<std::uint64_t> previousMenuRevision_ = std::nullopt;
    std::uint64_t previousChoiceContext_ = 0;
    bool previousQuickExit_ = false;
    bool previousRootMenu_ = true;
    InputMode previousMode_ = InputMode::Menu;
    // A backward sample cannot revive a hold consumed during an owned monitor pause
    std::optional<std::uint64_t> monitorWaitObservedMilliseconds_;
    // Every source sample detects rollback, including samples between closed evaluations
    std::optional<std::uint64_t> lastSourceSampleMilliseconds_;
    // Open and closed source visits share one relative monitor wait
    std::optional<std::uint64_t> lastSourceEvaluationMilliseconds_;
    std::uint64_t sourceEvaluationIntervalMilliseconds_ = 50;
    // Host recovery needs ordinary action release as well as opening-chord release
    bool sourceOrdinaryNeedsNeutral_ = false;
    // An unknown Select wait cannot be inferred from an ACK or a changed row
    bool sourceSelectLevelConsumed_ = false;
    ChoiceRepeat choices_;
    SourceUseInput sourceUse_;
    SourceMonitorInput sourceMonitor_;
    QuickExitGesture quickExit_;
    WidthEditorInput widthEditor_;
    InstructionPositionInput instructionPosition_;
    MenuPositionInput menuPosition_;
    MenuCreditsInput credits_;
    MainOpeningInput mainOpening_;
    // The arbitrator consumes the router's cached sample rather than another external key reader
    std::unique_ptr<quick::ClosedMenuOpeningArbitrator> closedOpening_;
    std::optional<LocalMenuEligibility> previousEligibility_;
    MenuSurface previousSurface_ = MenuSurface::Main;
    bool jumpHeld_ = false;
    std::optional<MainOpeningControls> previousOpeningControls_;
    std::optional<ClosedMenuOpeningPolicies> previousOpeningPolicies_;
};

// One command remains pending until the owner acknowledges its sequence
class CommandGate {
public:
    explicit CommandGate(unsigned long long initialSequence);
    bool Pending() const;
    void ObserveAck(unsigned long long sequence);
    unsigned long long NextSequence() const;
    void MarkSent(unsigned long long sequence);
private:
    unsigned long long acknowledged_;
    unsigned long long sent_;
};

const char* InputName(Input input);
void ValidateBindings(const Bindings& bindings);

}
