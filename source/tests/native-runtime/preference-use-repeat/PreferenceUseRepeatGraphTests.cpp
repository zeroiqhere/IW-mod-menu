#include "../closed-level-opening/ClosedLevelOpeningGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::preferenceUseGraph {
using closedLevelGraph::Read;
using closedLevelGraph::Router;
constexpr std::array Prefixes{"quick-exit", "spotlight", "colored", "large", "stealth"};
constexpr std::array Stages{"before", "select0", "wait179", "redraw180", "wait429", "ready430",
    "second-select430", "second-redraw610", "second-ready860"};
constexpr std::array DesignStages{"before", "select0", "wait249", "ready250", "second-select250", "wait499", "ready500"};
constexpr std::array DesignCancellationFiles{"design-nav-moved249.json", "design-owner-replaced0.json",
    "design-focus-hidden0.json", "design-focus-return250.json"};
constexpr std::array ChangedDesignStages{"before", "select0", "wait49", "redraw50", "wait249", "ready250",
    "second-select250", "wait499", "ready500"};
constexpr std::array ChangedDesignCancellationFiles{"design-changed-classic-navigation-moved49.json",
    "design-changed-classic-owner-replaced0.json", "design-changed-classic-focus-hidden0.json",
    "design-changed-classic-focus-return250.json"};

auto Frame(const std::filesystem::path& directory, const char* prefix, const char* stage) {
    return Read(directory / (std::string(prefix) + "-" + stage + ".json"));
}

