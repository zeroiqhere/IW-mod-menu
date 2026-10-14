#include "../use-monitor/UseContinuationGraphSupport.h"
#include <array>
#include <iostream>

namespace iw::overlay::tests::useGraph {
namespace {
struct Expected {
  const char *name;
  std::uint64_t ack;
  bool wait;
  int callback;
  bool receipt;
  const char *value;
};
const std::array expected{
    Expected{"palette-before0.json", 12, false, 0, false, "OFF"},
    Expected{"palette-use0.json", 13, false, 1, true, "ON"},
    Expected{"palette-use179.json", 13, false, 1, true, "ON"},
    Expected{"palette-use180.json", 13, false, 2, true, "ON"},
    Expected{"palette-use429.json", 13, false, 2, true, "ON"},
    Expected{"palette-ready430.json", 13, false, 0, true, "ON"},
    Expected{"palette-noop430.json", 14, true, 0, false, "ON"},
    Expected{"palette-noop479.json", 14, true, 0, false, "ON"},
    Expected{"palette-noop480.json", 14, false, 0, false, "ON"},
    Expected{"quick-exit-before0.json", 17, false, 0, false, "OFF"},
    Expected{"quick-exit-noop0.json", 18, true, 0, false, "OFF"},
    Expected{"quick-exit-noop49.json", 18, true, 0, false, "OFF"},
    Expected{"quick-exit-noop50.json", 18, false, 0, false, "OFF"},
    Expected{"root-before0.json", 2, false, 0, false, ">"},
    Expected{"root-noop0.json", 3, true, 0, false, ">"},
    Expected{"root-noop49.json", 3, true, 0, false, ">"},
    Expected{"root-noop50.json", 3, false, 0, false, ">"}};
void ActualNoopInventoryKeepsLiteralClocksOwnershipAndUnchangedValues(
    const std::filesystem::path &directory) {
  std::set<std::string> actual, wanted;
  const auto labels = runtime::DescribeOpeningActions(Bindings());
  for (const auto &entry : std::filesystem::directory_iterator(directory)) {
    Check(entry.is_regular_file() && entry.path().extension() == ".json",
          "No-op inventory contains an unexpected entry");
    actual.insert(entry.path().filename().string());
  }
  for (const auto &item : expected) {
    wanted.insert(item.name);
    const auto state = Read(directory / item.name);
    const auto &frame = *state.snapshot;
    const auto preview = ReadBackendPreview(directory / item.name, &labels);
    Check(state.lastSequence == item.ack,
          "Actual no-op changed original acknowledgment");
    Check(frame.menuMonitorWait == preview.menuMonitorWait &&
              frame.menuUseContinuation == preview.menuUseContinuation &&
              frame.menuOptionCallback == preview.menuOptionCallback &&
              frame.localMenuEligibility == preview.localMenuEligibility,
          "Readers disagree about actual no-op ownership");
    Check(frame.menuMonitorWait.has_value() == item.wait,
          "Actual outer50ms no-op boundary changed");
    if (item.wait)
      Check(*frame.menuMonitorWait ==
                MenuMonitorWait{1, MenuMonitorWaitKind::ChoiceNoop},
            "No-op borrowed slider180ms metadata");
    Check(
        frame.menuUseContinuation.has_value() == item.receipt,
        "No-op retained old callback permission or erased a ready predecessor");
    if (item.receipt)
      Check(*frame.menuUseContinuation ==
                MenuUseContinuation{1, 13, 10, MenuOptionCallbackKind::Boolean},
            "No-op predecessor lost original Boolean identity");
    Check(frame.menuOptionCallback.has_value() == (item.callback != 0),
          "No-op graph changed original Boolean phase");
    if (item.callback)
      Check(*frame.menuOptionCallback ==
                MenuOptionCallback{1, MenuOptionCallbackKind::Boolean,
                                   item.callback == 1
                                       ? MenuOptionCallbackPhase::Pending
                                       : MenuOptionCallbackPhase::Cooldown},
            "No-op graph changed callback revision or kind");
    // The root proof belongs to the declared source link, not the local Connect
    // extension
    if (std::string(item.name).starts_with("root-")) {
      Check(frame.selectedIndex == 1 &&
                frame.rows[1].label == "Basic Scripts" &&
                frame.navigationRevision == 2 && frame.menuRevision == 2,
            "Root no-op packet lost its exact source-declared Basic Scripts "
            "link");
    }
    Check(frame.rows[frame.selectedIndex].value == item.value,
          "Nonslider key adjusted a UI value");
    Check(frame.isOpen &&
              frame.localMenuEligibility->controlsDisabled ==
                  (item.callback != 0) &&
              runtime::InputModeForSnapshot(frame) ==
                  (item.callback ? runtime::InputMode::MenuLocked
                                 : runtime::InputMode::Menu),
          "No-op fabricated visibility, disabled controls or a modal");
    Check(frame.navigationRevision == preview.navigationRevision &&
              frame.menuRevision == preview.menuRevision &&
              frame.selectedIndex == preview.selectedIndex &&
              frame.rows.size() == preview.rows.size(),
          "Reader changed no-op navigation");
    for (std::size_t i = 0; i < frame.rows.size(); ++i)
      Check(frame.rows[i].label == preview.rows[i].label &&
                frame.rows[i].value == preview.rows[i].value &&
                frame.rows[i].enabled == preview.rows[i].enabled,
            "Reader changed actual no-op row presentation");
  }
  Check(actual == wanted, "Separate17 packet no-op inventory changed silently");
}
void ActualBooleanReadyChoiceStartsOnlyTheOuterFiftyMillisecondPause(
    const std::filesystem::path &directory) {
  Router router;
  const auto before = Read(directory / "palette-before0.json");
  router.Poll(before, 0);
  router.held = {70};
  Check(router.Poll(before, 0) == runtime::Input::Select,
        "Actual palette predecessor failed to dispatch");
  router.input.CaptureUseDispatch(13);
  Check(!router.Poll(Read(directory / "palette-use0.json"), 0),
        "Palette callback skipped its owned wait");
  router.held = {52};
  const std::array phases{std::pair{"palette-use179.json", 179u},
                          std::pair{"palette-use180.json", 180u},
                          std::pair{"palette-use429.json", 429u}};
  for (const auto &[name, now] : phases)
    Check(!router.Poll(Read(directory / name), now),
          "Nonslider button bypassed Boolean callback");
  Check(router.Poll(Read(directory / "palette-ready430.json"), 430) ==
            runtime::Input::NextChoice,
        "Actual ready palette failed to resume source slot");
  const auto wait = Read(directory / "palette-noop430.json");
  Check(wait.snapshot->menuMonitorWait->kind ==
                MenuMonitorWaitKind::ChoiceNoop &&
            !wait.snapshot->menuUseContinuation,
        "Nonslider source branch reused a previous wait or ready receipt");
  Check(!router.Poll(wait, 430) &&
            !router.Poll(Read(directory / "palette-noop479.json"), 479),
        "Nonslider outer pause ended before50ms");
  Check(!Read(directory / "palette-noop480.json").snapshot->menuMonitorWait,
        "No-op pause incorrectly retained slider180ms");
  // General held mechanical continuation after this phase remains a separate
  // unproved path
}
void ActualRootAndBooleanRowsKeepLegacyFreshChoiceWithoutChangingTheirOptions(
    const std::filesystem::path &directory) {
  for (const char *prefix : {"root", "quick-exit"}) {
    const auto path = [&](const char *suffix) {
      return directory / (std::string(prefix) + suffix + ".json");
    };
    const auto before = Read(path("-before0"));
    Router router;
    router.Poll(before, 0);
    router.held = {52};
    Check(!before.snapshot->menuUseContinuation,
          "Producer-only no-op frame invented a causal Use predecessor");
    Check(router.Poll(before, 0) == runtime::Input::NextChoice,
          "Ordinary fresh nonslider slot did not dispatch");
    const auto start = Read(path("-noop0")), ready = Read(path("-noop50"));
    Check(start.snapshot->rows[start.snapshot->selectedIndex].value ==
                  before.snapshot->rows[before.snapshot->selectedIndex].value &&
              start.snapshot->navigationRevision ==
                  before.snapshot->navigationRevision &&
              start.snapshot->menuRevision == before.snapshot->menuRevision,
          "No-op altered a root link or Boolean option");
    Check(!router.Poll(start, 0) && !router.Poll(Read(path("-noop49")), 49),
          "Fresh no-op escaped its outer pause");
    Check(!ready.snapshot->menuMonitorWait,
          "Actual nonslider branch did not retire at50ms");
  }
}
} // namespace
} // namespace iw::overlay::tests::useGraph
int main(int argc, char **argv) {
  if (argc != 2)
    return 2;
  using namespace iw::overlay::tests::useGraph;
  const std::array tests{
      std::pair{
          "ActualNoopInventoryKeepsLiteralClocksOwnershipAndUnchangedValues",
          ActualNoopInventoryKeepsLiteralClocksOwnershipAndUnchangedValues},
      std::pair{
          "ActualBooleanReadyChoiceStartsOnlyTheOuterFiftyMillisecondPause",
          ActualBooleanReadyChoiceStartsOnlyTheOuterFiftyMillisecondPause},
      std::pair{
          "ActualRootAndBooleanRowsKeepLegacyFreshChoiceWithoutChangingTheirOpt"
          "ions",
          ActualRootAndBooleanRowsKeepLegacyFreshChoiceWithoutChangingTheirOptions}};
  int failed = 0;
  for (const auto &[name, run] : tests) {
    try {
      run(argv[1]);
      std::cout << "PASS " << name << '\n';
    } catch (const std::exception &error) {
      ++failed;
      std::cout << "FAIL " << name << ": " << error.what() << '\n';
    }
  }
  std::cout << tests.size() - failed << '/' << tests.size()
            << " actual no-op choice graph checks passed\n";
  return failed ? 1 : 0;
}
