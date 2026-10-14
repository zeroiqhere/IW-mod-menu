#pragma once
#include "RuntimeSchema.h"
namespace iw::overlay::runtime {
struct PreparedRuntimeStartup {
    ParsedState state;
    Bindings bindings;
};
// Prepare owned data before a caller publishes a snapshot or starts its worker
PreparedRuntimeStartup PrepareRuntimeStartup(const std::string &stateBytes, const std::string &bindingBytes,
                                             DWORD processId);
} // namespace iw::overlay::runtime
