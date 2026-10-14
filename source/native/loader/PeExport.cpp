#include "PeExport.h"

#include <windows.h>
#include <algorithm>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <vector>

namespace iw::loader {
namespace {

bool Fits(std::size_t offset, std::size_t length, std::size_t size) {
    return offset <= size && length <= size - offset;
}

template<class T> T Read(std::span<const std::byte> bytes, std::size_t offset) {
    if (!Fits(offset, sizeof(T), bytes.size())) {
        throw std::runtime_error("The PE file contains a truncated field");
    }
    T value{};
    std::memcpy(&value, bytes.data() + offset, sizeof(value));
    return value;
}

class Image {
public:
    explicit Image(std::span<const std::byte> bytes) : file(bytes) {
        auto dos = Read<IMAGE_DOS_HEADER>(file, 0);
        if (dos.e_magic != IMAGE_DOS_SIGNATURE || dos.e_lfanew < static_cast<LONG>(sizeof(IMAGE_DOS_HEADER))) {
            throw std::runtime_error("The file does not have a valid DOS header");
        }
        std::size_t ntOffset = static_cast<std::size_t>(dos.e_lfanew);
        headers = Read<IMAGE_NT_HEADERS64>(file, ntOffset);
        if (headers.Signature != IMAGE_NT_SIGNATURE
            || headers.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64
            || !(headers.FileHeader.Characteristics & IMAGE_FILE_DLL)
            || headers.FileHeader.SizeOfOptionalHeader != sizeof(IMAGE_OPTIONAL_HEADER64)
            || headers.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC
            || headers.OptionalHeader.NumberOfRvaAndSizes <= IMAGE_DIRECTORY_ENTRY_EXPORT
            || headers.OptionalHeader.SizeOfImage == 0 || headers.OptionalHeader.SizeOfImage > 128 * 1024 * 1024
            || headers.OptionalHeader.SizeOfHeaders > file.size()) {
            throw std::runtime_error("A bounded AMD64 DLL image is required");
        }
        std::size_t count = headers.FileHeader.NumberOfSections;
        if (count == 0 || count > 96) {
            throw std::runtime_error("The PE section count is outside supported bounds");
        }
        std::size_t offset = ntOffset + sizeof(IMAGE_NT_HEADERS64);
        for (std::size_t index = 0; index < count; ++index) {
            auto section = Read<IMAGE_SECTION_HEADER>(file, offset + index * sizeof(IMAGE_SECTION_HEADER));
            if (!Fits(section.PointerToRawData, section.SizeOfRawData, file.size())
                || !Fits(section.VirtualAddress, std::max(section.Misc.VirtualSize, section.SizeOfRawData),
                    headers.OptionalHeader.SizeOfImage)) {
                throw std::runtime_error("A PE section exceeds its file or image");
            }
            sections.push_back(section);
        }
    }

    std::size_t Offset(std::uint32_t rva, std::size_t length) const {
        if (Fits(rva, length, headers.OptionalHeader.SizeOfHeaders) && Fits(rva, length, file.size())) {
            return rva;
        }
        std::size_t result = 0;
        std::size_t matches = 0;
        for (const auto& section : sections) {
            if (rva >= section.VirtualAddress
                && Fits(rva - section.VirtualAddress, length, section.SizeOfRawData)) {
                result = section.PointerToRawData + static_cast<std::size_t>(rva - section.VirtualAddress);
                ++matches;
            }
        }
        if (matches != 1 || !Fits(result, length, file.size())) {
            throw std::runtime_error("The export references missing or ambiguous file data");
        }
        return result;
    }

    bool Executable(std::uint32_t rva) const {
        return std::count_if(sections.begin(), sections.end(), [rva](const auto& section) {
            return rva >= section.VirtualAddress
                && Fits(rva - section.VirtualAddress, 1, section.Misc.VirtualSize)
                && (section.Characteristics & IMAGE_SCN_MEM_EXECUTE);
        }) == 1;
    }

    std::string_view Name(std::uint32_t rva) const {
        // Export names are bounded and must terminate inside their mapped file data
        std::size_t offset = Offset(rva, 1);
        std::size_t length = 0;
        while (length < 128 && Fits(offset, length + 1, file.size())) {
            Offset(rva + static_cast<std::uint32_t>(length), 1);
            if (file[offset + length] == std::byte{0}) {
                return {reinterpret_cast<const char*>(file.data() + offset), length};
            }
            ++length;
        }
        throw std::runtime_error("The export name is not bounded and terminated");
    }

    std::span<const std::byte> file;
    IMAGE_NT_HEADERS64 headers{};
    std::vector<IMAGE_SECTION_HEADER> sections;
};

}

ExportInfo InspectExport(std::span<const std::byte> file, std::string_view exportName) {
    if (file.size() > 64 * 1024 * 1024 || exportName.empty() || exportName.size() > 127) {
        throw std::runtime_error("The DLL or requested export exceeds supported bounds");
    }
    Image image(file);
    auto location = image.headers.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
    if (location.Size < sizeof(IMAGE_EXPORT_DIRECTORY)
        || !Fits(location.VirtualAddress, location.Size, image.headers.OptionalHeader.SizeOfImage)) {
        throw std::runtime_error("The DLL does not contain a bounded export directory");
    }
    auto exports = Read<IMAGE_EXPORT_DIRECTORY>(file, image.Offset(location.VirtualAddress, sizeof(IMAGE_EXPORT_DIRECTORY)));
    if (exports.NumberOfNames == 0 || exports.NumberOfNames > 256
        || exports.NumberOfFunctions == 0 || exports.NumberOfFunctions > 256) {
        throw std::runtime_error("The export table is outside supported bounds");
    }
    auto names = image.Offset(exports.AddressOfNames, exports.NumberOfNames * sizeof(DWORD));
    auto ordinals = image.Offset(exports.AddressOfNameOrdinals, exports.NumberOfNames * sizeof(WORD));
    auto functions = image.Offset(exports.AddressOfFunctions, exports.NumberOfFunctions * sizeof(DWORD));
    std::uint32_t selected = 0;
    std::size_t matches = 0;
    for (DWORD index = 0; index < exports.NumberOfNames; ++index) {
        auto nameRva = Read<DWORD>(file, names + index * sizeof(DWORD));
        if (image.Name(nameRva) != exportName) {
            continue;
        }
        WORD ordinal = Read<WORD>(file, ordinals + index * sizeof(WORD));
        if (ordinal >= exports.NumberOfFunctions) {
            throw std::runtime_error("The export ordinal exceeds the function table");
        }
        selected = Read<DWORD>(file, functions + ordinal * sizeof(DWORD));
        ++matches;
    }
    if (matches != 1 || selected == 0 || !image.Executable(selected)
        || (selected >= location.VirtualAddress && Fits(selected - location.VirtualAddress, 1, location.Size))) {
        throw std::runtime_error("The requested export is missing, forwarded or non-executable");
    }
    image.Offset(selected, 1);
    return {selected, image.headers.OptionalHeader.SizeOfImage, image.headers.FileHeader.TimeDateStamp};
}

}
