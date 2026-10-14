#pragma once
#include "../PanelText.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <string_view>

namespace iw::overlay::rows {
inline constexpr std::size_t MaximumRowLabelBytes = 512;
struct FittedRowLabel {
    // A fixed buffer bounds work and does not retain a pointer into a temporary
    // prefix
    std::array<char, MaximumRowLabelBytes + 3> bytes{};
    std::size_t length = 0;
    bool valid = false, ellipsized = false;
    std::string_view Text() const noexcept { return {bytes.data(), length}; }
};

// The measurement seam keeps this bounded UTF-8 fitter independent from fonts
// and graphics APIs
template <class Measure>
FittedRowLabel FitRowLabel(std::string_view input, float available, Measure &&measure) {
    FittedRowLabel result;
    if (!std::isfinite(available) || available < 0) {
        return result;
    }
    input = BoundedPanelText(input, MaximumRowLabelBytes);
    const auto width = [&](std::string_view text) {
        const float measured = measure(text);
        return std::isfinite(measured) && measured >= 0 ? measured : -1.0f;
    };
    const float full = width(input);
    if (full < 0) {
        return result;
    }
    result.valid = true;
    if (full <= available) {
        std::copy(input.begin(), input.end(), result.bytes.begin());
        result.length = input.size();
        return result;
    }
    result.ellipsized = true;
    const float dots = width("...");
    if (dots < 0) {
        result.valid = false;
        return result;
    }
    if (dots > available) {
        return result;
    }
    const auto candidate = [&](std::size_t count) {
        const auto prefix = BoundedPanelText(input, count);
        std::copy(prefix.begin(), prefix.end(), result.bytes.begin());
        std::copy_n("...", 3, result.bytes.begin() + prefix.size());
        result.length = prefix.size() + 3;
        return width(result.Text());
    };
    // Search complete prefixes without repeated allocation or a scan of every
    // possible label
    std::size_t low = 0, high = input.size();
    while (low < high) {
        const auto middle = low + (high - low + 1) / 2;
        const float measured = candidate(middle);
        if (measured < 0) {
            result.valid = false;
            result.length = 0;
            return result;
        }
        if (measured <= available) {
            low = middle;
        } else {
            high = middle - 1;
        }
    }
    // The final measurement also guards against a changing or unusual font seam
    const float finalWidth = candidate(low);
    if (finalWidth < 0 || finalWidth > available) {
        result.valid = false;
        result.length = 0;
    }
    return result;
}
} // namespace iw::overlay::rows
