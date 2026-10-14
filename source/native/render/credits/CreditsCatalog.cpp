#include "CreditsCatalog.h"
#include "../PanelText.h"

namespace iw::overlay::credits {
namespace {
// The opening names the BO3 reference, keeping its contributor acknowledgments distinct from IW
constexpr std::array<Entry, 26> Entries{{
    {"^1Apparition (BO3 reference)", true, 0},
    {"Original ^1Black Ops 3 Zombies Menu", false, 5},
    {"Developed By: ^1CF4_99", false, 10},
    {"Discord.gg/^1apparitionbo3", false, 15},
    {" ", false, 20, true},
    {"^1Extinct", true, 35},
    {"Ideas", false, 40},
    {"Suggestions", false, 45},
    {"Constructive Criticism", false, 50},
    {"His Spec-Nade", false, 55},
    {" ", false, 60, true},
    {"^1ItsFebiven", true, 75},
    {"Ideas", false, 80},
    {"Suggestions", false, 85},
    {" ", false, 90, true},
    {"^1CraftyCritter", true, 105},
    {"BO3 GSC Compiler", false, 110},
    {" ", false, 115, true},
    {"^1Joel", true, 130},
    {"Testing", false, 135},
    {"Breaking Shit", false, 140},
    {"Bug Reporting", false, 145},
    {" ", false, 150, true},
    {"^1Thanks For Choosing Apparition", true, 165},
    {"YouTube: ^1CF4_99", false, 170},
    {"Discord: ^1cf4_99", false, 175}
}};
}
std::span<const Entry, 26> Catalog() noexcept { return Entries; }
TextRuns SplitText(std::string_view text) noexcept {
    text = BoundedPanelText(text, 512);
    TextRuns result; bool red = false; std::size_t first = 0;
    const auto append = [&](std::size_t last) {
        if (last > first && result.count < result.runs.size())
            result.runs[result.count++] = {text.substr(first, last - first), red};
    };
    for (std::size_t index = 0; index + 1 < text.size(); ++index) {
        if (text[index] != '^' || text[index + 1] != '1') continue;
        append(index); red = true; first = index + 2; ++index;
    }
    append(text.size()); return result;
}
}
