#include "MenuBooleanPaintSchema.h"
#include "../shared/paint/RowDisplayValue.h"
#include <stdexcept>

namespace iw::overlay::runtime {
MenuBooleanPaint ParseMenuBooleanPaint(const nlohmann::json& value, const MenuSnapshot& snapshot) {
    if (!value.is_object() || value.size() != 2 || !value.contains("revision") || !value.contains("rows"))
        throw std::runtime_error("Boolean paint requires exactly two fields");
    const auto& revision = value.at("revision"); const auto& rows = value.at("rows");
    if (!revision.is_number_unsigned() || revision.get<std::uint64_t>() == 0
        || !rows.is_array() || rows.empty() || rows.size() > 128)
        throw std::runtime_error("Boolean paint requires a positive revision and bounded rows");
    MenuBooleanPaint paint; paint.revision = revision.get<std::uint64_t>();
    for (const auto& row : rows) {
        if (!row.is_object() || row.size() != 2 || !row.contains("index") || !row.contains("value")
            || !row.at("index").is_number_unsigned() || row.at("index").get<std::uint64_t>() > 127
            || !row.at("value").is_string())
            throw std::runtime_error("Boolean paint rows require an exact index and value");
        auto& target = paint.rows[paint.count++]; target.index = row.at("index").get<std::size_t>();
        if (row.at("value") == "ON") target.value = MenuBooleanPaintValue::On;
        else if (row.at("value") == "OFF") target.value = MenuBooleanPaintValue::Off;
        else if (row.at("value") == "UNKNOWN") target.value = MenuBooleanPaintValue::Unknown;
        else throw std::runtime_error("Unsupported boolean paint value");
    }
    // Current row shape is checked without changing feature values or capability flags
    if (!IsBooleanPaintContext(snapshot) || !AreBooleanPaintTargetsValid(snapshot, paint))
        throw std::runtime_error("Invalid boolean paint context or targets");
    return paint;
}
}
