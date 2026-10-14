#pragma once
namespace iw::overlay::runtime {
// Only completion of the aggregate chord creates a fresh opening action
class MainOpeningInput final {
  public:
    void Reset(bool held = false) noexcept { held_ = held; }
    bool Poll(bool held, bool blocked) noexcept {
        const bool fresh = held && !held_;
        held_ = held;
        // Observing blocked input consumes its completion rather than queuing a retry
        return fresh && !blocked;
    }

  private:
    bool held_ = false;
};
} // namespace iw::overlay::runtime