void ActualPreferencePacketsKeepDelayedPaintAndOneOwnedCallbackClock(const std::filesystem::path& directory) {
    std::set<std::string> expected{"preference-use-manifest.json"}, actual;
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    for (const auto* prefix : Prefixes) {
        const auto before = Frame(directory, prefix, "before");
        const auto index = before.snapshot->selectedIndex;
        const auto initialValue = before.snapshot->rows[index].value;
        Check(initialValue == "ON" || initialValue == "OFF", "Actual preference has no applied Boolean value");
        std::optional<MenuUseContinuation> receipt;
        for (const auto* stage : Stages) {
            const auto name = std::string(prefix) + "-" + stage + ".json";
            expected.insert(name);
            const auto parsed = Read(directory / name);
            const auto preview = ReadBackendPreview(directory / name, &labels);
            const auto& frame = *parsed.snapshot;
            Check(frame.localMenuEligibility == preview.localMenuEligibility
                && frame.menuUseContinuation == preview.menuUseContinuation
                && frame.menuOptionCallback == preview.menuOptionCallback,
                "Production readers disagree on actual preference ownership or pacing");
            Check(!frame.menuPreferenceRefresh && !frame.menuPreferenceRebuild && !frame.menuDesignTransition,
                "A typed ordinary preference acquired a competing timer");
            Check(frame.selectedIndex == index && frame.navigationRevision == before.snapshot->navigationRevision,
                "An owned preference redraw changed its source row or navigation");
            if (std::string_view(stage) == "before") continue;
            Check(frame.menuUseContinuation.has_value(), "Actual preference callback lost its written Use receipt");
            const auto current = *frame.menuUseContinuation;
            Check(current.kind == MenuOptionCallbackKind::Boolean && current.sequence == parsed.lastSequence,
                "Preference receipt does not identify its actual Boolean Select");
            if (std::string_view(stage) == "select0") receipt = current;
            if (std::string_view(stage) == "second-select430") {
                Check(receipt && current.revision == receipt->revision + 1 && current.sequence > receipt->sequence,
                    "Reselection reused the earlier invocation identity");
                receipt = current;
            }
            Check(receipt == current, "A scheduled preference redraw replaced its original Use identity");
            const auto phase = std::string_view(stage);
            const bool ready = phase == "ready430" || phase == "second-ready860";
            Check(frame.menuOptionCallback.has_value() != ready, "Preference wait retired at the wrong source deadline");
            if (!ready) Check(frame.menuOptionCallback->revision == current.revision,
                "Preference callback and receipt have different owners");
        }
        const auto early = Frame(directory, prefix, "wait179");
        const auto drawn = Frame(directory, prefix, "redraw180");
        const auto second = Frame(directory, prefix, "second-redraw610");
        Check(early.snapshot->rows[index].value == initialValue
            && drawn.snapshot->rows[index].value != initialValue
            && second.snapshot->rows[index].value == initialValue,
            "Preference paint did not follow the two actual toggles at their 180 ms redraws");
        Check(early.snapshot->menuRevision == before.snapshot->menuRevision
            && drawn.snapshot->menuRevision == *before.snapshot->menuRevision + 1,
            "Preference presentation changed before its redraw or spent an extra epoch");
        if (std::string_view(prefix) == "quick-exit") {
            Check(Frame(directory, prefix, "select0").snapshot->appearance.quickExit
                && !Frame(directory, prefix, "second-select430").snapshot->appearance.quickExit,
                "The committed knife policy waited for checkbox paint");
        }
    }
    for (const auto* stage : DesignStages)
        expected.insert("design-same-" + std::string(stage) + ".json");
    for (const auto* name : DesignCancellationFiles) expected.insert(name);
    for (const auto* stage : ChangedDesignStages)
        expected.insert("design-changed-classic-" + std::string(stage) + ".json");
    for (const auto* name : ChangedDesignCancellationFiles) expected.insert(name);
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file(), "Preference graph contains a non-file entry");
        actual.insert(entry.path().filename().string());
    }
    Check(actual == expected, "Actual preference inventory is missing packets or contains unexpected data");
    const auto manifest = Json::parse(useGraph::Bytes(directory / "preference-use-manifest.json"));
    Check(manifest.at("source").at("commit") == "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c"
        && manifest.at("source").at("sha256") == "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11"
        && manifest.at("source").at("designUsePath").at("sliderDispatchLine") == 85
        && manifest.at("source").at("designUsePath").at("callerWaitLine") == 99
        && manifest.at("source").at("designUsePath").at("outerMonitorWaitLine") == 168
        && manifest.at("source").at("designUsePath").at("handlerFile") == "Menu/menu_customization.gsc"
        && manifest.at("source").at("designUsePath").at("handlerSha256") == "29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da"
        && manifest.at("source").at("designUsePath").at("handlerLines") == "450-474"
        && manifest.at("source").at("designUsePath").at("unchangedValueReturnLine") == 453
        && manifest.at("session").at("actionCount") == 0
        && manifest.at("source").at("designUsePath").at("callerWaitMilliseconds") == 200
        && manifest.at("source").at("designUsePath").at("outerMonitorWaitMilliseconds") == 50
        && manifest.at("source").at("designUsePath").at("nominalUseCycleMilliseconds") == 250
        && manifest.at("timing").at("changedDesign").at("controllerRedrawMilliseconds") == 50
        && manifest.at("timing").at("changedDesign").at("controllerReadyMilliseconds") == 250
        && manifest.at("session").at("processMemoryWriterUsed") == false
        && manifest.at("commands").size() == 14 && manifest.at("frames").size() == 69,
        "Actual preference graph changed its source, command inventory or inert session contract");
    for (const auto& command : manifest.at("commands")) {
        const auto state = Read(directory / command.at("file").get<std::string>());
        const auto& frame = *state.snapshot;
        const auto& capture = command.at("capture");
        const auto native = Json::parse(runtime::CommandJson(42, command.at("sequence").get<std::uint64_t>(),
            runtime::Input::Select, frame.navigationRevision, runtime::ObservedOpeningControlsRevision(frame)));
        // Admission and presentation are observed context, not extra Select envelope fields
        Check(native.at("input") == command.at("input") && command.at("sequence") > capture.at("lastSequence")
            && native.at("sequence") == state.lastSequence
            && native.at("navigationRevision") == command.at("wire").at("navigationRevision")
            && native.at("openingControlsRevision") == command.at("wire").at("openingControlsRevision")
            && capture.at("admissionRevision") == frame.localMenuEligibility->revision
            && capture.at("navigationRevision") == frame.navigationRevision
            && capture.at("menuRevision") == *frame.menuRevision
            && capture.at("selectedSourceIndex") == frame.selectedIndex
            && capture.at("selectedSourceLabel") == frame.rows[frame.selectedIndex].label
            && !native.contains("localAdmissionRevision") && !native.contains("menuRevision")
            && command.at("wire").at("localAdmissionRevision").is_null()
            && command.at("wire").at("menuRevision").is_null(),
            "Native preference Select broadened or changed the producer's actual captured scopes");
    }
}

