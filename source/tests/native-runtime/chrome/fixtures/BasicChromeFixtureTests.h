#pragma once
#include <filesystem>
namespace iw::overlay::tests::chrome {
void ActualBasicChromeGraphKeepsCreationSeparateFromRowsCallbacksAndClose(const std::filesystem::path& directory);
}
