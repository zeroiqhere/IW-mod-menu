#include "CreditsTimeline.h"
#include "CreditsCatalog.h"
#include <algorithm>
#include <cmath>

namespace iw::overlay::credits {
Frame Sample(double elapsedMilliseconds) noexcept {
    Frame result;
    if (!std::isfinite(elapsedMilliseconds) || elapsedMilliseconds < 0 || elapsedMilliseconds >= 40000) return result;
    const auto catalog = Catalog();
    for (std::size_t index = 0; index < catalog.size(); ++index) {
        const auto& entry = catalog[index];
        if (entry.separator) continue;
        // Sixths compare exact source launch ratios without accumulating per-line millisecond rounding
        const double ageSixths = elapsedMilliseconds * 6 - entry.launchSixths * 1000;
        if (ageSixths < 0 || ageSixths >= 60000) continue;
        const double age = ageSixths / 6;
        const float opacity = static_cast<float>(std::clamp(std::min(age / 500, (10000 - age) / 500), 0.0, 1.0));
        if (result.count == result.lines.size()) break;
        // Native linear movement and fading preserve source durations, not unverified engine easing
        result.lines[result.count++] = {index, static_cast<float>(212 - age * .02), opacity, entry.title ? 1.4f : 1.1f};
    }
    return result;
}
}
