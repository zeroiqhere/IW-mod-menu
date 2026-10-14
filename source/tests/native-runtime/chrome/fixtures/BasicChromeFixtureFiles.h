#pragma once
#include "BasicChromeFixtureExpected.h"
#include <filesystem>
#include <fstream>
#include <set>
#include <stdexcept>
#include <string>

namespace iw::overlay::tests::chrome {
inline void RequireChrome(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
inline void CheckChromeInventory(const std::filesystem::path& directory) {
    RequireChrome(std::filesystem::is_directory(directory), "The actual Basic chrome graph is missing");
    std::set<std::string> expected, actual;
    for (const auto& profile : ChromeProfiles) expected.emplace(profile.file);
    // A separate fixed inventory cannot silently expand the ordinary preview graph
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        RequireChrome(entry.symlink_status().type() == std::filesystem::file_type::regular,
            "The Basic chrome graph contains a non-regular entry");
        RequireChrome(actual.emplace(entry.path().filename().string()).second && actual.size() <= ChromeProfiles.size(),
            "The Basic chrome inventory exceeds its fixed bound");
    }
    RequireChrome(actual == expected, "The Basic chrome inventory differs from the actual controller graph");
}
inline std::string ReadChromePacket(const std::filesystem::path& path) {
    RequireChrome(std::filesystem::symlink_status(path).type() == std::filesystem::file_type::regular,
        "A Basic chrome packet is not a regular file");
    const auto size = std::filesystem::file_size(path);
    RequireChrome(size > 0 && size <= 256 * 1024, "A Basic chrome packet is empty or oversized");
    std::ifstream input(path, std::ios::binary);
    RequireChrome(input.good(), "A Basic chrome packet could not be opened");
    std::string bytes(static_cast<std::size_t>(size), '\0');
    input.read(bytes.data(), static_cast<std::streamsize>(size));
    RequireChrome(input.gcount() == static_cast<std::streamsize>(size)
        && input.peek() == std::char_traits<char>::eof(), "A Basic chrome packet changed during its bounded read");
    return bytes;
}
inline bool SameChromeColor(const layout::Color& a, const layout::Color& b) noexcept {
    return a.red == b.red && a.green == b.green && a.blue == b.blue && a.alpha == b.alpha;
}
}
