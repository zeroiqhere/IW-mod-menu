#pragma once
#include <array>
#include <cstdint>
#include <optional>
#include <string_view>
namespace iw::overlay {
using InstructionRgb = std::array<std::uint8_t,3>;
// Exact ASCII parsing also gives case-independent equality without allocating inside paint paths
inline std::optional<InstructionRgb> ParseInstructionRgb(std::string_view value) noexcept {
    if(value.size()!=7 || value[0]!='#') return std::nullopt;
    auto digit=[](char byte) noexcept {
        if(byte>='0' && byte<='9') return byte-'0';
        if(byte>='A' && byte<='F') return byte-'A'+10;
        if(byte>='a' && byte<='f') return byte-'a'+10;
        return -1;
    };
    InstructionRgb result{};
    for(unsigned index=0;index<3;++index) {
        const int high=digit(value[1+index*2]),low=digit(value[2+index*2]);
        if(high<0 || low<0) return std::nullopt;
        result[index]=static_cast<std::uint8_t>(high*16+low);
    }
    return result;
}
}
