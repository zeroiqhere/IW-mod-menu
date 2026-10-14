#pragma once
#include "../MenuDesign.h"
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace iw::overlay {
// An absent owned HUD differs from a legacy packet without a lifetime descriptor
struct InstructionHud {
    bool exists = false;
    std::optional<std::uint64_t> creationRevision = std::nullopt;
    std::optional<std::uint64_t> admissionRevision = std::nullopt;
    std::optional<MenuDesign> creationDesign = std::nullopt;
    std::optional<bool> creationAlternate = std::nullopt;
    std::optional<bool> creationBackground = std::nullopt;
    // Only native saved groups supply physical key hints
    std::vector<std::string> instructions;
    // Paired creation facts do not claim the source Rainbow sample
    std::optional<std::string> creationAccent = std::nullopt;
    std::optional<bool> creationRainbow = std::nullopt;
};
}
