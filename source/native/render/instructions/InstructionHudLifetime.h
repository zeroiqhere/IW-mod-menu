#pragma once
#include "InstructionHudPresence.h"
namespace iw::overlay {
class InstructionHudLifetime final {
public:
    bool Observe(const MenuSnapshot* snapshot) noexcept;
private:
    std::optional<std::uint64_t> highest_;
    std::optional<MenuDesign> design_;
    std::optional<bool> alternate_, background_;
    std::optional<InstructionRgb> creationRgb_;
    std::optional<bool> creationRainbow_;
    bool quarantined_ = false;
};
}