void ActualSameDesignPacketsKeepOnePlainClockAndExactReceipts(const std::filesystem::path& directory) {
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    const auto before = Frame(directory, "design-same", "before");
    const auto& initial = *before.snapshot;
    Check(initial.selectedIndex == 0 && initial.rows[0].label == "Design"
        && initial.appearance.design == MenuDesign::Apparition,
        "Same-design graph must begin on the applied Design choice");
    Check(!initial.menuOptionCallback && !initial.menuUseContinuation,
        "Design preselection must not contain an earlier callback receipt");
    std::optional<MenuUseContinuation> first;
    std::optional<MenuUseContinuation> second;
    for (const auto* stage : DesignStages) {
        const auto parsed = Frame(directory, "design-same", stage);
        const auto preview = ReadBackendPreview(directory / (std::string("design-same-") + stage + ".json"), &labels);
        const auto& frame = *parsed.snapshot;
        Check(frame.localMenuEligibility == preview.localMenuEligibility
            && frame.menuUseContinuation == preview.menuUseContinuation
            && frame.menuOptionCallback == preview.menuOptionCallback,
            "Both production readers must preserve actual same-design ownership");
        Check(frame.selectedIndex == initial.selectedIndex && frame.navigationRevision == initial.navigationRevision
            && frame.menuRevision == initial.menuRevision && frame.appearance == initial.appearance
            && !frame.menuDesignTransition,
            "Same-design callback cannot change appearance, cursor epochs or the visual transition");
        if (std::string_view(stage) == "before") continue;
        Check(frame.menuUseContinuation.has_value(), "Actual same-design Select lost its plain Use receipt");
        const auto current = *frame.menuUseContinuation;
        Check(current.kind == MenuOptionCallbackKind::Plain && current.sequence == parsed.lastSequence
            && current.admissionRevision == frame.localMenuEligibility->revision,
            "Same-design receipt must retain its plain action and admitted source sequence");
        const auto name = std::string_view(stage);
        if (name == "select0") first = current;
        if (name == "second-select250") {
            Check(first && current.revision == first->revision + 1 && current.sequence > first->sequence,
                "Second same-design selection must start a new callback invocation");
            second = current;
        }
        const bool secondInvocation = name == "second-select250" || name == "wait499" || name == "ready500";
        Check(current == (secondInvocation ? second : first),
            "Same-design wait or expiry replaced the original exact receipt");
        const bool ready = name == "ready250" || name == "ready500";
        Check(frame.menuOptionCallback.has_value() != ready,
            "Same-design callback must expire at250ms for each invocation");
        if (!ready) Check(frame.menuOptionCallback == MenuOptionCallback{current.revision,
            MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown},
            "Same-design callback must use the existing plain cooldown phase");
    }

    for (const auto* name : DesignCancellationFiles) {
        const auto frame = Read(directory / name);
        const auto preview = ReadBackendPreview(directory / name, &labels);
        Check(frame.snapshot->menuUseContinuation == preview.menuUseContinuation
            && frame.snapshot->menuOptionCallback == preview.menuOptionCallback
            && frame.snapshot->localMenuEligibility == preview.localMenuEligibility,
            "Both readers must agree after a same-design scope change");
        Check(!frame.snapshot->menuUseContinuation,
            "Navigation, owner or focus loss cannot leave a same-design ready receipt");
    }
    const auto focusReturn = Frame(directory, "design-focus", "return250");
    Check(focusReturn.snapshot->isOpen && !focusReturn.snapshot->menuOptionCallback
        && !focusReturn.snapshot->menuUseContinuation,
        "A focus-returned frame cannot restore the canceled same-design receipt");
}

