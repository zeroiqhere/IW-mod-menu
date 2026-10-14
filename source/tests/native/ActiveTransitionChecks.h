#pragma once
#include "ClosedTransitionChecks.h"

namespace iw::overlay::tests {

// Replace only owned swapchains while the synthetic menu stays open
void CheckActiveTransitions(HWND window, ATOM windowClass, IDXGISwapChain*& chain, ID3D11Device*& device,
                            ID3D11DeviceContext*& context, const ChainFactory& create, StatisticsCall statistics,
                            RendererStatusCall diagnostics, SnapshotCall snapshot, InitializeCall stop,
                            const TransitionCheck& check, std::ostream& log);

} // namespace iw::overlay::tests
