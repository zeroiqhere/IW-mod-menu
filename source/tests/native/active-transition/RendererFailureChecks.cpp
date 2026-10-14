#include "RendererFailureChecks.h"
#include "../../../native/render/Renderer.h"
#include "SwapChainProxy.h"

#include <stdexcept>
#include <thread>

namespace iw::overlay::tests {
namespace {

template <class T> void Release(T*& value) {
    if (value) {
        value->Release();
        value = nullptr;
    }
}

class OwnedDevice final {
  public:
    ~OwnedDevice() {
        if (context) {
            context->ClearState();
            context->Flush();
        }
        Release(chain);
        Release(context);
        Release(device);
    }
    IDXGISwapChain* chain = nullptr;
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
};

} // namespace

void CheckRendererFailureRetirement(HWND window, const ChainFactory& create, const TransitionCheck& check,
                                    std::ostream& log) {
    for (const auto failure : {ChainFailure::BindDevice, ChainFailure::NullBindDevice, ChainFailure::BackBuffer}) {
        OwnedDevice old;
        OwnedDevice replacement;
        check(create(window, &old.chain, &old.device, &old.context) &&
                  create(window, &replacement.chain, &replacement.device, &replacement.context),
              "Failure retirement uses two real owned D3D11 devices");
        auto lifetime = std::make_shared<ChainLifetime>();
        auto* prior = new SwapChainProxy(old.chain, lifetime, ChainFailure::None);
        Renderer renderer;
        MenuSnapshot model;
        model.isOpen = true;
        model.title = "Owned failure fixture";
        model.rows = {{"No game features", "UNKNOWN", false}};
        IDXGISwapChain* retired = nullptr;
        check(renderer.Draw(prior, model, retired) && !retired,
              "The real renderer binds the owned prior chain before failure");
        prior->Release();
        auto replacementLifetime = std::make_shared<ChainLifetime>();
        auto* candidate = new SwapChainProxy(replacement.chain, replacementLifetime, failure);
        bool foreignAccepted = true;
        std::thread foreign([&] { foreignAccepted = renderer.Draw(candidate, model, retired); });
        foreign.join();
        check(!foreignAccepted && candidate->DeviceReads() == 0 && !retired,
              "A rejected presenting thread performs no candidate device reads or "
              "retirement");

        // The output slot has its own reference so rejecting it cannot steal caller
        // ownership
        prior->AddRef();
        retired = prior;
        check(!renderer.Draw(candidate, model, retired) && retired == prior && candidate->DeviceReads() == 0,
              "An occupied retirement slot is rejected without overwriting its "
              "reference");
        retired->Release();
        retired = nullptr;

        for (const auto invalidOutput : {ChainFailure::ValidationDevice, ChainFailure::NullValidationDevice}) {
            auto invalidLifetime = std::make_shared<ChainLifetime>();
            auto* invalid = new SwapChainProxy(replacement.chain, invalidLifetime, invalidOutput);
            check(!renderer.Draw(invalid, model, retired) && !retired && renderer.Initialized(),
                  "Failed or null incoming device validation preserves the prior "
                  "initialized renderer");
            invalid->Release();
        }

        bool failed = false;
        try {
            renderer.Draw(candidate, model, retired);
        } catch (const std::runtime_error&) {
            failed = true;
        }
        check(failed && retired == prior && !lifetime->destroyed,
              "A checked bind or draw failure keeps the retired reference alive");

        // This uses the same retirement ordering as the hook, with a real original
        // Present
        log << "TRACE Before original Present with failed renderer and live "
               "retired reference\n";
        log.flush();
        const auto result = candidate->Present(0, 0);
        check(SUCCEEDED(result) && !lifetime->destroyed,
              "Original Present completes before the failed handoff releases the "
              "old chain");
        retired->Release();
        retired = nullptr;
        check(lifetime->destroyed, "Releasing the retirement slot after original "
                                   "Present destroys the old proxy");
        auto* current = renderer.CloseOnPresentThread();
        if (current) {
            current->Release();
        }
        candidate->Release();
        check(replacementLifetime->destroyed, "Owner-thread cleanup releases the failed candidate reference");
    }
}

} // namespace iw::overlay::tests