void ActualChangedDesignPacketsSeparateRedrawEpochFromSourceVisit(const std::filesystem::path& directory) {
    const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
    const auto before = Frame(directory, "design-changed-classic", "before");
    const auto& initial = *before.snapshot;
    Check(initial.selectedIndex == 0 && initial.rows[0].label == "Design"
        && initial.rows[0].value == "Classic" && initial.appearance.design == MenuDesign::Apparition,
        "Changed-design graph must stage Classic without applying it before Use");
    Check(!initial.menuOptionCallback && !initial.menuUseContinuation && !initial.menuDesignTransition,
        "Changed-design staging cannot contain a prior invocation or transition");

    std::optional<MenuUseContinuation> first;
    std::optional<MenuUseContinuation> second;
    std::optional<MenuOpening> redrawOpening;
    for (const auto* stage : ChangedDesignStages) {
        const auto parsed = Frame(directory, "design-changed-classic", stage);
        const auto preview = ReadBackendPreview(directory / (std::string("design-changed-classic-") + stage + ".json"), &labels);
        const auto& frame = *parsed.snapshot;
        Check(frame.localMenuEligibility == preview.localMenuEligibility
            && frame.menuUseContinuation == preview.menuUseContinuation
            && frame.menuOptionCallback == preview.menuOptionCallback,
            "Both production readers must preserve actual changed-design ownership");
        Check(frame.selectedIndex == initial.selectedIndex && frame.navigationRevision == initial.navigationRevision
            && frame.rows.size() == initial.rows.size() && frame.rows[0].label == "Design"
            && frame.rows[0].value == "Classic",
            "The design redraw changes presentation without changing the selected source topology");
        const auto name = std::string_view(stage);
        const bool beforeSelect = name == "before";
        const bool afterRedraw = name == "redraw50" || name == "wait249" || name == "ready250"
            || name == "second-select250" || name == "wait499" || name == "ready500";
        const bool transitionActive = name == "select0" || name == "wait49" || name == "redraw50" || name == "wait249";
        const bool ready = name == "ready250" || name == "ready500";
        Check(frame.menuRevision == *initial.menuRevision + (afterRedraw ? 1u : 0u),
            "Only the observed50ms redraw advances MenuRevision");
        Check(frame.appearance.design == (afterRedraw ? MenuDesign::Classic : MenuDesign::Apparition),
            "The applied body design changes at the owned redraw, not at the source draft");
        Check(frame.menuDesignTransition.has_value() == transitionActive,
            "Changed Design keeps its separate50ms redraw and250ms input transition");
        if (name == "select0" || name == "wait49")
            Check(frame.menuDesignTransition == MenuDesignTransitionPhase::Rebuild,
                "The captured Design wait starts in its rebuild phase");
        if (name == "redraw50" || name == "wait249")
            Check(frame.menuDesignTransition == MenuDesignTransitionPhase::Cooldown,
                "The visible redraw keeps only the remaining Design cooldown");
        if (beforeSelect) {
            Check(!frame.menuOptionCallback && !frame.menuUseContinuation,
                "Pre-admission Design has no callback receipt");
            continue;
        }
        Check(frame.menuUseContinuation.has_value(),
            "Actual changed Design Select must retain the written Use receipt through its redraw");
        const auto current = *frame.menuUseContinuation;
        Check(current.kind == MenuOptionCallbackKind::Plain && current.sequence == parsed.lastSequence
            && current.admissionRevision == frame.localMenuEligibility->revision,
            "Changed-design receipt must retain its exact plain invocation and actor admission");
        if (name == "select0") first = current;
        if (name == "second-select250") {
            Check(first && current.revision == first->revision + 1 && current.sequence > first->sequence,
                "The held repeat after the changed Design begins a fresh same-value invocation");
            second = current;
        }
        const bool secondInvocation = name == "second-select250" || name == "wait499" || name == "ready500";
        Check(current == (secondInvocation ? second : first),
            "Changed-design redraw and callback expiry preserve the exact written receipt");
        Check(frame.menuOptionCallback.has_value() != ready,
            "Both changed-design Select calls have their own250ms expiry");
        if (!ready) Check(frame.menuOptionCallback == MenuOptionCallback{current.revision,
            MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown},
            "Changed Design uses its source plain cooldown alongside the visual transition");
        if (name == "redraw50") {
            Check(frame.menuOpening && frame.menuOpening->elapsedMilliseconds == 0,
                "The50ms redraw starts a separate visible row opening");
            redrawOpening = frame.menuOpening;
        }
        if (name == "wait249")
            Check(redrawOpening && frame.menuOpening && frame.menuOpening->revision == redrawOpening->revision
                && frame.menuOpening->elapsedMilliseconds == 199,
                "Row opening and source callback advance independently");
        if (name == "ready250")
            Check(redrawOpening && frame.menuOpening && frame.menuOpening->revision == redrawOpening->revision
                && frame.menuOpening->elapsedMilliseconds == 200,
                "The callback expires while the row opening continues");
    }

    for (const auto* name : ChangedDesignCancellationFiles) {
        const auto parsed = Read(directory / name);
        const auto preview = ReadBackendPreview(directory / name, &labels);
        Check(parsed.lastSequence == Frame(directory, "design-changed-classic", "select0").lastSequence,
            "A changed-design cancellation packet must describe the same original Select sequence");
        Check(parsed.snapshot->menuUseContinuation == preview.menuUseContinuation
            && parsed.snapshot->menuOptionCallback == preview.menuOptionCallback
            && parsed.snapshot->localMenuEligibility == preview.localMenuEligibility,
            "Both readers must agree after a changed-design scope change");
        Check(!parsed.snapshot->menuUseContinuation && !parsed.snapshot->menuOptionCallback,
            "Navigation, actor or focus changes cannot retain a changed-design receipt");
    }
    const auto moved = Read(directory / ChangedDesignCancellationFiles[0]);
    Check(moved.snapshot->selectedIndex == 1
        && moved.snapshot->navigationRevision != initial.navigationRevision,
        "Cursor cancellation changes navigation while MenuRevision remains an independent display epoch");
    const auto replaced = Read(directory / ChangedDesignCancellationFiles[1]);
    Check(replaced.snapshot->localMenuEligibility->revision != initial.localMenuEligibility->revision,
        "Actor cancellation replaces only the local admission owner");
    const auto returned = Read(directory / ChangedDesignCancellationFiles[3]);
    Check(returned.snapshot->isOpen && !returned.snapshot->menuUseContinuation
        && !returned.snapshot->menuOptionCallback,
        "Focus return at the original deadline cannot revive a canceled changed-design receipt");
}

