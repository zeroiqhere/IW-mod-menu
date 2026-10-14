#pragma once
#include "ClosedTransitionChecks.h"

namespace iw::overlay::tests {
// Recreate only owned resources after a real menu draw and a confirmed closed frame
void CheckOpenedTransitions(HWND window, ATOM windowClass, IDXGISwapChain*& chain,
    ID3D11Device*& device, ID3D11DeviceContext*& context, const ChainFactory& create,
    StatisticsCall statistics, RendererStatusCall diagnostics, SnapshotCall snapshot, SnapshotCall unavailable,
    InitializeCall stop, const TransitionCheck& check, std::ostream& log);
}
