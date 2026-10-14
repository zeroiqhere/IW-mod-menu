#pragma once
#include "../../native/runtime/OpeningActionLabels.h"
#include "../../native/shared/MenuSnapshot.h"
#include <filesystem>

namespace iw::overlay::tests {
// Fixture JSON is data only and cannot invoke any backend action
MenuSnapshot ReadBackendPreview(const std::filesystem::path& path, const runtime::OpeningActionLabels* openingLabels = nullptr);
}
