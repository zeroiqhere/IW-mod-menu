#include "ControlLabels.h"
#include <iostream>
#include <stdexcept>

namespace {
using iw::overlay::runtime::Bindings;
using iw::overlay::runtime::DescribeControls;
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
void CurrentSavedBindingsProduceActualKeys() {
    const auto help = DescribeControls(Bindings{{2}, {86, 6}, {2, 38}, {1, 40}, {70}});
    Require(help[0] == "RMB / Up | LMB / Down: Scroll", "Scroll help must match saved action keys");
    Require(help[1] == "F: Select", "Select help must show the actual use key");
    Require(help[2] == "V / Mouse 5: Back / close", "Back help must show every current melee key");
}
void ReboundKeysUpdateHelpWithoutFixedKeyboardAssumptions() {
    const auto help = DescribeControls(Bindings{{81}, {69}, {81}, {82}, {13}});
    Require(help[0] == "Q | R: Scroll", "Rebound scroll keys were lost");
    Require(help[1] == "Enter: Select", "A rebound select must not keep a stale F hint");
    Require(help[2] == "E: Back / close", "A rebound back must not keep a stale V hint");
}
void ManyBindingsRemainBoundedAndDiscloseAdditionalKeys() {
    const auto help = DescribeControls(Bindings{{2}, {65, 66, 67}, {2, 38}, {1, 40}, {70}});
    Require(help[2] == "A / B (+1): Back / close", "Extra keys must be disclosed without unbounded help");
    for (const auto& line : help) Require(line.size() <= 96, "A control hint exceeded its text bound");
}
void InvalidBindingsCannotGenerateMisleadingInstructions() {
    try {
        DescribeControls(Bindings{{2}, {0}, {2}, {1}, {70}});
    } catch (const std::invalid_argument&) { return; }
    throw std::runtime_error("Invalid binding keys must be rejected before formatting help");
}
void ChoiceHintsRequireBothSavedGroupsAndShowTheirActualKeys() {
    const auto saved = DescribeControls(Bindings{{2}, {86}, {2}, {1}, {70}, {51}, {52}});
    Require(saved.size() == 4 && saved[3] == "3: Previous | 4: Next choice",
        "Choice help did not show both saved action keys");
    const auto rebound = DescribeControls(Bindings{{2}, {86}, {2}, {1}, {70}, {37}, {39}});
    Require(rebound.size() == 4 && rebound[3] == "Left: Previous | Right: Next choice",
        "Rebound choices retained fixed number-key hints");
    const auto missing = DescribeControls(Bindings{{2}, {86}, {2}, {1}, {70}});
    Require(missing.size() == 3, "Missing choices invented an instruction line");
    const auto partial = DescribeControls(Bindings{{2}, {86}, {2}, {1}, {70}, {51}, {}});
    Require(partial.size() == 3, "Partial choice bindings advertised an unavailable choice route");
}
void QuickExitHintsMatchCommittedPreferenceAndRootBehavior() {
    const Bindings saved{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}};
    const auto normal = DescribeControls(saved, false, false);
    const auto nested = DescribeControls(saved, true, false);
    const auto root = DescribeControls(saved, true, true);
    Require(normal[2] == "V / Mouse 5: Back / close", "Disabled preference must keep ordinary help");
    Require(nested[2] == "V / Mouse 5: Tap back / hold close", "Nested enabled preference needs tap and hold help");
    Require(root[2] == "V / Mouse 5: Close", "Root enabled preference closes immediately");
    for (std::size_t index : {0u, 1u, 3u}) {
        Require(normal[index] == nested[index] && nested[index] == root[index],
            "Quick Exit must not alter saved scroll, Use or choice instructions");
    }
    const Bindings rebound{{81}, {69}, {81}, {82}, {13}};
    Require(DescribeControls(rebound, true, false)[2] == "E: Tap back / hold close",
        "Quick Exit help must use the saved melee binding");
    for (const auto& line : nested) Require(line.size() <= 96, "Quick Exit hint must remain bounded");
}
void WidthEditorHintsUseExactSavedActionsWithoutCombinedScrollAliases() {
    const Bindings saved{{2}, {86, 6}, {2, 38}, {1, 65, 40}, {70}, {51}, {52}, {1, 65}};
    const auto hints = DescribeWidthEditorControls(saved);
    Require(hints.size() == 5 && hints[0] == "LMB / A: Increase Width" && hints[1] == "RMB: Decrease Width",
        "Width help must identify exact attack and ADS keys without action-slot scroll aliases");
    Require(hints[2] == "4: Increase Offset" && hints[3] == "3: Decrease Offset" && hints[4] == "V / Mouse 5: Exit",
        "Offset and exit help must show their actual saved controls");
    for (const auto& line : hints) Require(line.size() <= 96, "Editor help must remain bounded");
    const Bindings remapped{{81}, {69}, {81, 38}, {82, 40}, {13}, {37}, {39}, {82}};
    const auto changed = DescribeWidthEditorControls(remapped);
    Require(changed[0] == "R: Increase Width" && changed[1] == "Q: Decrease Width"
        && changed[2] == "Right: Increase Offset" && changed[3] == "Left: Decrease Offset" && changed[4] == "E: Exit",
        "Remapped editor help must not retain fixed mouse or number keys");
    for (const Bindings& absent : {Bindings{{2}, {86}, {2}, {1}, {70}, {51}, {52}},
        Bindings{{2}, {86}, {2}, {1}, {70}, {}, {52}, {1}}}) {
        try { DescribeWidthEditorControls(absent); }
        catch (const std::invalid_argument&) { continue; }
        throw std::runtime_error("Incomplete editor controls cannot advertise a guessed route");
    }
}
void PositionEditorHintsUseOnlyExactFourSavedActionSlotsAndMelee() {
    const Bindings saved{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}};
    const auto hints = iw::overlay::runtime::DescribeInstructionPositionControls(saved);
    Require(hints == std::vector<std::string>{"Up: Move Up", "Down: Move Down", "3: Move Left", "4: Move Right", "V / Mouse 5: Exit"},
        "Position help substituted aim, attack or guessed movement keys");
    const Bindings remapped{{81}, {69}, {81, 87}, {82, 83}, {13}, {65}, {68}, {82}, {87}, {83}};
    Require(iw::overlay::runtime::DescribeInstructionPositionControls(remapped)
        == std::vector<std::string>{"W: Move Up", "S: Move Down", "A: Move Left", "D: Move Right", "E: Exit"},
        "Remapped position help kept stale arrow or number keys");
    for (const auto& line : hints) Require(line.size() <= 96, "Position help exceeded its display bound");
}
void PositionEditorHintsRejectEveryMissingExactActionGroup() {
    const Bindings saved{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}};
    for (int missing = 0; missing < 4; ++missing) {
        auto bindings = saved;
        if (missing == 0) bindings.moveUp.clear();
        if (missing == 1) bindings.moveDown.clear();
        if (missing == 2) bindings.previousChoice.clear();
        if (missing == 3) bindings.nextChoice.clear();
        bool rejected = false;
        try { iw::overlay::runtime::DescribeInstructionPositionControls(bindings); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected, "Incomplete position bindings advertised a guessed route");
    }
}
void MenuPositionHintsPreserveSevenExactSavedActionsInSourceDisplayOrder() {
    const Bindings saved{{2}, {86, 6}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}};
    const auto hints = iw::overlay::runtime::DescribeMenuPositionControls(saved);
    Require(hints == std::vector<std::string>{"Up: Move Up", "Down: Move Down", "3: Move Left", "4: Move Right",
        "G: Increase Offset", "E: Decrease Offset", "V / Mouse 5: Exit"}, "Menu position help lost saved keys or source order");
    const Bindings remapped{{2}, {6}, {2, 87}, {1, 83}, {70}, {65}, {68}, {1}, {87}, {83}, {81, 5}, {82}};
    Require(iw::overlay::runtime::DescribeMenuPositionControls(remapped)
        == std::vector<std::string>{"W: Move Up", "S: Move Down", "A: Move Left", "D: Move Right",
            "Q / Mouse 4: Increase Offset", "R: Decrease Offset", "Mouse 5: Exit"},
        "Remapped position help retained fixed movement or grenade keys");
    for (const auto& line : hints) Require(line.size() <= 96, "Menu position hint exceeded its display bound");
}
void MenuPositionHintsRejectEveryMissingSavedSlotOrGrenadeGroup() {
    const Bindings saved{{2}, {86}, {2, 38}, {1, 40}, {70}, {51}, {52}, {1}, {38}, {40}, {71}, {69}};
    for (int missing = 0; missing < 6; ++missing) {
        auto bindings = saved;
        std::vector<int>* groups[]{&bindings.moveUp, &bindings.moveDown, &bindings.previousChoice,
            &bindings.nextChoice, &bindings.frag, &bindings.secondaryOffhand};
        groups[missing]->clear(); bool rejected = false;
        try { iw::overlay::runtime::DescribeMenuPositionControls(bindings); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected, "Missing exact action group produced a misleading editor hint");
    }
}
void CreditsHintsUseExactSavedMeleeWithoutOptionalEditorBindings() {
    const Bindings saved{{2}, {86, 6}, {2}, {1}, {70}};
    Require(iw::overlay::runtime::DescribeMenuCreditsControls(saved)
        == std::vector<std::string>{"V / Mouse 5: Exit Menu Credits"}, "Credits exit help guessed keys or advertised ordinary gestures");
    const Bindings remapped{{2}, {69, 5, 81}, {2}, {1}, {70}};
    const auto help = iw::overlay::runtime::DescribeMenuCreditsControls(remapped);
    Require(help == std::vector<std::string>{"E / Mouse 4 (+1): Exit Menu Credits"}, "Rebound credits help retained fixed V or lost alternatives");
    Require(help[0].size() <= 96, "Credits exit help exceeded its display bound");
}
void CreditsHintsRejectMissingDuplicateAndAliasedMeleeKeys() {
    for (const auto& melee : {std::vector<int>{}, std::vector<int>{0}, std::vector<int>{86, 86}, std::vector<int>{2}}) {
        const Bindings invalid{{2}, melee, {2}, {1}, {70}}; bool rejected = false;
        try { iw::overlay::runtime::DescribeMenuCreditsControls(invalid); }
        catch (const std::invalid_argument&) { rejected = true; }
        Require(rejected, "Malformed credits melee produced misleading exit help");
    }
}
}
int main() {
    try {
        CurrentSavedBindingsProduceActualKeys();
        ReboundKeysUpdateHelpWithoutFixedKeyboardAssumptions();
        ManyBindingsRemainBoundedAndDiscloseAdditionalKeys();
        InvalidBindingsCannotGenerateMisleadingInstructions();
        ChoiceHintsRequireBothSavedGroupsAndShowTheirActualKeys();
        QuickExitHintsMatchCommittedPreferenceAndRootBehavior();
        WidthEditorHintsUseExactSavedActionsWithoutCombinedScrollAliases();
        PositionEditorHintsUseOnlyExactFourSavedActionSlotsAndMelee();
        PositionEditorHintsRejectEveryMissingExactActionGroup();
        MenuPositionHintsPreserveSevenExactSavedActionsInSourceDisplayOrder();
        MenuPositionHintsRejectEveryMissingSavedSlotOrGrenadeGroup();
        CreditsHintsUseExactSavedMeleeWithoutOptionalEditorBindings();
        CreditsHintsRejectMissingDuplicateAndAliasedMeleeKeys();
        std::cout << "Passed 13 saved-control hint checks\n";
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
