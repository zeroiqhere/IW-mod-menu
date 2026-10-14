#pragma once

#include <cstddef>
#include <cstdint>
#include <span>
#include <string_view>

namespace iw::loader {

// Export offsets come from validated file metadata rather than a local DLL load
struct ExportInfo {
    std::uint32_t entryRva;
    std::uint32_t imageSize;
    std::uint32_t timeStamp;
};

ExportInfo InspectExport(std::span<const std::byte> file, std::string_view exportName);

}