void HeldUseRepeatsTwoPreferenceInvocationsOnlyAtTheirActualReadiness(const std::filesystem::path& directory) {
    for (const auto* prefix : Prefixes) {
        Router router;
        router.held = {70};
        const auto before = Frame(directory, prefix, "before");
        Check(router.Poll(before, 0) == runtime::Input::Select, "Current Use did not dispatch its first preference Select");
        const auto first = Frame(directory, prefix, "select0");
        router.input.CaptureUseDispatch(first.lastSequence);
        for (const auto& [stage, now] : std::array{
            std::pair{"select0", 0U}, std::pair{"wait179", 179U},
            std::pair{"redraw180", 180U}, std::pair{"wait429", 429U}}) {
            Check(!router.Poll(Frame(directory, prefix, stage), now), "Held preference Use bypassed its caller wait");
        }
        Check(router.Poll(Frame(directory, prefix, "ready430"), 430) == runtime::Input::Select,
            "Held preference Use failed to resume at its actual 430 ms readiness");
        const auto second = Frame(directory, prefix, "second-select430");
        router.input.CaptureUseDispatch(second.lastSequence);
        Check(!router.Poll(second, 430), "The second preference Select replayed without a wait");
        Check(!router.Poll(Frame(directory, prefix, "second-redraw610"), 610), "The second redraw granted early input");
        Check(router.Poll(Frame(directory, prefix, "second-ready860"), 860) == runtime::Input::Select,
            "The second owned preference invocation did not resume held Use");
    }
}

