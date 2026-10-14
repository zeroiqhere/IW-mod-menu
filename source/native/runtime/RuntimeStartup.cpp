#include "RuntimeStartup.h"
#include "ControlLabels.h"
#include <utility>
namespace iw::overlay::runtime {
PreparedRuntimeStartup PrepareRuntimeStartup(const std::string &stateBytes, const std::string &bindingBytes,
                                             DWORD processId) {
    auto bindings = ParseBindings(bindingBytes);
    const auto labels = DescribeOpeningActions(bindings);
    // Private availability and captions belong to the first immutable snapshot too
    auto state = ParseState(stateBytes, processId, nullptr, nullptr, nullptr, nullptr, nullptr, std::nullopt,
                            nullptr, nullptr, &labels);
    return {std::move(state), std::move(bindings)};
}
} // namespace iw::overlay::runtime
