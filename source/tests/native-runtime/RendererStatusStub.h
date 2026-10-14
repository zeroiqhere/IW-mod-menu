#pragma once
#include "../../native/render/RendererDiagnostics.h"

namespace iw::overlay::tests {

// Runtime tests change cached values without creating a device or linking the renderer
void SetRendererStatusForTest(const RendererStatus& status);

}