void UncapturedPreferenceUseCannotBorrowAnActualReadyReceipt(const std::filesystem::path& directory) {
    for (const auto* prefix : Prefixes) {
        Router router;
        router.held = {70};
        Check(router.Poll(Frame(directory, prefix, "before"), 0) == runtime::Input::Select,
            "Uncaptured test did not begin its native Select");
        // Observed backend work is not proof that this native command was written
        Check(!router.Poll(Frame(directory, prefix, "select0"), 0), "Uncaptured Use bypassed callback input locks");
        Check(!router.Poll(Frame(directory, prefix, "ready430"), 430), "Uncaptured Use borrowed a ready preference receipt");
    }
    Router router;
    router.held = {70};
    Check(router.Poll(Frame(directory, "design-same", "before"), 0) == runtime::Input::Select,
        "Uncaptured same-design test did not begin its actual Select");
    Check(!router.Poll(Frame(directory, "design-same", "select0"), 0),
        "Same-design Use repeated without a successful captured write");
    Check(!router.Poll(Frame(directory, "design-same", "ready250"), 250)
        && !router.Poll(Frame(directory, "design-same", "ready500"), 500),
        "Uncaptured same-design Use borrowed either ready receipt");

    Router changed;
    changed.held = {70};
    Check(changed.Poll(Frame(directory, "design-changed-classic", "before"), 0) == runtime::Input::Select,
        "Uncaptured changed-design test did not begin its actual Select");
    Check(!changed.Poll(Frame(directory, "design-changed-classic", "select0"), 0),
        "Uncaptured changed Design repeated without a successful captured write");
    Check(!changed.Poll(Frame(directory, "design-changed-classic", "ready250"), 250)
        && !changed.Poll(Frame(directory, "design-changed-classic", "ready500"), 500),
        "Uncaptured changed Design cannot borrow either ready receipt");
}

void HeldSameDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites(const std::filesystem::path& directory) {
    Router router;
    router.held = {70};
    const auto before = Frame(directory, "design-same", "before");
    Check(router.Poll(before, 0) == runtime::Input::Select,
        "Current held Use did not dispatch the first same-design Select");
    const auto first = Frame(directory, "design-same", "select0");
    router.input.CaptureUseDispatch(first.lastSequence);
    Check(!router.Poll(first, 0), "Captured same-design Select repeated in its own frame");
    Check(!router.Poll(Frame(directory, "design-same", "wait249"), 249),
        "Held same-design Use resumed before250ms");
    Check(router.Poll(Frame(directory, "design-same", "ready250"), 250) == runtime::Input::Select,
        "Held same-design Use did not resume at its first plain callback expiry");

    const auto second = Frame(directory, "design-same", "second-select250");
    router.input.CaptureUseDispatch(second.lastSequence);
    Check(!router.Poll(second, 250), "Second captured same-design Select repeated in its own frame");
    Check(!router.Poll(Frame(directory, "design-same", "wait499"), 499),
        "Second held same-design Use resumed before its own250ms wait");
    const auto ready = Frame(directory, "design-same", "ready500");
    Check(router.Poll(ready, 500) == runtime::Input::Select,
        "The second exact plain callback did not resume held Use at500ms");
    Check(!router.Poll(ready, 500), "One same-design readiness admitted a second Select in the same cycle");
}

void HeldChangedDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites(const std::filesystem::path& directory) {
    Router router;
    router.held = {70};
    const auto before = Frame(directory, "design-changed-classic", "before");
    Check(router.Poll(before, 0) == runtime::Input::Select,
        "Held Use did not dispatch the first changed-design Select");
    const auto first = Frame(directory, "design-changed-classic", "select0");
    router.input.CaptureUseDispatch(first.lastSequence);
    Check(!router.Poll(first, 0), "Captured changed Design repeated in its own frame");
    for (const auto& [stage, now] : std::array{std::pair{"wait49", 49U}, std::pair{"redraw50", 50U},
        std::pair{"wait249", 249U}}) {
        Check(!router.Poll(Frame(directory, "design-changed-classic", stage), now),
            "Held Use bypassed the changed-design rebuild or250ms caller wait");
    }
    Check(router.Poll(Frame(directory, "design-changed-classic", "ready250"), 250) == runtime::Input::Select,
        "The exact changed-design write did not resume at its first ready callback");
    const auto second = Frame(directory, "design-changed-classic", "second-select250");
    router.input.CaptureUseDispatch(second.lastSequence);
    Check(!router.Poll(second, 250), "The second same-value Design Select replayed in its own frame");
    Check(!router.Poll(Frame(directory, "design-changed-classic", "wait499"), 499),
        "The second Design callback resumed before its own250ms wait");
    const auto ready = Frame(directory, "design-changed-classic", "ready500");
    Check(router.Poll(ready, 500) == runtime::Input::Select,
        "The second exact Design write did not resume at500ms");
    Check(!router.Poll(ready, 500), "One changed-design readiness admitted a second Select in the same cycle");
}

void ChangedDesignScopeChangesCancelTheCapturedHeldUse(const std::filesystem::path& directory) {
    const auto begin = [&] {
        Router router;
        router.held = {70};
        Check(router.Poll(Frame(directory, "design-changed-classic", "before"), 0) == runtime::Input::Select,
            "Changed-design cancellation did not dispatch its original Select");
        const auto selected = Frame(directory, "design-changed-classic", "select0");
        router.input.CaptureUseDispatch(selected.lastSequence);
        Check(!router.Poll(selected, 0), "Changed-design cancellation bypassed its callback");
        return router;
    };
    {
        auto router = begin();
        const auto moved = Read(directory / ChangedDesignCancellationFiles[0]);
        Check(!router.Poll(moved, 49) && !router.Poll(moved, 250),
            "Cursor movement revived a changed-design Use after its deadline");
    }
    {
        auto router = begin();
        const auto replaced = Read(directory / ChangedDesignCancellationFiles[1]);
        Check(!router.Poll(replaced, 1) && !router.Poll(replaced, 250),
            "Actor replacement revived a changed-design Use after its deadline");
    }
    {
        auto router = begin();
        router.foreground = false;
        const auto hidden = Read(directory / ChangedDesignCancellationFiles[2]);
        Check(!router.Poll(hidden, 1), "Focus loss replayed the changed-design held Use");
        router.foreground = true;
        const auto returned = Read(directory / ChangedDesignCancellationFiles[3]);
        Check(!router.Poll(returned, 250), "Focus return revived the canceled changed-design receipt");
    }
}

