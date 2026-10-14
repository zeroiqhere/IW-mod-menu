#pragma once
#include <filesystem>

namespace iw::overlay::tests::booleanPaint {
// One aggregate checks the real owned display lifecycle without invoking its game actions
void ActualBooleanPaintGraphKeepsCurrentResultsSeparateFromCopiedHudValues(const std::filesystem::path& directory);
void ActualCohortBooleanPaintKeepsAggregateAuthoritySeparateFromDisplaySamples(const std::filesystem::path& directory);
}
