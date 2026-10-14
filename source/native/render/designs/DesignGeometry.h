#pragma once
#include "../layout/ApparitionLayout.h"

namespace iw::overlay::layout {
struct DesignMetrics {
    float rowOffset, bodyExtra, scrollerOffset, footerX, footerY, footerWidth;
    bool counter, colored;
};
// Each source design changes geometry independently of its selected theme
DesignMetrics MetricsFor(MenuDesign design) noexcept;
void ConfigureDesignHud(Hud& hud, bool softLock, bool widthEditor) noexcept;
}