void SameDesignScopeChangesCancelHeldUseAcrossItsOriginalDeadline(const std::filesystem::path& directory) {
    {
        Router router;
        router.held = {70};
        Check(router.Poll(Frame(directory, "design-same", "before"), 0) == runtime::Input::Select,
            "Navigation cancellation graph did not dispatch its original Select");
        router.input.CaptureUseDispatch(Frame(directory, "design-same", "select0").lastSequence);
        Check(!router.Poll(Frame(directory, "design-same", "select0"), 0),
            "Navigation graph bypassed the same-design callback");
        Check(!router.Poll(Frame(directory, "design-same", "wait249"), 249),
            "Navigation graph repeated before the callback deadline");
        Check(!router.Poll(Frame(directory, "design-nav", "moved249"), 249)
            && !router.Poll(Frame(directory, "design-nav", "moved249"), 250),
            "Cursor movement revived the original same-design held Use");
    }
    {
        Router router;
        router.held = {70};
        Check(router.Poll(Frame(directory, "design-same", "before"), 0) == runtime::Input::Select,
            "Owner cancellation graph did not dispatch its original Select");
        router.input.CaptureUseDispatch(Frame(directory, "design-same", "select0").lastSequence);
        Check(!router.Poll(Frame(directory, "design-same", "select0"), 0),
            "Owner graph bypassed the same-design callback");
        const auto replaced = Frame(directory, "design-owner", "replaced0");
        Check(!router.Poll(replaced, 1) && !router.Poll(replaced, 250),
            "A replacement actor revived the original same-design held Use");
    }
    {
        Router router;
        router.held = {70};
        Check(router.Poll(Frame(directory, "design-same", "before"), 0) == runtime::Input::Select,
            "Focus cancellation graph did not dispatch its original Select");
        router.input.CaptureUseDispatch(Frame(directory, "design-same", "select0").lastSequence);
        Check(!router.Poll(Frame(directory, "design-same", "select0"), 0),
            "Focus graph bypassed the same-design callback");
        router.foreground = false;
        const auto hidden = Frame(directory, "design-focus", "hidden0");
        Check(!router.Poll(hidden, 1),
            "Focus loss replayed held same-design Use");
        router.foreground = true;
        Check(!router.Poll(Frame(directory, "design-focus", "return250"), 250),
            "Focus return revived the canceled same-design held Use");
    }
}
}

int main(int argc, char** argv) {
    using namespace iw::overlay::tests::preferenceUseGraph;
    if (argc != 2) return 2;
    int failed = 0;
    const std::filesystem::path directory(argv[1]);
    for (const auto& [name, run] : std::array{
        std::pair{"ActualPreferencePacketsKeepDelayedPaintAndOneOwnedCallbackClock", ActualPreferencePacketsKeepDelayedPaintAndOneOwnedCallbackClock},
        std::pair{"ActualSameDesignPacketsKeepOnePlainClockAndExactReceipts", ActualSameDesignPacketsKeepOnePlainClockAndExactReceipts},
        std::pair{"ActualChangedDesignPacketsSeparateRedrawEpochFromSourceVisit", ActualChangedDesignPacketsSeparateRedrawEpochFromSourceVisit},
        std::pair{"HeldUseRepeatsTwoPreferenceInvocationsOnlyAtTheirActualReadiness", HeldUseRepeatsTwoPreferenceInvocationsOnlyAtTheirActualReadiness},
        std::pair{"UncapturedPreferenceUseCannotBorrowAnActualReadyReceipt", UncapturedPreferenceUseCannotBorrowAnActualReadyReceipt},
        std::pair{"HeldSameDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites", HeldSameDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites},
        std::pair{"SameDesignScopeChangesCancelHeldUseAcrossItsOriginalDeadline", SameDesignScopeChangesCancelHeldUseAcrossItsOriginalDeadline},
        std::pair{"HeldChangedDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites", HeldChangedDesignUseRepeatsOnlyAfterItsTwoCapturedPlainWrites},
        std::pair{"ChangedDesignScopeChangesCancelTheCapturedHeldUse", ChangedDesignScopeChangesCancelTheCapturedHeldUse}}) {
        try { run(directory); std::cout << "PASS " << name << '\n'; }
        catch (const std::exception& error) { ++failed; std::cout << "FAIL " << name << ": " << error.what() << '\n'; }
    }
    std::cout << 9 - failed << "/9 actual preference Use graph checks passed\n";
    return failed ? 1 : 0;
}
