#pragma once
#include <cstdint>

// Schema-only Linux tests need header types and a fixed owned fixture identity
using DWORD = std::uint32_t;
using HWND = void*;
inline DWORD GetCurrentProcessId() { return 42; }
