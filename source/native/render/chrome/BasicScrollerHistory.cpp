#include "BasicScrollerHistory.h"

namespace iw::overlay {
std::optional<layout::Color> BasicScrollerHistory::Update(std::optional<std::uint64_t> creationRevision,
    bool existingBasicScroller, std::optional<layout::Color> rainbowSample) noexcept {
    // Missing legacy identity cannot later restore a previously observed widget
    if (!creationRevision) {
        revision_.reset(); color_.reset(); retired_ = false;
        return std::nullopt;
    }
    // Regressing identities retire their color until a genuinely newer creation arrives
    if (*creationRevision == 0 || (revision_ && *creationRevision < *revision_)) {
        color_.reset(); retired_ = true;
        return std::nullopt;
    }
    if (!revision_ || *creationRevision > *revision_) {
        revision_ = creationRevision; color_.reset(); retired_ = false;
    }
    if (retired_ || !existingBasicScroller) return std::nullopt;
    // A newly existing Basic scroller has the source's black creation color
    if (!color_) color_ = layout::Color{0, 0, 0, 255};
    if (rainbowSample) {
        // RGB assignment does not change the source scroller's opacity
        color_ = layout::Color{rainbowSample->red, rainbowSample->green, rainbowSample->blue, 255};
    }
    return color_;
}
}
