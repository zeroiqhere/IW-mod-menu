#pragma once
#include "../QuickMenu/ClosedMenuOpening.h"
#include <array>
#include <optional>
namespace iw::overlay {
enum class OpeningPolicyMode { MainOnly, Dual };
// Bounded direct fields identify a policy without retaining vectors or relying on hashes
struct OpeningPolicyIdentity {
    OpeningPolicyMode mode = OpeningPolicyMode::MainOnly;
    std::uint64_t revision = 0, mainRevision = 0, quickRevision = 0;
    std::array<OpeningAction, 3> main{}, quick{};
    unsigned mainCount = 0, quickCount = 0;
    bool quickDisabled = false;
    bool operator==(const OpeningPolicyIdentity &) const = default;
};
inline bool IsValidOpeningPolicyOwnership(const std::optional<MainOpeningControls> &main,
                                         const std::optional<ClosedMenuOpeningPolicies> &pair) noexcept {
    if (main && !IsValidMainOpeningControls(*main)) return false;
    // A pair requires its exact Main mirror even when the ordinary HUD is closed
    return !pair || (main && IsValidClosedMenuOpeningPolicies(*pair) && *main == pair->main);
}
inline std::optional<OpeningPolicyIdentity> CaptureOpeningPolicyIdentity(
    const std::optional<MainOpeningControls> &main,
    const std::optional<ClosedMenuOpeningPolicies> &pair) noexcept {
    if (!IsValidOpeningPolicyOwnership(main, pair) || !main) return std::nullopt;
    OpeningPolicyIdentity identity;
    identity.mainRevision = main->revision;
    identity.mainCount = static_cast<unsigned>(main->actions.size());
    for (unsigned index = 0; index < identity.mainCount; ++index) identity.main[index] = main->actions[index];
    // Legacy commands use Main scope; dual commands use the global transaction scope
    identity.revision = main->revision;
    if (pair) {
        identity.mode = OpeningPolicyMode::Dual;
        identity.revision = pair->revision;
        identity.quickRevision = pair->quick.revision;
        identity.quickCount = static_cast<unsigned>(pair->quick.actions.size());
        identity.quickDisabled = pair->quickMenuDisabled;
        for (unsigned index = 0; index < identity.quickCount; ++index) identity.quick[index] = pair->quick.actions[index];
    }
    return identity;
}
} // namespace iw::overlay
