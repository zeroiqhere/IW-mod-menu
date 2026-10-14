#include "loader/PeExport.h"
#include <windows.h>
#include <cstring>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <vector>

namespace {
template<class T> void Put(std::vector<std::byte>& file, std::size_t offset, const T& value) {
    std::memcpy(file.data() + offset, &value, sizeof(value));
}

std::vector<std::byte> Image() {
    // A minimal file fixture is inspected as data and is never loaded as code
    std::vector<std::byte> file(0x600);
    IMAGE_DOS_HEADER dos{};
    dos.e_magic = IMAGE_DOS_SIGNATURE;
    dos.e_lfanew = 0x80;
    Put(file, 0, dos);
    IMAGE_NT_HEADERS64 nt{};
    nt.Signature = IMAGE_NT_SIGNATURE;
    nt.FileHeader.Machine = IMAGE_FILE_MACHINE_AMD64;
    nt.FileHeader.Characteristics = IMAGE_FILE_DLL;
    nt.FileHeader.NumberOfSections = 2;
    nt.FileHeader.SizeOfOptionalHeader = sizeof(IMAGE_OPTIONAL_HEADER64);
    nt.FileHeader.TimeDateStamp = 1234;
    nt.OptionalHeader.Magic = IMAGE_NT_OPTIONAL_HDR64_MAGIC;
    nt.OptionalHeader.SizeOfImage = 0x3000;
    nt.OptionalHeader.SizeOfHeaders = 0x200;
    nt.OptionalHeader.NumberOfRvaAndSizes = IMAGE_NUMBEROF_DIRECTORY_ENTRIES;
    nt.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT] = {0x2000, 0x120};
    Put(file, 0x80, nt);
    IMAGE_SECTION_HEADER code{};
    code.VirtualAddress = 0x1000;
    code.Misc.VirtualSize = code.SizeOfRawData = 0x200;
    code.PointerToRawData = 0x200;
    code.Characteristics = IMAGE_SCN_MEM_EXECUTE | IMAGE_SCN_MEM_READ;
    IMAGE_SECTION_HEADER exports = code;
    exports.VirtualAddress = 0x2000;
    exports.PointerToRawData = 0x400;
    exports.Characteristics = IMAGE_SCN_MEM_READ;
    Put(file, 0x80 + sizeof(nt), code);
    Put(file, 0x80 + sizeof(nt) + sizeof(code), exports);
    IMAGE_EXPORT_DIRECTORY directory{};
    directory.NumberOfFunctions = directory.NumberOfNames = 1;
    directory.AddressOfFunctions = 0x2060;
    directory.AddressOfNames = 0x2070;
    directory.AddressOfNameOrdinals = 0x2078;
    Put(file, 0x400, directory);
    Put(file, 0x460, DWORD{0x1100});
    Put(file, 0x470, DWORD{0x2080});
    Put(file, 0x478, WORD{0});
    const char name[] = "InitializeOverlay";
    std::memcpy(file.data() + 0x480, name, sizeof(name));
    return file;
}

void Reject(const char* name, std::vector<std::byte> file) {
    try {
        iw::loader::InspectExport(file, "InitializeOverlay");
    } catch (const std::runtime_error&) {
        std::cout << "PASS " << name << '\n';
        return;
    }
    throw std::runtime_error(name);
}
}

int main() {
    try {
        auto original = Image();
        auto result = iw::loader::InspectExport(original, "InitializeOverlay");
        if (result.entryRva != 0x1100 || result.imageSize != 0x3000 || result.timeStamp != 1234) {
            throw std::runtime_error("Valid export metadata differs from the fixture");
        }
        std::cout << "PASS Valid AMD64 DLL returns the exact bootstrap offset and metadata\n";
        auto file = original;
        file.resize(10);
        Reject("Truncated DOS header is rejected", file);
        file = original;
        Put(file, offsetof(IMAGE_DOS_HEADER, e_lfanew), LONG{-1});
        Reject("Negative PE header offset is rejected", file);
        file = original;
        Put(file, 0x80 + offsetof(IMAGE_NT_HEADERS64, FileHeader) + offsetof(IMAGE_FILE_HEADER, Machine), WORD{IMAGE_FILE_MACHINE_I386});
        Reject("A 32-bit payload is rejected", file);
        file = original;
        Put(file, 0x400 + offsetof(IMAGE_EXPORT_DIRECTORY, NumberOfNames), DWORD{257});
        Reject("Oversized export tables are rejected", file);
        file = original;
        Put(file, 0x478, WORD{1});
        Reject("Invalid function ordinals are rejected", file);
        file = original;
        Put(file, 0x460, DWORD{0x2080});
        Reject("Forwarded or non-executable exports are rejected", file);
        file = original;
        Put(file, 0x460, DWORD{0x3100});
        Reject("Exports outside the loaded image are rejected", file);
        file = original;
        std::memset(file.data() + 0x480, 'x', 128);
        Reject("Unterminated export names are rejected", file);
        file = original;
        Put(file, 0x80 + sizeof(IMAGE_NT_HEADERS64) + offsetof(IMAGE_SECTION_HEADER, PointerToRawData), DWORD{0x10000});
        Reject("Raw sections beyond the file are rejected", file);
        file = original;
        Put(file, 0x470, DWORD{0x10000000});
        Reject("Invalid export name references are rejected", file);
        file = original;
        std::memcpy(file.data() + 0x480, "DifferentOverlay", 17);
        Reject("Missing named bootstrap exports are rejected", file);
        std::cout << "Passed 12 PE export inspection regressions\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
