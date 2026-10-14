#include "loader/BootstrapPacket.h"

#include <iostream>
#include <stdexcept>
#include <string>

namespace {
template<class Action> void Reject(const char* label, Action action) {
    try { action(); } catch (const std::runtime_error&) {
        std::cout << "PASS " << label << '\n';
        return;
    }
    throw std::runtime_error(label);
}
}

int main() {
    try {
        // Packet tests never open another process or allocate remote memory
        HWND window = reinterpret_cast<HWND>(static_cast<std::uintptr_t>(42));
        auto config = iw::loader::MakeBootstrapPacket(51, window, L"C:\\private-session");
        if (config.size != sizeof(config) || config.version != iw::overlay::ProtocolVersion
            || config.expectedProcessId != 51 || config.gameWindow != window
            || std::wstring(config.sessionDirectory) != L"C:\\private-session") {
            throw std::runtime_error("The exact identity and session path must be copied into the packet");
        }
        std::cout << "PASS The packet preserves the exact identity and session path\n";
        std::wstring maximum(1023, L'a');
        auto boundary = iw::loader::MakeBootstrapPacket(51, window, maximum);
        if (boundary.sessionDirectory[1023] != L'\0' || std::wstring(boundary.sessionDirectory) != maximum) {
            throw std::runtime_error("The maximum accepted path must retain its terminator");
        }
        std::cout << "PASS The maximum accepted path retains a zero terminator\n";
        Reject("An oversized session path is rejected before remote work", [&] {
            iw::loader::MakeBootstrapPacket(51, window, std::wstring(1024, L'a'));
        });
        Reject("An empty session path is rejected", [&] {
            iw::loader::MakeBootstrapPacket(51, window, L"");
        });
        Reject("An embedded zero cannot truncate the validated session path", [&] {
            iw::loader::MakeBootstrapPacket(51, window, std::wstring(L"a\0b", 3));
        });
        Reject("A missing process identity is rejected", [&] {
            iw::loader::MakeBootstrapPacket(0, window, L"C:\\private-session");
        });
        Reject("A missing render window is rejected", [&] {
            iw::loader::MakeBootstrapPacket(51, nullptr, L"C:\\private-session");
        });
        std::cout << "Passed 7 bootstrap packet regressions\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
