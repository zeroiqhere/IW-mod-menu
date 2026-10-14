#include "ControlLabels.h"
#include "ScrollSpacerCounts.h"
#include "OpeningPolicyScope.h"
#include "TestSupport.h"
#include <fstream>
#include <map>
#include <set>

namespace iw::overlay::tests {
void BackendOpeningControlsPreserveActualPolicyRowsAndCapturedEpochs(const std::filesystem::path &directory) {
    // Older immutable fixture sets have no configured policy and retain their original checks
    if (!std::filesystem::exists(directory / "open-controls-default.json")) {
        return;
    }
    const runtime::Bindings bindings{{2},  {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1},
                                     {38}, {40},    {71},    {69},    {16}, {17}, {32}};
    const auto labels = runtime::DescribeOpeningActions(bindings);
    struct Expected {
        const char *name;
        std::uint64_t navigation, menu, sequence, policy;
        std::vector<OpeningAction> actions;
        std::size_t selected, count;
        int onRow;
        std::optional<MenuPreferenceRefreshPhase> refresh;
        bool open = true;
    };
    using Action = OpeningAction;
    using Phase = MenuPreferenceRefreshPhase;
    const std::vector<Action> original{Action::Aim, Action::Melee};
    const std::vector<Action> changed{Action::Use, Action::Melee};
    const std::vector<Action> three{Action::Aim, Action::Melee, Action::Frag};
    const bool dual = std::filesystem::exists(directory / "quick-controls-default.json");
    const Expected profiles[] = {
        {"default", 8, 6, 6, 1, original, 0, 16, 8, {}},
        {"change-pending", 18, 6, 17, 2, changed, 11, 16, 8, Phase::Pending},
        {"change-pre-refresh", 18, 6, 17, 2, changed, 11, 16, 8, Phase::Pending},
        {"change-cooldown", 18, 7, 17, 2, changed, 11, 16, 11, Phase::Cooldown},
        {"change-pre-unlock", 18, 7, 17, 2, changed, 11, 16, 11, Phase::Cooldown},
        {"change-unlocked", 18, 7, 17, 2, changed, 11, 16, 11, {}},
        {"closed", 19, 8, 18, 2, changed, 11, 16, 11, {}, false},
        {"hidden", 18, 7, 17, 2, changed, 11, 16, 11, {}, false},
        {"none-no-op-pending", dual ? 26u : 25u, 8, 28, 1, original, 3, 17, 3, Phase::Pending},
        {"none-truncate-pending", dual ? 44u : 42u, 10, 49, 3, {Action::Aim}, 3, 17, 8, Phase::Pending},
        {"none-truncate-unlocked", dual ? 44u : 42u, 11, 49, 3, {Action::Aim}, 3, 17, 3, {}},
        {"offhand-conflict-pending", dual ? 39u : 37u, 9, 43, 2, three, 14, 17, 8, Phase::Pending},
        {"quick-candidate", dual ? 26u : 24u, 8, 27, 1, original, 0, 17, 3, {}},
        {dual ? "quick-applied" : "quick-rejected", dual ? 27u : 24u, dual ? 9u : 8u, 28, 1, original, 0, 17, 3, Phase::Cooldown},
        {"reselect-pending", 15, 6, 14, 1, original, 8, 16, 8, Phase::Pending},
        {"selector-same-menu", 8, 6, 7, 1, original, 0, 16, 8, dual ? Phase::Selector : Phase::Cooldown},
        {"selector-same-slot", 9, 7, 9, 1, original, 1, 16, 8, Phase::Cooldown},
        {"slot-draft", 9, 6, 8, 1, original, 1, 16, 8, {}},
        {"slot2-applied", dual ? 10u : 9u, 7, 9, 1, original, 1, 17, 8, {}},
        {"slot3-applied", dual ? 11u : 9u, 8, 11, 1, original, 1, 17, 3, {}},
        {"third-action-pending", dual ? 21u : 20u, 7, 22, 2, three, 13, 17, 3, Phase::Pending},
        {"unavailable-actions", 8, 6, 6, 1, original, 0, 16, 8, {}}};
    std::map<std::string, std::shared_ptr<const MenuSnapshot>> snapshots;
    for (const auto &expected : profiles) {
        const auto path = directory / (std::string("open-controls-") + expected.name + ".json");
        Check(std::filesystem::is_regular_file(path) &&
                  std::filesystem::file_size(path) <= runtime::MaximumFileBytes,
              "An actual opening-controls packet is missing or oversized");
        std::ifstream file(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
        auto privateLabels = labels;
        const bool partial = std::string_view(expected.name) == "unavailable-actions";
        if (partial) {
            for (unsigned id : {0u, 1u, 7u, 9u, 10u, 11u, 12u}) {
                privateLabels[id].clear();
            }
        }
        const auto state = runtime::ParseState(bytes, 42, nullptr, nullptr, nullptr, nullptr, nullptr,
                                               std::nullopt, nullptr, nullptr, &privateLabels);
        const auto &model = *state.snapshot;
        Check(model.title == "Open Controls" && model.isOpen == expected.open && !model.isRootMenu &&
                  model.navigationRevision == expected.navigation && model.menuRevision == expected.menu &&
                  state.lastSequence == expected.sequence && model.selectedIndex == expected.selected &&
                  model.rows.size() == expected.count &&
                  model.mainOpeningControls == MainOpeningControls{expected.policy, expected.actions} &&
                  model.menuPreferenceRefresh == expected.refresh && !model.menuOpening,
              "Actual policy, cursor, page identity or refresh timing changed");
        Check(runtime::InputModeForSnapshot(model) ==
                  (expected.refresh ? runtime::InputMode::MenuLocked : runtime::InputMode::Menu),
              "Opening policy introduced an input mode or failed to lock a refresh");
        Check(model.rows[2].isSpacer && model.rows[2].label.empty() && model.rows[2].value.empty() &&
                  !model.rows[2].enabled && !model.rows[2].openingAction,
              "Actual source spacer gained a label, key action or capability claim");
        const std::size_t first = expected.count == 17 ? 4 : 3;
        if (first == 4) {
            Check(model.rows[3].label == "None" && model.rows[3].enabled && !model.rows[3].openingAction,
                  "The optional empty slot action was not a selectable ordinary row");
        }
        for (unsigned id = 0; id < OpeningActionCount; ++id) {
            const auto &row = model.rows[first + id];
            const bool available = !privateLabels[id].empty();
            Check(row.openingAction == static_cast<Action>(id) && !row.isSpacer && row.enabled == available &&
                      row.label ==
                          (available ? privateLabels[id] : OpeningActionCaption(static_cast<Action>(id))) &&
                      row.value == (available
                                        ? (static_cast<int>(first + id) == expected.onRow ? "ON" : "OFF")
                                        : "UNAVAILABLE"),
                  "Actual saved-action caption, availability or selected-slot radio value changed");
        }
        snapshots.emplace(expected.name, state.snapshot);
    }
    // Closed projection retains the committed tuple while fresh aggregate edges still own opening
    runtime::NativeInputRouter router(bindings);
    std::set<int> held;
    const auto poll = [&](const MenuSnapshot &model, std::uint64_t time) {
        const auto skips = runtime::CountScrollSpacers(model);
        return router.Poll(
            true, model.isOpen, false, [&](int key) { return held.contains(key); }, time,
            model.navigationRevision, false, false, runtime::InputModeForSnapshot(model), 120,
            model.menuRevision, &*model.mainOpeningControls, skips.up, skips.down,
            model.openingPolicies ? &*model.openingPolicies : nullptr);
    };
    const auto &closed = *snapshots.at("closed");
    Check(!poll(closed, 0), "Closed actual policy inherited an initial press");
    held = {70};
    Check(!poll(closed, 20), "Partial actual Use chord opened");
    held.insert(86);
    Check(poll(closed, 40) == runtime::Input::Toggle, "Actual committed chord did not open");
    Check(!poll(closed, 60), "Actual aggregate hold repeated Toggle");
    // The command owns both revisions from this parsed snapshot instead of a current producer value
    const auto command = Json::parse(runtime::CommandJson(
        42, 21, runtime::Input::Select, closed.navigationRevision, runtime::ObservedOpeningControlsRevision(closed)));
    Check(command.at("navigationRevision") == 19 && command.at("openingControlsRevision") == 2,
          "Actual mutation command did not serialize both observed revisions");
    const auto &pending = *snapshots.at("change-pending");
    const auto &cooldown = *snapshots.at("change-cooldown");
    const auto &unlocked = *snapshots.at("change-unlocked");
    held.clear();
    poll(pending, 100);
    held = {70};
    Check(!poll(pending, 120) && !poll(cooldown, 280) && !poll(unlocked, 530) && !poll(unlocked, 550),
          "A held select during an actual refresh replayed after unlock");
    held.clear();
    poll(unlocked, 570);
    held = {70};
    Check(poll(unlocked, 590) == runtime::Input::Select, "Fresh select after actual refresh was lost");
}
} // namespace iw::overlay::tests
