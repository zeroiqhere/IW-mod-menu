#include "RendererStatusStub.h"
#include <mutex>

namespace {
std::mutex statusMutex;
iw::overlay::RendererStatus status;
}

namespace iw::overlay {

RendererStatus ReadRendererStatus() noexcept {
    std::lock_guard<std::mutex> lock(statusMutex);
    return status;
}

namespace tests {
void SetRendererStatusForTest(const RendererStatus& value) {
    // The stub supplies a coherent sample while publication is running on another thread
    std::lock_guard<std::mutex> lock(statusMutex);
    status = value;
}
}
}
