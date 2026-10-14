#include "MechanicalGraphSupport.h"
#include <array>

namespace iw::overlay::tests::mechanicalGraph {
namespace {
struct Expected {
  const char *name;
  std::uint64_t ack, navigation, menu, admission;
  bool open, root;
  std::uint64_t wait, receipt;
  MenuMonitorWaitKind kind;
  std::size_t index, rows;
  const char *label, *value;
  bool enabled = true;
};
constexpr auto Choice = MenuMonitorWaitKind::Choice;
constexpr auto Noop = MenuMonitorWaitKind::ChoiceNoop;
constexpr auto Back = MenuMonitorWaitKind::Back;
// Literal source outcomes keep reader agreement from serving as the only oracle
const std::array expected{
    Expected{"choice-before0.json", 12, 14, 6, 10, true, false, 0, 0, Choice, 1,
             12, "Bool Display", "Boxes"},
    Expected{"choice-next0.json", 1001, 14, 6, 10, true, false, 1, 1, Choice, 1,
             12, "Bool Display", "Text"},
    Expected{"choice-next179.json", 1001, 14, 6, 10, true, false, 1, 1, Choice,
             1, 12, "Bool Display", "Text"},
    Expected{"choice-ready180.json", 1001, 14, 6, 10, true, false, 0, 1, Choice,
             1, 12, "Bool Display", "Text"},
    Expected{"choice-previous180.json", 1002, 14, 6, 10, true, false, 2, 2,
             Choice, 1, 12, "Bool Display", "Boxes"},
    Expected{"choice-previous359.json", 1002, 14, 6, 10, true, false, 2, 2,
             Choice, 1, 12, "Bool Display", "Boxes"},
    Expected{"choice-ready360.json", 1002, 14, 6, 10, true, false, 0, 2, Choice,
             1, 12, "Bool Display", "Boxes"},
    Expected{"choice-use360.json", 1003, 14, 6, 10, true, false, 0, 0, Choice,
             1, 12, "Bool Display", "Boxes"},
    Expected{"choice-use609.json", 1003, 14, 6, 10, true, false, 0, 0, Choice,
             1, 12, "Bool Display", "Boxes"},
    Expected{"choice-use610.json", 1003, 14, 6, 10, true, false, 0, 0, Choice,
             1, 12, "Bool Display", "Boxes"},
    Expected{"noop-before0.json", 10, 12, 6, 10, true, false, 0, 0, Noop, 0, 37,
             "Red", "OFF"},
    Expected{"noop-next0.json", 1001, 12, 6, 10, true, false, 1, 1, Noop, 0, 37,
             "Red", "OFF"},
    Expected{"noop-next49.json", 1001, 12, 6, 10, true, false, 1, 1, Noop, 0,
             37, "Red", "OFF"},
    Expected{"noop-ready50.json", 1001, 12, 6, 10, true, false, 0, 1, Noop, 0,
             37, "Red", "OFF"},
    Expected{"noop-repeat50.json", 1002, 12, 6, 10, true, false, 2, 2, Noop, 0,
             37, "Red", "OFF"},
    Expected{"noop-repeat99.json", 1002, 12, 6, 10, true, false, 2, 2, Noop, 0,
             37, "Red", "OFF"},
    Expected{"noop-ready100.json", 1002, 12, 6, 10, true, false, 0, 2, Noop, 0,
             37, "Red", "OFF"},
    Expected{"back-before0.json", 11, 13, 6, 10, true, false, 0, 0, Back, 0, 12,
             "Design", "Apparition"},
    Expected{"back-child0.json", 1001, 15, 8, 10, true, false, 1, 1, Back, 6, 7,
             "Menu Preferences", ">"},
    Expected{"back-child249.json", 1001, 15, 8, 10, true, false, 1, 1, Back, 6,
             7, "Menu Preferences", ">"},
    Expected{"back-child250.json", 1001, 15, 8, 10, true, false, 0, 1, Back, 6,
             7, "Menu Preferences", ">"},
    Expected{"back-parent250.json", 1002, 17, 10, 10, true, true, 2, 2, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"back-parent499.json", 1002, 17, 10, 10, true, true, 2, 2, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"back-parent500.json", 1002, 17, 10, 10, true, true, 0, 2, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"back-root500.json", 1003, 18, 11, 10, false, true, 3, 3, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"back-root749.json", 1003, 18, 11, 10, false, true, 3, 3, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"back-root750.json", 1003, 18, 11, 10, false, true, 0, 3, Back, 2,
             8, "Menu Customization", ">"},
    Expected{"close-before0.json", 24, 25, 7, 10, true, false, 0, 0, Back, 0,
             12, "Design", "Apparition"},
    Expected{"close-back0.json", 1001, 27, 9, 10, true, false, 1, 1, Back, 6, 7,
             "Menu Preferences", ">"},
    Expected{"close-ready250.json", 1001, 27, 9, 10, true, false, 0, 1, Back, 6,
             7, "Menu Preferences", ">"},
    Expected{"close-ready339.json", 1001, 27, 9, 10, true, false, 0, 1, Back, 6,
             7, "Menu Preferences", ">"},
    Expected{"close-ready340.json", 1001, 27, 9, 10, true, false, 0, 1, Back, 6,
             7, "Menu Preferences", ">"},
    Expected{"close-child340.json", 1002, 28, 10, 10, false, false, 2, 2, Back,
             6, 7, "Menu Preferences", ">"},
    Expected{"close-child589.json", 1002, 28, 10, 10, false, false, 2, 2, Back,
             6, 7, "Menu Preferences", ">"},
    Expected{"close-child590.json", 1002, 28, 10, 10, false, false, 0, 2, Back,
             6, 7, "Menu Preferences", ">"},
    Expected{"jump-before0.json", 1, 1, 3, 11, true, true, 0, 0, Back, 0, 3,
             "Respawn", "UNAVAILABLE", false},
    Expected{"jump0.json", 1001, 2, 4, 11, false, true, 1, 1, Back, 0, 3,
             "Respawn", "UNAVAILABLE", false},
    Expected{"jump249.json", 1001, 2, 4, 11, false, true, 1, 1, Back, 0, 3,
             "Respawn", "UNAVAILABLE", false},
    Expected{"jump250.json", 1001, 2, 4, 11, false, true, 0, 1, Back, 0, 3,
             "Respawn", "UNAVAILABLE", false},
    Expected{"withdraw-before0.json", 11, 13, 6, 10, true, false, 0, 0, Choice,
             0, 12, "Design", "Apparition"},
    Expected{"withdraw-choice0.json", 1001, 13, 6, 10, true, false, 1, 1,
             Choice, 0, 12, "Design", "Classic"},
    Expected{"withdraw-ready180.json", 1001, 13, 6, 10, true, false, 0, 1,
             Choice, 0, 12, "Design", "Classic"},
    Expected{"withdraw-focus180.json", 1001, 13, 6, 10, false, false, 0, 0,
             Choice, 0, 12, "Design", "Classic"},
    Expected{"withdraw-return180.json", 1001, 13, 6, 10, true, false, 0, 0,
             Choice, 0, 12, "Design", "Classic"},
    Expected{"withdraw-nav180.json", 1002, 14, 6, 10, true, false, 0, 0, Choice,
             1, 12, "Bool Display", "Boxes"},
    Expected{"rollback-before0.json", 11, 13, 6, 10, true, false, 0, 0, Choice,
             0, 12, "Design", "Apparition"},
    Expected{"rollback-choice0.json", 1001, 13, 6, 10, true, false, 1, 1,
             Choice, 0, 12, "Design", "Classic"},
    Expected{"rollback100.json", 1001, 13, 6, 10, true, false, 1, 1, Choice, 0,
             12, "Design", "Classic"},
    Expected{"rollback10.json", 1001, 13, 6, 10, true, false, 1, 1, Choice, 0,
             12, "Design", "Classic"},
    Expected{"rollback189.json", 1001, 13, 6, 10, true, false, 1, 1, Choice, 0,
             12, "Design", "Classic"},
    Expected{"rollback190.json", 1001, 13, 6, 10, true, false, 0, 1, Choice, 0,
             12, "Design", "Classic"},
    Expected{"ammo-before0.json", 8, 9, 4, 10, true, false, 0, 0, Choice, 5, 31,
             "Unlimited Ammo", "UNAVAILABLE", false},
    Expected{"ammo-choice0.json", 1001, 9, 4, 10, true, false, 1, 1, Choice, 5,
             31, "Unlimited Ammo", "UNAVAILABLE", false},
    Expected{"ammo-choice179.json", 1001, 9, 4, 10, true, false, 1, 1, Choice,
             5, 31, "Unlimited Ammo", "UNAVAILABLE", false},
    Expected{"ammo-ready180.json", 1001, 9, 4, 10, true, false, 0, 1, Choice, 5,
             31, "Unlimited Ammo", "UNAVAILABLE", false}};
} // namespace
void ActualMechanicalInventoryMatchesLiteralOraclesAndBothReaders(
    const std::filesystem::path &directory) {
  std::set<std::string> actual, names;
  for (const auto &entry : std::filesystem::directory_iterator(directory)) {
    Check(entry.is_regular_file() && entry.path().extension() == ".json",
          "Mechanical graph contains an unexpected entry");
    actual.insert(entry.path().filename().string());
  }
  const auto labels = runtime::DescribeOpeningActions(useGraph::Bindings());
  for (const auto &item : expected) {
    names.insert(item.name);
    const auto state = Read(directory / item.name);
    const auto &frame = *state.snapshot;
    const auto preview = ReadBackendPreview(directory / item.name, &labels);
    Check(state.lastSequence == item.ack &&
              frame.navigationRevision == item.navigation &&
              frame.menuRevision == item.menu &&
              frame.localMenuEligibility->revision == item.admission,
          "Actual mechanical source epochs changed");
    Check(frame.isOpen == item.open && frame.isRootMenu == item.root &&
              frame.selectedIndex == item.index &&
              frame.rows.size() == item.rows,
          "Actual resulting page or saved cursor changed");
    const auto &row = frame.rows[item.index];
    Check(
        row.label == item.label && row.value == item.value &&
            row.enabled == item.enabled,
        "Actual source choice altered the wrong visible draft or availability");
    Check(frame.menuMonitorWait.has_value() == (item.wait != 0) &&
              frame.menuMonitorContinuation.has_value() == (item.receipt != 0),
          "Actual mechanical invocation changed its normal retirement");
    if (item.wait)
      Check(*frame.menuMonitorWait == MenuMonitorWait{item.wait, item.kind},
            "Actual wait lost its own revision or source kind");
    if (item.receipt)
      Check(*frame.menuMonitorContinuation ==
                MenuMonitorContinuation{item.receipt, item.ack, item.admission,
                                        item.kind},
            "Actual receipt lost causal sequence proof");
    const auto name = std::string(item.name);
    const bool jump = name.starts_with("jump"),
               use = name.starts_with("choice-use");
    Check(frame.menuSurface ==
                  (jump ? MenuSurface::Quick : MenuSurface::Main) &&
              frame.localMenuEligibility->life ==
                  (jump ? PlayerLifeState::Dead : PlayerLifeState::Alive),
          "Actual source actor or surface changed");
    Check(frame.menuUseContinuation.has_value() == use,
          "Mechanical receipt overlapped another invocation");
    if (use)
      Check(*frame.menuUseContinuation ==
                MenuUseContinuation{1, 1003, 10, MenuOptionCallbackKind::Plain},
            "Actual subsequent Use lost its own sequence");
    const bool callback = use && name != "choice-use610.json";
    Check(frame.menuOptionCallback.has_value() == callback,
          "Actual callback retirement changed");
    if (callback)
      Check(*frame.menuOptionCallback ==
                MenuOptionCallback{1, MenuOptionCallbackKind::Plain,
                                   MenuOptionCallbackPhase::Cooldown},
            "Actual subsequent Use borrowed mechanical time");
    // Both independent readers must preserve the same copied state and visible
    // rows
    Check(frame.menuMonitorContinuation == preview.menuMonitorContinuation &&
              frame.menuMonitorWait == preview.menuMonitorWait &&
              frame.menuUseContinuation == preview.menuUseContinuation &&
              frame.menuOptionCallback == preview.menuOptionCallback &&
              frame.localMenuEligibility == preview.localMenuEligibility &&
              frame.navigationRevision == preview.navigationRevision &&
              frame.menuRevision == preview.menuRevision &&
              frame.isOpen == preview.isOpen &&
              frame.isRootMenu == preview.isRootMenu &&
              frame.selectedIndex == preview.selectedIndex &&
              frame.rows.size() == preview.rows.size(),
          "Readers disagree about actual mechanical results");
    for (std::size_t i = 0; i < frame.rows.size(); ++i)
      Check(frame.rows[i].label == preview.rows[i].label &&
                frame.rows[i].value == preview.rows[i].value &&
                frame.rows[i].enabled == preview.rows[i].enabled,
            "Readers changed actual visible row state");
  }
  Check(names == actual && names.size() == 55,
        "Actual55 mechanical inventory changed silently");
}
} // namespace iw::overlay::tests::mechanicalGraph
