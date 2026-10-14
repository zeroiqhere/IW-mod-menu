#pragma once
#include <cstdint>
#include <optional>
#include <stdexcept>
#include <nlohmann/json.hpp>
namespace iw::overlay::runtime {
// Both readers use the same strict creation identity without inferring widget presence
inline std::optional<std::uint64_t> ParseMenuChromeRevision(const nlohmann::json& value) {
    // Null follows omitted legacy metadata and never creates a color lifetime
    if (value.is_null()) return std::nullopt;
    if (!value.is_number_unsigned() || value.get<std::uint64_t>() == 0)
        throw std::runtime_error("A positive menu chrome revision is required");
    return value.get<std::uint64_t>();
}
}
