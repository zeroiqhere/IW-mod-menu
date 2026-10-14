#include "../../native/render/rows/RowFont.h"
#include "Checks.h"
#include <limits>
namespace iw::overlay::tests {
void AddRowFontChecks(Checks &checks) {
    checks.emplace_back("Normal and sampled large-cursor fonts retain the source equation", [] {
        Require(rows::RowFontPixels(10, false, 1) == 10 && rows::RowFontPixels(10, true, 0) == 10 &&
                    rows::RowFontPixels(10, true, .5f) == 11 && rows::RowFontPixels(10, true, 1) == 12,
                "Sampled font growth changed");
    });
    checks.emplace_back("Subpixel and invalid font requests never bake below one pixel", [] {
        const auto nan = std::numeric_limits<float>::quiet_NaN();
        for (float base : {.01f, 0.f, -1.f, nan, std::numeric_limits<float>::infinity()}) {
            Require(rows::RowFontPixels(base, true, .5f) == 1, "Font floor failed");
        }
        Require(rows::RowFontPixels(10, true, nan) == 1 &&
                    rows::RowFontPixels(std::numeric_limits<float>::max(), true, 1) == 1,
                "Invalid sampled size escaped");
    });
    checks.emplace_back("Out-of-range selection samples clamp to the source endpoints", [] {
        Require(rows::RowFontPixels(10, true, -1) == 10 && rows::RowFontPixels(10, true, 2) == 12,
                "Selection sample escaped the bounded font range");
    });
}
} // namespace iw::overlay::tests
