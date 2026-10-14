#include "NativeInputRouter.h"
#include "OpeningActionBindings.h"
#include "quick/ClosedMenuOpeningArbitrator.h"

#include <algorithm>
#include <chrono>
#include <limits>
#include <stdexcept>
#include <utility>

namespace iw::overlay::runtime {
namespace {
// The source scroll branch adds 25 ms before the monitor's normal 50 ms wait
constexpr std::uint64_t ScrollMonitorOverheadMilliseconds = 75;
// The outer source monitor waits fifty after every completed logical iteration
constexpr std::uint64_t SourceMonitorIntervalMilliseconds = 50;
// Knife and dead Jump add two hundred before the outer monitor wait
constexpr std::uint64_t BackMonitorIntervalMilliseconds = 250;
}

std::uint64_t MonotonicMilliseconds() {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

void ValidateBindings(const Bindings& bindings) {
    const std::vector<int>* groups[] = { &bindings.aim, &bindings.melee,
        &bindings.up, &bindings.down, &bindings.select, &bindings.previousChoice, &bindings.nextChoice,
        &bindings.attack, &bindings.moveUp, &bindings.moveDown, &bindings.frag, &bindings.secondaryOffhand, &bindings.sprint, &bindings.stance, &bindings.jump };
    std::array<unsigned, 256> membership{};
    for (unsigned group = 0; group < std::size(groups); ++group) {
        if ((group < 5 && groups[group]->empty()) || groups[group]->size() > 16) {
            throw std::invalid_argument("Required bindings need one to sixteen keys; optional bindings allow zero");
        }
        for (int key : *groups[group]) {
            if (key < 1 || key > 254 || (membership[key] & (1u << group)) != 0) {
                throw std::invalid_argument("A binding contains an invalid or duplicate key");
            }
            membership[key] |= 1u << group;
            // Exact movement groups share only their matching combined scroll group
            if ((membership[key] & (membership[key] - 1)) != 0
                && membership[key] != ((1u << 0) | (1u << 2))
                && membership[key] != ((1u << 3) | (1u << 7))
                && membership[key] != ((1u << 2) | (1u << 8))
                && membership[key] != ((1u << 3) | (1u << 9))) {
                throw std::invalid_argument("Bindings overlap outside exact action aliases");
            }
        }
    }
    // The producer's combined Down group must contain every exact attack key
    for (int key : bindings.attack) {
        if (std::find(bindings.down.begin(), bindings.down.end(), key) == bindings.down.end()) {
            throw std::invalid_argument("Exact attack bindings must be present in Down");
        }
    }
    // Source action slots must be present in combined scrolling without aim or attack aliases
    for (const auto& [exact, combined] : {std::pair{&bindings.moveUp, &bindings.up},
        std::pair{&bindings.moveDown, &bindings.down}}) {
        for (int key : *exact) {
            if (std::find(combined->begin(), combined->end(), key) == combined->end()) {
                throw std::invalid_argument("Exact movement bindings must be present in their scroll group");
            }
        }
    }
}

NativeInputRouter::NativeInputRouter(Bindings bindings) : bindings_(std::move(bindings)) {
    ValidateBindings(bindings_);
    closedOpening_ = std::make_unique<quick::ClosedMenuOpeningArbitrator>(bindings_);
    for (const auto* group : { &bindings_.aim, &bindings_.melee, &bindings_.up,
        &bindings_.down, &bindings_.select, &bindings_.previousChoice, &bindings_.nextChoice, &bindings_.attack,
        &bindings_.moveUp, &bindings_.moveDown, &bindings_.frag, &bindings_.secondaryOffhand,
        &bindings_.sprint, &bindings_.stance, &bindings_.jump }) {
        for (int key : *group) {
            if (std::find(keys_.begin(), keys_.end(), key) == keys_.end()) {
                keys_.push_back(key);
            }
        }
    }
}

NativeInputRouter::~NativeInputRouter() = default;
NativeInputRouter::NativeInputRouter(NativeInputRouter&&) noexcept = default;
NativeInputRouter& NativeInputRouter::operator=(NativeInputRouter&&) noexcept = default;

void NativeInputRouter::CaptureUseDispatch(std::uint64_t sequence) {
    sourceUse_.CaptureDispatch(sequence);
}

void NativeInputRouter::CaptureMonitorDispatch(std::uint64_t sequence) {
    try {
        sourceMonitor_.CaptureDispatch(sequence);
    } catch (...) {
        // A malformed write receipt cannot leave the previous choice timer armed
        Reset();
        throw;
    }
}

void NativeInputRouter::Reset() {
    seeded_ = false;
    previous_.fill(false);
    repeatDirection_.reset();
    scrollCooldownActive_ = false;
    monitorWaitObservedMilliseconds_.reset();
    lastSourceSampleMilliseconds_.reset();
    lastSourceEvaluationMilliseconds_.reset();
    sourceEvaluationIntervalMilliseconds_ = SourceMonitorIntervalMilliseconds;
    sourceOrdinaryNeedsNeutral_ = true;
    sourceSelectLevelConsumed_ = false;
    suppressedMelee_ = false;
    previousMenuRevision_.reset();
    choices_.Reset();
    sourceUse_.Reset();
    sourceMonitor_.Reset();
    quickExit_.Reset();
    widthEditor_.Reset();
    instructionPosition_.Reset();
    menuPosition_.Reset();
    credits_.Reset();
    mainOpening_.Reset();
    previousOpeningControls_.reset();
    previousOpeningPolicies_.reset();
    previousEligibility_.reset();
    previousSurface_ = MenuSurface::Main;
    jumpHeld_ = false;
    closedOpening_->Reset();
}

std::optional<Input> NativeInputRouter::Poll(bool foregroundAllowed, bool menuOpen,
    bool commandPending, const std::function<bool(int)>& isDown,
    std::uint64_t nowMilliseconds, std::uint64_t choiceContext, bool quickExit, bool isRootMenu,
    InputMode mode, int scrollAnimationMilliseconds, std::optional<std::uint64_t> menuRevision,
    const MainOpeningControls* mainOpeningControls, unsigned skippedUpSpacers, unsigned skippedDownSpacers,
    const ClosedMenuOpeningPolicies* openingPolicies,
    const LocalMenuEligibility* localMenuEligibility, MenuSurface menuSurface, const MenuMonitorWait* monitorWait,
    const UseMonitorObservation* useObservation, const MonitorContinuationObservation* monitorObservation,
    ClosedOpeningPolling openingPolling) {
    if (!IsValidClosedOpeningPolling(openingPolling)) {
        Reset();
        throw std::invalid_argument("Unsupported closed opening polling mode");
    }
    try {
        ValidateMonitorObservation(monitorObservation, monitorWait, localMenuEligibility,
            menuOpen, mode == InputMode::Menu);
        if (monitorObservation && monitorObservation->continuation && useObservation
            && (useObservation->continuation || useObservation->callback))
            throw std::invalid_argument("Mechanical and Use invocations cannot share input");
        ValidateUseObservation(useObservation, localMenuEligibility, menuOpen,
            mode == InputMode::Menu || mode == InputMode::MenuLocked, mode == InputMode::MenuLocked, monitorWait != nullptr);
    } catch (...) { Reset(); throw; }
    if (monitorWait && (!IsValidMenuMonitorWait(*monitorWait) || mode != InputMode::Menu
        || (monitorWait->kind != MenuMonitorWaitKind::Back && !menuOpen))) {
        Reset();
        throw std::invalid_argument("Unsupported mechanical monitor wait");
    }
    if ((menuSurface != MenuSurface::Main && menuSurface != MenuSurface::Quick)
        || (localMenuEligibility && (!localMenuEligibility->revision
            || (localMenuEligibility->life != PlayerLifeState::Unknown
                && localMenuEligibility->life != PlayerLifeState::Alive
                && localMenuEligibility->life != PlayerLifeState::Dead)))) {
        Reset();
        throw std::invalid_argument("Unsupported local opening eligibility or menu surface");
    }
    if (scrollAnimationMilliseconds < 100 || scrollAnimationMilliseconds > 250
        || scrollAnimationMilliseconds % 10 != 0) {
        // Invalid direct calls cannot read keys or leave an old repeat armed
        Reset();
        throw std::invalid_argument("Unsupported scroll animation duration");
    }
    if (skippedUpSpacers > 127 || skippedDownSpacers > 127) {
        Reset();
        throw std::invalid_argument("Spacer cadence exceeds its row bound");
    }
    if (mainOpeningControls) {
        // A direct caller cannot read keys for a malformed or unavailable opening action
        try { ValidateMainOpeningBindings(bindings_, *mainOpeningControls); }
        catch (...) { Reset(); throw; }
    }
    if (openingPolicies) {
        try {
            if (!mainOpeningControls || !IsValidClosedMenuOpeningPolicies(*openingPolicies) ||
                *mainOpeningControls != openingPolicies->main) {
                throw std::invalid_argument("Dual policy requires its exact Main mirror");
            }
            // Disabled Quick still needs verified groups; parsed metadata cannot invent keys
            ValidateMainOpeningBindings(bindings_, openingPolicies->quick);
        } catch (...) { Reset(); throw; }
    }
    if (!foregroundAllowed) {
        Reset();
        return std::nullopt;
    }
    // The backend owns readiness; native time only retires input after a backward sample
    if (monitorWaitObservedMilliseconds_ && nowMilliseconds < *monitorWaitObservedMilliseconds_) {
        Reset();
        return std::nullopt;
    }
    monitorWaitObservedMilliseconds_ = monitorWait ? std::optional<std::uint64_t>(nowMilliseconds) : std::nullopt;
    if (mode != InputMode::Menu && mode != InputMode::WidthEditor && mode != InputMode::MenuLocked
        && mode != InputMode::InstructionPositionEditor && mode != InputMode::MenuPositionEditor
        && mode != InputMode::MenuCredits) {
        // Rejected mode calls retire held intent before valid input resumes
        Reset();
        throw std::invalid_argument("Unknown input mode");
    }
    if (mode == InputMode::WidthEditor && (bindings_.attack.empty()
        || bindings_.previousChoice.empty() || bindings_.nextChoice.empty())) {
        Reset();
        throw std::invalid_argument("Width editor requires exact attack and both offset bindings");
    }
    if (mode == InputMode::InstructionPositionEditor && (bindings_.moveUp.empty() || bindings_.moveDown.empty()
        || bindings_.previousChoice.empty() || bindings_.nextChoice.empty())) {
        Reset();
        throw std::invalid_argument("Position editor requires all four exact action-slot groups");
    }
    if (mode == InputMode::MenuPositionEditor && (bindings_.moveUp.empty() || bindings_.moveDown.empty()
        || bindings_.previousChoice.empty() || bindings_.nextChoice.empty()
        || bindings_.frag.empty() || bindings_.secondaryOffhand.empty())) {
        Reset();
        throw std::invalid_argument("Menu position requires four exact slots and both grenade groups");
    }
    const bool sourceLevels = openingPolling == ClosedOpeningPolling::SourceLevels;
    if (sourceLevels) {
        // Reject backward samples before physical input can revive a consumed chord
        if (lastSourceSampleMilliseconds_ && nowMilliseconds < *lastSourceSampleMilliseconds_) {
            Reset();
            return std::nullopt;
        }
        lastSourceSampleMilliseconds_ = nowMilliseconds;
        // A missing typed owner cannot lend its old source phase to another visit
        if (!localMenuEligibility) {
            lastSourceEvaluationMilliseconds_.reset();
            sourceEvaluationIntervalMilliseconds_ = SourceMonitorIntervalMilliseconds;
        }
    } else {
        // The legacy edge route owns no source-level clock
        lastSourceSampleMilliseconds_.reset();
        lastSourceEvaluationMilliseconds_.reset();
        sourceEvaluationIntervalMilliseconds_ = SourceMonitorIntervalMilliseconds;
    }
    std::array<bool, 256> down{};
    try {
        for (int key : keys_) down[key] = isDown(key);
    } catch (...) {
        Reset();
        throw;
    }
    const auto any = [](const std::vector<int>& group, const auto& states) {
        return std::any_of(group.begin(), group.end(), [&](int key) { return states[key]; });
    };
    bool sourceNeutralSample = false;
    if (sourceLevels) {
        // Physical release retires an unclassified Use independently of row redraws
        if (!any(bindings_.select, down)) sourceSelectLevelConsumed_ = false;
        const bool ordinaryHeld = any(bindings_.up, down) || any(bindings_.down, down)
            || any(bindings_.select, down) || any(bindings_.previousChoice, down)
            || any(bindings_.nextChoice, down) || any(bindings_.melee, down) || any(bindings_.jump, down);
        if (sourceOrdinaryNeedsNeutral_ && !ordinaryHeld) {
            sourceOrdinaryNeedsNeutral_ = false;
            // The neutral recovery sample starts a new ordinary idle wait rather than issuing a command
            if (menuOpen) {
                sourceNeutralSample = true;
                lastSourceEvaluationMilliseconds_ = nowMilliseconds;
                sourceEvaluationIntervalMilliseconds_ = SourceMonitorIntervalMilliseconds;
            }
        }
    }
    const bool openingHeld = mainOpeningControls && std::all_of(mainOpeningControls->actions.begin(),
        mainOpeningControls->actions.end(), [&](OpeningAction action) { return any(OpeningActionKeys(bindings_, action), down); });
    const bool openingPolicyChanged = mainOpeningControls
        ? !previousOpeningControls_ || *previousOpeningControls_ != *mainOpeningControls
        : previousOpeningControls_.has_value();
    const bool dualPolicyChanged = openingPolicies
        ? !previousOpeningPolicies_ || *previousOpeningPolicies_ != *openingPolicies
        : previousOpeningPolicies_.has_value();
    const bool eligibilityChanged = localMenuEligibility
        ? !previousEligibility_ || *previousEligibility_ != *localMenuEligibility
        : previousEligibility_.has_value();
    const bool observedContextChanged = eligibilityChanged || menuSurface != previousSurface_
        || (localMenuEligibility && !menuOpen && choiceContext != previousChoiceContext_);
    // Unknown permissions and backend work consume ordinary edges instead of queuing them
    const bool ordinaryDisabled = localMenuEligibility && mode == InputMode::Menu
        && (localMenuEligibility->hasMenu != true || localMenuEligibility->controlsDisabled != false
            || localMenuEligibility->life == PlayerLifeState::Unknown);
    // A source monitor lock does not disable the dedicated editor that owns that lock
    const bool observedBlocked = localMenuEligibility && (localMenuEligibility->blocked || ordinaryDisabled);
    const bool inputPending = commandPending || observedBlocked;
    sourceUse_.Observe(useObservation, localMenuEligibility, choiceContext, menuSurface, menuOpen,
        (mode == InputMode::Menu || mode == InputMode::MenuLocked) && !monitorWait,
        commandPending, observedBlocked, nowMilliseconds);
    sourceMonitor_.Observe(monitorObservation, monitorWait, localMenuEligibility, choiceContext,
        menuRevision, menuSurface, menuOpen, isRootMenu, mode == InputMode::Menu,
        commandPending, observedBlocked, nowMilliseconds);
    if (sourceMonitor_.Retired()) {
        // Cancellation consumes the inherited levels before legacy timers or fresh edges can reuse them
        previous_ = down;
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        choices_.Interrupt(any(bindings_.previousChoice, down) || any(bindings_.nextChoice, down));
        suppressedMelee_ = any(bindings_.melee, down);
        quickExit_.Reset(suppressedMelee_);
        return std::nullopt;
    }
    bool sourceEvaluationDue = !sourceLevels;
    if (sourceLevels && localMenuEligibility && !sourceNeutralSample
        && (!menuOpen || !sourceOrdinaryNeedsNeutral_) && !quickExit_.HasActiveHold()
        && mode == InputMode::Menu && !commandPending && !localMenuEligibility->blocked && !monitorWait) {
        // Guard-false facts still complete an idle iteration; queued work merely suspends it
        sourceEvaluationDue = !lastSourceEvaluationMilliseconds_
            || nowMilliseconds - *lastSourceEvaluationMilliseconds_ >= sourceEvaluationIntervalMilliseconds_;
        if (sourceEvaluationDue) {
            lastSourceEvaluationMilliseconds_ = nowMilliseconds;
            sourceEvaluationIntervalMilliseconds_ = SourceMonitorIntervalMilliseconds;
        }
    }
    const bool sourceOrdinaryAllowed = sourceLevels && menuOpen && localMenuEligibility
        && !inputPending && !monitorWait && mode == InputMode::Menu
        && !sourceOrdinaryNeedsNeutral_ && !sourceNeutralSample;
    const auto mechanicalResult = [&](std::optional<Input> result) {
        if (result && (*result == Input::PreviousChoice || *result == Input::NextChoice
            || *result == Input::Back || *result == Input::Close || *result == Input::JumpBack)) {
            // Only an emitted ordinary command stages the scope that its successful write may capture
            sourceMonitor_.Begin(monitorObservation, monitorWait, localMenuEligibility, choiceContext,
                menuRevision, menuSurface, menuOpen, isRootMenu,
                *result == Input::Back || *result == Input::Close || *result == Input::JumpBack);
            if (sourceLevels && (*result == Input::Back || *result == Input::Close || *result == Input::JumpBack)) {
                // A child knife loop starts its caller wait when its terminal result is emitted
                lastSourceEvaluationMilliseconds_ = nowMilliseconds;
                sourceEvaluationIntervalMilliseconds_ = BackMonitorIntervalMilliseconds;
            }
        }
        return result;
    };
    const bool jumpHeld = any(bindings_.jump, down);
    const bool freshJump = jumpHeld && !jumpHeld_
        && (!localMenuEligibility || choiceContext == previousChoiceContext_);
    jumpHeld_ = jumpHeld;
    const auto observedOpening = [&]() -> std::optional<ClosedMenuOpeningIntent> {
        if (!localMenuEligibility) {
            closedOpening_->Reset();
            return std::nullopt;
        }
        ClosedMenuOpeningPolicies policies;
        if (openingPolicies) policies = *openingPolicies;
        else if (mainOpeningControls) policies.main = *mainOpeningControls;
        ClosedMenuOpeningContext context{
            {localMenuEligibility->life, localMenuEligibility->hasMenu == true,
                localMenuEligibility->controlsDisabled != false},
            foregroundAllowed, menuOpen, commandPending,
            localMenuEligibility->blocked || mode != InputMode::Menu || monitorWait || !mainOpeningControls};
        // This callback accesses only the already captured array, never desktop input
        const auto intent = closedOpening_->Poll(context, policies, [&](int key) { return down[key]; },
            openingPolicies != nullptr, openingPolling);
        // Fast samples maintain the host release barrier but cannot queue an early intent
        return sourceEvaluationDue ? intent : std::nullopt;
    };
    const WidthEditorKeys editorKeys{any(bindings_.attack, down), any(bindings_.aim, down),
        any(bindings_.previousChoice, down), any(bindings_.nextChoice, down), any(bindings_.melee, down)};
    const InstructionPositionKeys positionKeys{any(bindings_.moveUp, down), any(bindings_.moveDown, down),
        any(bindings_.previousChoice, down), any(bindings_.nextChoice, down), any(bindings_.melee, down)};
    const MenuPositionKeys menuPositionKeys{positionKeys.up, positionKeys.down, positionKeys.left, positionKeys.right,
        any(bindings_.frag, down), any(bindings_.secondaryOffhand, down), positionKeys.melee};
    if (!seeded_ || menuOpen != previousMenuOpen_ || mode != previousMode_
        || menuRevision != previousMenuRevision_ || openingPolicyChanged || dualPolicyChanged || observedContextChanged) {
        // Legacy edges are seeded while source visits retain their current qualified levels
        // Only the same ready causal action may cross its own callback lock or row redraw
        const bool ownedReady = seeded_ && (sourceUse_.Ready() || sourceMonitor_.Ready());
        if (!ownedReady) previous_ = down;
        previousOpeningControls_ = mainOpeningControls ? std::optional<MainOpeningControls>(*mainOpeningControls) : std::nullopt;
        previousOpeningPolicies_ = openingPolicies ? std::optional<ClosedMenuOpeningPolicies>(*openingPolicies) : std::nullopt;
        mainOpening_.Reset(openingHeld);
        previousEligibility_ = localMenuEligibility ? std::optional<LocalMenuEligibility>(*localMenuEligibility) : std::nullopt;
        previousSurface_ = menuSurface;
        // Seed all aggregates now so a fresh partial completion on the next sample remains valid
        closedOpening_->Reset(false);
        const auto initialOpening = observedOpening();
        previousMenuOpen_ = menuOpen;
        seeded_ = true;
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        previousMenuRevision_ = menuRevision;
        previousChoiceContext_ = choiceContext;
        previousQuickExit_ = quickExit;
        previousRootMenu_ = isRootMenu;
        previousMode_ = mode;
        choices_.Reset();
        quickExit_.Reset(std::any_of(bindings_.melee.begin(), bindings_.melee.end(),
            [&](int key) { return down[key]; }));
        widthEditor_.Reset(editorKeys);
        instructionPosition_.Reset(positionKeys);
        menuPosition_.Reset(menuPositionKeys);
        credits_.Reset(any(bindings_.melee, down));
        if (!ownedReady) {
            // A closed current-level visit supplies its own policy and actor proof after ordinary edges are seeded
            if (!menuOpen && sourceLevels && initialOpening) {
                sourceUse_.Cancel();
                sourceMonitor_.Cancel();
                return *initialOpening == ClosedMenuOpeningIntent::Main ? Input::OpenMain : Input::OpenQuick;
            }
            if (!sourceOrdinaryAllowed || !sourceEvaluationDue) return std::nullopt;
        }
    }
    std::array<bool, 256> pressed{};
    for (int key : keys_) pressed[key] = down[key] && !previous_[key];
    previous_ = down;
    // Observe the complete chord before any mode or pending-ACK branch can return
    const bool freshOpening = mainOpening_.Poll(openingHeld, inputPending || menuOpen || mode != InputMode::Menu || monitorWait);
    const auto scopedOpening = observedOpening();
    if (mode == InputMode::MenuLocked) {
        // A temporary unlock consumes keys while leaving the logical menu open
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        choices_.Reset();
        quickExit_.Reset(any(bindings_.melee, down));
        widthEditor_.Reset(editorKeys);
        instructionPosition_.Reset(positionKeys);
        menuPosition_.Reset(menuPositionKeys);
        credits_.Reset(any(bindings_.melee, down));
        previousChoiceContext_ = choiceContext;
        return std::nullopt;
    }
    if (mode == InputMode::WidthEditor) {
        // Editor controls bypass normal scrolling, selection and optional Quick Exit
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        choices_.Reset();
        quickExit_.Reset();
        const bool changed = choiceContext != previousChoiceContext_;
        previousChoiceContext_ = choiceContext;
        return menuOpen ? widthEditor_.Poll(editorKeys, inputPending, changed, nowMilliseconds)
            : std::nullopt;
    }
    if (mode == InputMode::InstructionPositionEditor) {
        // Position changes bypass menu scrolling, choice edits, Use and optional Quick Exit
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        choices_.Reset();
        quickExit_.Reset();
        const bool changed = choiceContext != previousChoiceContext_;
        previousChoiceContext_ = choiceContext;
        return menuOpen ? instructionPosition_.Poll(positionKeys, inputPending, changed, nowMilliseconds)
            : std::nullopt;
    }
    if (mode == InputMode::MenuPositionEditor) {
        // Menu positioning owns all seven actions without invoking ordinary menu gestures
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        choices_.Reset();
        quickExit_.Reset();
        const bool changed = choiceContext != previousChoiceContext_;
        previousChoiceContext_ = choiceContext;
        return menuOpen ? menuPosition_.Poll(menuPositionKeys, inputPending, changed, nowMilliseconds)
            : std::nullopt;
    }
    if (mode == InputMode::MenuCredits) {
        // Credits ignore scrolling, selection, editor actions and optional knife hold gestures
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        suppressedMelee_ = false;
        choices_.Reset();
        quickExit_.Reset();
        const bool changed = choiceContext != previousChoiceContext_;
        previousChoiceContext_ = choiceContext;
        return menuOpen ? credits_.Poll(any(bindings_.melee, down), inputPending, changed) : std::nullopt;
    }
    const auto scrollInterval = [&](Input input) {
        const auto skipped = input == Input::Up ? skippedUpSpacers : skippedDownSpacers;
        // Legal durations use even ten-millisecond steps, so each source half-wait is exact
        return static_cast<std::uint64_t>(scrollAnimationMilliseconds) + ScrollMonitorOverheadMilliseconds
            + static_cast<std::uint64_t>(scrollAnimationMilliseconds / 2) * skipped;
    };
    const bool up = any(bindings_.up, down);
    const bool lower = any(bindings_.down, down);
    const auto direction = up != lower ? std::optional<Input>(up ? Input::Up : Input::Down)
        : std::nullopt;
    if (repeatDirection_ != direction) repeatDirection_.reset();
    const bool previousChoice = any(bindings_.previousChoice, down);
    const bool nextChoice = any(bindings_.nextChoice, down);
    const bool contextChanged = choiceContext != previousChoiceContext_;
    choices_.Observe(previousChoice, nextChoice, contextChanged);
    previousChoiceContext_ = choiceContext;
    if (monitorWait) {
        // These keys are already sampled, so a paused edge cannot become a later command
        repeatDirection_.reset();
        scrollCooldownActive_ = false;
        if (monitorWait->kind == MenuMonitorWaitKind::Back) choices_.Interrupt(previousChoice || nextChoice);
        // A choice wait preserves its admitted same-direction repeat while retiring knife input
        const bool heldKnife = any(bindings_.melee, down);
        quickExit_.Reset(heldKnife);
        suppressedMelee_ = heldKnife;
        previousQuickExit_ = quickExit;
        previousRootMenu_ = isRootMenu;
        return std::nullopt;
    }
    if (sourceLevels) {
        // Closed and ordinary visits share a clock, but neither borrows permission from a receipt
        if (!menuOpen) {
            if (!scopedOpening) return std::nullopt;
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            return *scopedOpening == ClosedMenuOpeningIntent::Main ? Input::OpenMain : Input::OpenQuick;
        }
        if (!sourceOrdinaryAllowed) {
            // Current host or actor denial retires a child hold without queuing its result
            quickExit_.Reset(any(bindings_.melee, down));
            return std::nullopt;
        }
        const bool meleeHeld = any(bindings_.melee, down);
        if (quickExit_.HasActiveHold()) {
            // The source's inner knife loop samples before returning to the ordinary branch chain
            return mechanicalResult(quickExit_.Poll(meleeHeld, false, quickExit, isRootMenu,
                contextChanged || quickExit != previousQuickExit_ || isRootMenu != previousRootMenu_,
                false, nowMilliseconds).command);
        }
        previousQuickExit_ = quickExit;
        previousRootMenu_ = isRootMenu;
        if (!sourceEvaluationDue) return std::nullopt;
        const auto discardLowerBranches = [&]() {
            // The next logical iteration reads current levels rather than inherited legacy timers
            repeatDirection_.reset();
            scrollCooldownActive_ = false;
            choices_.Reset();
            quickExit_.Reset();
            suppressedMelee_ = false;
        };
        // One source iteration takes the first matching current predicate
        if (direction) {
            discardLowerBranches();
            sourceEvaluationIntervalMilliseconds_ = scrollInterval(*direction);
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            return direction;
        }
        if (any(bindings_.select, down)) {
            discardLowerBranches();
            // Known callbacks may repeat only through their exact captured ready invocation
            // Generic row timing remains unclassified, so its held Use needs physical release
            if (sourceSelectLevelConsumed_ && !sourceUse_.Ready()) return std::nullopt;
            sourceSelectLevelConsumed_ = true;
            sourceMonitor_.Cancel();
            sourceUse_.Begin(useObservation, localMenuEligibility, choiceContext, menuSurface);
            return Input::Select;
        }
        if (previousChoice != nextChoice) {
            discardLowerBranches();
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            // The actual producer owns slider180 versus nonslider50 readiness
            return mechanicalResult(previousChoice ? Input::PreviousChoice : Input::NextChoice);
        }
        const bool deadJump = localMenuEligibility->life == PlayerLifeState::Dead
            && menuSurface == MenuSurface::Quick && jumpHeld;
        if (meleeHeld || deadJump) {
            discardLowerBranches();
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            if (deadJump && (!meleeHeld || !quickExit || isRootMenu))
                return mechanicalResult(Input::JumpBack);
            // A current knife level admits the child loop without an artificial second press
            return mechanicalResult(quickExit_.Poll(meleeHeld, true, quickExit, isRootMenu,
                false, false, nowMilliseconds).command);
        }
        return std::nullopt;
    }
    if ((scrollCooldownActive_ || repeatDirection_) && nowMilliseconds < lastScrollMilliseconds_) {
        // Elapsed subtraction is safe only after rollback has discarded inherited holds
        Reset();
        return std::nullopt;
    }
    if (inputPending && (scrollCooldownActive_ || repeatDirection_)) lastScrollMilliseconds_ = nowMilliseconds;
    if (menuOpen && scrollCooldownActive_) {
        if (nowMilliseconds - lastScrollMilliseconds_ < scrollIntervalMilliseconds_) {
            // The monitor wait consumes every observed edge even after navigation is released
            choices_.Interrupt(previousChoice || nextChoice);
            quickExit_.Reset(any(bindings_.melee, down));
            suppressedMelee_ = any(bindings_.melee, down);
            previousQuickExit_ = quickExit;
            previousRootMenu_ = isRootMenu;
            return std::nullopt;
        }
        scrollCooldownActive_ = false;
    }
    if (suppressedMelee_ && !any(bindings_.melee, down)) {
        // Release restores knife gestures without replaying a discarded lower-priority press
        suppressedMelee_ = false;
        quickExit_.Reset();
    }
    const bool meleeHeld = any(bindings_.melee, down);
    const bool gestureContextChanged = contextChanged || quickExit != previousQuickExit_
        || isRootMenu != previousRootMenu_;
    const bool activeHold = quickExit_.HasActiveHold();
    QuickExitResult gesture;
    if (!suppressedMelee_ && (activeHold || gestureContextChanged || inputPending)) {
        // Only an already admitted knife loop can run before ordinary monitor branches
        gesture = quickExit_.Poll(meleeHeld, any(bindings_.melee, pressed),
            quickExit, isRootMenu, gestureContextChanged, inputPending, nowMilliseconds);
    }
    previousQuickExit_ = quickExit;
    previousRootMenu_ = isRootMenu;
    if (inputPending) {
        // A pending packet consumes edges and delays repeats without queuing a burst
        choices_.Pause(nowMilliseconds);
        return std::nullopt;
    }
    if (!menuOpen) {
        if (localMenuEligibility) {
            if (!scopedOpening) return std::nullopt;
            // A new scoped opening retires history without borrowing permission from that history
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            return *scopedOpening == ClosedMenuOpeningIntent::Main ? Input::OpenMain : Input::OpenQuick;
        }
        if (mainOpeningControls) return freshOpening ? std::optional<Input>(Input::Toggle) : std::nullopt;
        return any(bindings_.aim, down) && any(bindings_.melee, pressed)
            ? std::optional<Input>(Input::Toggle) : std::nullopt;
    }
    if (activeHold || gesture.consumesInput) {
        // A knife gesture owns input until its single result or required release
        repeatDirection_.reset();
        choices_.Interrupt(previousChoice || nextChoice);
        return mechanicalResult(gesture.command);
    }
    const auto discardKnife = [&]() {
        // A lower-priority knife press cannot start a delayed close after another branch wins
        suppressedMelee_ = meleeHeld;
        quickExit_.Reset(meleeHeld);
    };
    // The source exclusive chain checks scrolling, Use, sliders, then knife or dead Jump
    if (up != lower) {
        const bool fresh = any(up ? bindings_.up : bindings_.down, pressed) || sourceUse_.Ready() || sourceMonitor_.Ready();
        if (fresh) {
            choices_.Interrupt(previousChoice || nextChoice);
            repeatDirection_ = direction;
            lastScrollMilliseconds_ = nowMilliseconds;
            scrollIntervalMilliseconds_ = scrollInterval(*direction);
            scrollCooldownActive_ = true;
            discardKnife();
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            return direction;
        }
    }
    if (repeatDirection_ && nowMilliseconds - lastScrollMilliseconds_ >= scrollIntervalMilliseconds_) {
        lastScrollMilliseconds_ = nowMilliseconds;
        scrollIntervalMilliseconds_ = scrollInterval(*repeatDirection_);
        scrollCooldownActive_ = true;
        choices_.Interrupt(previousChoice || nextChoice);
        discardKnife();
        sourceUse_.Cancel();
        sourceMonitor_.Cancel();
        return repeatDirection_;
    }
    if (any(bindings_.select, pressed) || ((sourceUse_.Ready() || sourceMonitor_.Ready()) && any(bindings_.select, down))) {
        choices_.Interrupt(previousChoice || nextChoice);
        discardKnife();
        sourceMonitor_.Cancel();
        sourceUse_.Begin(useObservation, localMenuEligibility, choiceContext, menuSurface);
        return Input::Select;
    }
    const bool resumedChoice = (sourceUse_.Ready() || sourceMonitor_.Ready()) && previousChoice != nextChoice;
    if (resumedChoice) {
        // Only the same completed callback may resume a current held source slot
        choices_.Reset();
    }
    if (auto choice = choices_.Poll(previousChoice, nextChoice,
        any(bindings_.previousChoice, pressed) || (resumedChoice && previousChoice),
        any(bindings_.nextChoice, pressed) || (resumedChoice && nextChoice), nowMilliseconds)) {
        discardKnife();
        sourceUse_.Cancel();
        sourceMonitor_.Cancel();
        return mechanicalResult(choice);
    }
    const bool resumedJump = (sourceUse_.Ready() || sourceMonitor_.Ready()) && jumpHeld;
    if (localMenuEligibility && localMenuEligibility->life == PlayerLifeState::Dead
        && menuSurface == MenuSurface::Quick && (freshJump || resumedJump)) {
        // Dead Jump enters the source Back branch after scrolling, Use and sliders
        repeatDirection_.reset();
        choices_.Interrupt(previousChoice || nextChoice);
        sourceUse_.Cancel();
        sourceMonitor_.Cancel();
        if (!meleeHeld || !quickExit || isRootMenu) {
            quickExit_.Reset(meleeHeld);
            return mechanicalResult(Input::JumpBack);
        }
        // Jump and knife share one source branch, whose child hold tests the knife state
        // A new scoped Jump admits that branch even if an earlier knife edge was discarded
        suppressedMelee_ = false;
        quickExit_.Reset();
        return mechanicalResult(quickExit_.Poll(meleeHeld, freshJump || resumedJump, quickExit, isRootMenu,
            false, false, nowMilliseconds).command);
    }
    const bool resumedKnife = (sourceUse_.Ready() || sourceMonitor_.Ready()) && meleeHeld;
    if (resumedKnife) {
        // Higher branches already declined, so this new knife loop owns the current iteration
        suppressedMelee_ = false;
        quickExit_.Reset();
    }
    if (!suppressedMelee_) {
        gesture = quickExit_.Poll(meleeHeld, any(bindings_.melee, pressed) || resumedKnife, quickExit, isRootMenu,
            false, false, nowMilliseconds);
        if (gesture.consumesInput) {
            // Consume the prior action at hold admission, before a later Back or Close result
            sourceUse_.Cancel();
            sourceMonitor_.Cancel();
            repeatDirection_.reset();
            choices_.Interrupt(previousChoice || nextChoice);
            return mechanicalResult(gesture.command);
        }
    }
    return std::nullopt;
}

CommandGate::CommandGate(unsigned long long initialSequence)
    : acknowledged_(initialSequence), sent_(initialSequence) {}

bool CommandGate::Pending() const { return acknowledged_ < sent_; }

void CommandGate::ObserveAck(unsigned long long sequence) {
    if (sequence < acknowledged_) throw std::runtime_error("State acknowledgement moved backwards");
    acknowledged_ = sequence;
    if (sent_ < sequence) sent_ = sequence;
}

unsigned long long CommandGate::NextSequence() const {
    if (Pending() || sent_ == std::numeric_limits<unsigned long long>::max()) {
        throw std::runtime_error("No command sequence is available");
    }
    return sent_ + 1;
}

void CommandGate::MarkSent(unsigned long long sequence) {
    if (sequence != NextSequence()) throw std::runtime_error("Unexpected command sequence");
    sent_ = sequence;
}

const char* InputName(Input input) {
    switch (input) {
        case Input::OpenMain: return "openMain";
        case Input::OpenQuick: return "openQuick";
        case Input::JumpBack: return "jumpBack";
        case Input::MoveMenuUp: return "moveMenuUp";
        case Input::MoveMenuDown: return "moveMenuDown";
        case Input::MoveMenuLeft: return "moveMenuLeft";
        case Input::MoveMenuRight: return "moveMenuRight";
        case Input::IncreaseMenuPositionStep: return "increaseMenuPositionStep";
        case Input::DecreaseMenuPositionStep: return "decreaseMenuPositionStep";
        case Input::FinishMenuPositionEditor: return "finishMenuPositionEditor";
        case Input::FinishMenuCredits: return "finishMenuCredits";
        case Input::Toggle: return "toggle";
        case Input::Up: return "up";
        case Input::Down: return "down";
        case Input::Select: return "select";
        case Input::Back: return "back";
        case Input::PreviousChoice: return "previousChoice";
        case Input::NextChoice: return "nextChoice";
        case Input::Close: return "close";
        case Input::IncreaseWidth: return "increaseWidth";
        case Input::DecreaseWidth: return "decreaseWidth";
        case Input::IncreaseWidthStep: return "increaseWidthStep";
        case Input::DecreaseWidthStep: return "decreaseWidthStep";
        case Input::FinishWidthEditor: return "finishWidthEditor";
        case Input::MoveInstructionsUp: return "moveInstructionsUp";
        case Input::MoveInstructionsDown: return "moveInstructionsDown";
        case Input::MoveInstructionsLeft: return "moveInstructionsLeft";
        case Input::MoveInstructionsRight: return "moveInstructionsRight";
        case Input::FinishInstructionPositionEditor: return "finishInstructionPositionEditor";
    }
    throw std::invalid_argument("Unknown overlay input");
}

}
