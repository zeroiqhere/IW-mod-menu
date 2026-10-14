#pragma once
#include <filesystem>

namespace iw::overlay::tests::callback {
// One aggregate joins the complete real-controller callback lifecycle
void ActualCallbacksKeepOwnedWaitsSeparateFromBackendWork(const std::filesystem::path& directory);
}
