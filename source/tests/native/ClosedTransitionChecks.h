#pragma once
#include "FixtureApi.h"
#include <d3d11.h>
#include <functional>
#include <ostream>
#include <string>

namespace iw::overlay::tests {

using TransitionCheck = std::function<void(bool, const std::string&)>;
using ChainFactory = std::function<bool(HWND, IDXGISwapChain**, ID3D11Device**, ID3D11DeviceContext**)>;

// Every window and device passed here belongs to the standalone fixture
void CheckClosedTransitions(HWND& window, ATOM windowClass, IDXGISwapChain*& chain,
    ID3D11Device*& device, ID3D11DeviceContext*& context, const ChainFactory& create,
    StatisticsCall statistics, RendererStatusCall diagnostics, InitializeCall stop,
    const TransitionCheck& check, std::ostream& log);

}
