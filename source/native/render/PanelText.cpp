#include "PanelText.h"
#include <algorithm>

namespace iw::overlay {
std::string_view BoundedPanelText(std::string_view text, std::size_t limit) noexcept {
    std::size_t end = std::min(text.size(), limit);
    // The first omitted byte can reveal a split character at the limit
    if (end < text.size()) {
        while (end && (static_cast<unsigned char>(text[end]) & 0xc0) == 0x80) { --end; }
    }
    return text.substr(0, end);
}
}
