#pragma once
#include <filesystem>
namespace iw::overlay::tests::localOptions {
// One aggregate joins current facts, copied paint and routing from the real controller graph
void ActualLocalOptionGraphKeepsCurrentStatePaintAndCallerWaitsSeparate(const std::filesystem::path& directory);
}
