#include "../../native/render/rows/RowTextLayout.h"
#include "Checks.h"
#include <limits>
namespace iw::overlay::tests {
void AddRowLayoutChecks(Checks &checks) {
    using namespace rows;
    checks.emplace_back("Fitting scalar keeps established source anchors and gap", [] {
        const auto ink = MakeRowTextLayout({10, 260, 1, 60, ValuePlacement::Text});
        Require(ink.valid && ink.labelLeft == 14 && ink.labelRight == 202 && ink.valueLeft == 206 &&
                    ink.valueRight == 266 && ink.valueClipLeft == 202,
                "Fitting row anchors changed");
    });
    checks.emplace_back("Value wider than half the body reserves all measured ink", [] {
        const auto ink = MakeRowTextLayout({0, 200, 1, 150, ValuePlacement::Text});
        Require(ink.valid && ink.valueLeft == 46 && ink.labelRight == 42 && ink.valueClipLeft == 42,
                "Full value reservation kept the old half-body cap");
    });
    checks.emplace_back("Oversized value keeps its right origin and clips honestly to the body", [] {
        const auto ink = MakeRowTextLayout({10, 200, 1, 400, ValuePlacement::Text});
        Require(ink.valid && ink.valueLeft == -194 && ink.valueRight == 206 && ink.valueClipLeft == 10 &&
                    ink.labelRight == ink.labelLeft,
                "Oversized value was shifted or label room invented");
    });
    checks.emplace_back("Left and right boolean boxes retain their original label bounds", [] {
        const auto left = MakeRowTextLayout({10, 260, 2, 0, ValuePlacement::BoxLeft});
        const auto right = MakeRowTextLayout({10, 260, 2, 0, ValuePlacement::BoxRight});
        Require(left.valid && left.labelLeft == 46 && left.labelRight == 262 && right.valid &&
                    right.labelLeft == 18 && right.labelRight == 230,
                "Box reservation moved");
    });
    checks.emplace_back("Rows without value ink retain full padded label space", [] {
        const auto ink = MakeRowTextLayout({10, 260, 1, 0, ValuePlacement::None});
        Require(ink.valid && ink.labelLeft == 14 && ink.labelRight == 266 && ink.valueClipLeft == 270,
                "Empty or color-only value consumed label space");
    });
    checks.emplace_back("Invalid numeric layout inputs and unknown placements fail closed", [] {
        const auto nan = std::numeric_limits<float>::quiet_NaN();
        const auto inf = std::numeric_limits<float>::infinity();
        for (auto request : {RowTextRequest{nan, 200, 1, 20, ValuePlacement::Text},
                             {0, inf, 1, 20, ValuePlacement::Text},
                             {0, 200, 0, 20, ValuePlacement::Text},
                             {0, -1, 1, 20, ValuePlacement::Text},
                             {0, 200, 1, -1, ValuePlacement::Text},
                             {0, 200, 1, nan, ValuePlacement::Text},
                             {0, 200, inf, 20, ValuePlacement::Text},
                             {0, 200, std::numeric_limits<float>::max() / 8, 20, ValuePlacement::BoxLeft},
                             {std::numeric_limits<float>::max(), std::numeric_limits<float>::max(), 1, 20,
                              ValuePlacement::Text},
                             {0, 200, 1, 20, static_cast<ValuePlacement>(999)}}) {
            Require(!MakeRowTextLayout(request).valid, "Invalid layout was accepted");
        }
    });
}
} // namespace iw::overlay::tests
