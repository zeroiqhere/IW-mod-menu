#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <string_view>

namespace iw::overlay {
enum class MenuBooleanPaintValue { On, Off, Unknown };
struct MenuBooleanPaintRow {
    std::size_t index = 0;
    MenuBooleanPaintValue value = MenuBooleanPaintValue::Unknown;
    bool operator==(const MenuBooleanPaintRow&) const = default;
};
struct MenuBooleanPaint {
    std::uint64_t revision = 0;
    // Fixed storage keeps copied presentation identity bounded without allocation
    std::array<MenuBooleanPaintRow, 128> rows{};
    std::size_t count = 0;
    bool operator==(const MenuBooleanPaint&) const = default;
};
inline constexpr std::string_view BooleanPaintText(MenuBooleanPaintValue value) noexcept {
    if (value == MenuBooleanPaintValue::On) return "ON";
    if (value == MenuBooleanPaintValue::Off) return "OFF";
    return "UNKNOWN";
}
inline bool IsValidMenuBooleanPaint(const MenuBooleanPaint& paint, std::size_t rowCount) noexcept {
    if (!paint.revision || !paint.count || paint.count > paint.rows.size() || rowCount > 128) return false;
    for (std::size_t entry = 0; entry < paint.count; ++entry) {
        const auto& row = paint.rows[entry];
        if (row.index >= rowCount || (entry && row.index <= paint.rows[entry - 1].index)
            || (row.value != MenuBooleanPaintValue::On && row.value != MenuBooleanPaintValue::Off
                && row.value != MenuBooleanPaintValue::Unknown)) return false;
    }
    return true;
}
}
