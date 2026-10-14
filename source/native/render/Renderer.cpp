#include "chrome/BasicScrollerClosedObservation.h"
#include "chrome/BasicScrollerPresence.h"
#include "Renderer.h"
#include "instructions/observation/InstructionRenderPhase.h"
#include "SmoothRainbow.h"
#include "ScrollRows.h"
#include "ApparitionPanel.h"

#include <algorithm>
#include <array>
#include <stdexcept>
#include <imgui.h>
#include <backends/imgui_impl_dx11.h>

namespace iw::overlay {
namespace {

template<class T> void Release(T*& value) noexcept {
    if (value) { value->Release(); value = nullptr; }
}

// Restore the caller's ImGui context even if a frame cannot be completed
class ContextScope {
public:
    explicit ContextScope(ImGuiContext* current) : previous_(ImGui::GetCurrentContext()) {
        ImGui::SetCurrentContext(current);
    }
    ~ContextScope() { ImGui::SetCurrentContext(previous_); }
private:
    ImGuiContext* previous_;
};

// Dear ImGui backs up its common drawing state but omits these shader stages and targets
class AdditionalState {
public:
    explicit AdditionalState(ID3D11DeviceContext* context) : context_(context) {
        context_->OMGetRenderTargets(static_cast<UINT>(targets_.size()), targets_.data(), &depth_);
        context_->HSGetShader(&hull_, hullInstances_.data(), &hullCount_);
        context_->DSGetShader(&domain_, domainInstances_.data(), &domainCount_);
        context_->CSGetShader(&compute_, computeInstances_.data(), &computeCount_);
        // Binding a back buffer can clear conflicting resources in any shader stage
        context_->VSGetShaderResources(0, ResourceCount, resources_[0].data());
        context_->HSGetShaderResources(0, ResourceCount, resources_[1].data());
        context_->DSGetShaderResources(0, ResourceCount, resources_[2].data());
        context_->GSGetShaderResources(0, ResourceCount, resources_[3].data());
        context_->PSGetShaderResources(0, ResourceCount, resources_[4].data());
        context_->CSGetShaderResources(0, ResourceCount, resources_[5].data());
        context_->OMGetRenderTargetsAndUnorderedAccessViews(0, nullptr, nullptr, 0, UavCount, pixelUavs_.data());
        context_->CSGetUnorderedAccessViews(0, UavCount, computeUavs_.data());
    }
    ~AdditionalState() noexcept {
        UINT count = 0;
        for (UINT index = 0; index < targets_.size(); ++index) {
            if (targets_[index]) { count = index + 1; }
        }
        // Restore output bindings first so resource reads are not cleared again
        context_->OMSetRenderTargets(count, targets_.data(), depth_);
        std::array<UINT, UavCount> keepCounts;
        keepCounts.fill(D3D11_KEEP_UNORDERED_ACCESS_VIEWS);
        if (count < UavCount) {
            context_->OMSetRenderTargetsAndUnorderedAccessViews(D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL,
                nullptr, nullptr, count, UavCount - count, pixelUavs_.data() + count, keepCounts.data() + count);
        }
        context_->CSSetUnorderedAccessViews(0, UavCount, computeUavs_.data(), keepCounts.data());
        context_->VSSetShaderResources(0, ResourceCount, resources_[0].data());
        context_->HSSetShaderResources(0, ResourceCount, resources_[1].data());
        context_->DSSetShaderResources(0, ResourceCount, resources_[2].data());
        context_->GSSetShaderResources(0, ResourceCount, resources_[3].data());
        context_->PSSetShaderResources(0, ResourceCount, resources_[4].data());
        context_->CSSetShaderResources(0, ResourceCount, resources_[5].data());
        context_->HSSetShader(hull_, hullInstances_.data(), hullCount_);
        context_->DSSetShader(domain_, domainInstances_.data(), domainCount_);
        context_->CSSetShader(compute_, computeInstances_.data(), computeCount_);
        for (auto& value : targets_) { Release(value); }
        Release(depth_); Release(hull_); Release(domain_); Release(compute_);
        for (auto& value : hullInstances_) { Release(value); }
        for (auto& value : domainInstances_) { Release(value); }
        for (auto& value : computeInstances_) { Release(value); }
        for (auto& stage : resources_) { for (auto& value : stage) { Release(value); } }
        for (auto& value : pixelUavs_) { Release(value); }
        for (auto& value : computeUavs_) { Release(value); }
    }
private:
    static constexpr UINT ResourceCount = D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT;
    static constexpr UINT UavCount = D3D11_PS_CS_UAV_REGISTER_COUNT;
    ID3D11DeviceContext* context_;
    std::array<ID3D11RenderTargetView*, D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT> targets_{};
    ID3D11DepthStencilView* depth_ = nullptr;
    ID3D11HullShader* hull_ = nullptr;
    ID3D11DomainShader* domain_ = nullptr;
    ID3D11ComputeShader* compute_ = nullptr;
    std::array<ID3D11ClassInstance*, 256> hullInstances_{}, domainInstances_{}, computeInstances_{};
    UINT hullCount_ = 256, domainCount_ = 256, computeCount_ = 256;
    std::array<std::array<ID3D11ShaderResourceView*, ResourceCount>, 6> resources_{};
    std::array<ID3D11UnorderedAccessView*, UavCount> pixelUavs_{}, computeUavs_{};
};

}

Renderer::~Renderer() { Reset(); }

bool Renderer::Bind(IDXGISwapChain* chain) {
    DXGI_SWAP_CHAIN_DESC description{};
    if (FAILED(chain->GetDesc(&description)) || !description.OutputWindow
        || FAILED(chain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&device_))) || !device_) {
        Reset(); return false;
    }
    // Restrict the state guard to the verified Direct3D 11.0 resource limits
    featureLevel_ = static_cast<std::uint32_t>(device_->GetFeatureLevel());
    if (featureLevel_ != D3D_FEATURE_LEVEL_11_0) {
        Reset(); return false;
    }
    device_->GetImmediateContext(&context_);
    if (!context_) { Reset(); return false; }
    chain_ = chain;
    chain_->AddRef();
    window_ = description.OutputWindow;
    thread_ = GetCurrentThreadId();
    ImGuiContext* previous = ImGui::GetCurrentContext();
    imgui_ = ImGui::CreateContext();
    ImGui::SetCurrentContext(previous);
    if (!imgui_) { Reset(); return false; }
    ContextScope scope(imgui_);
    auto& io = ImGui::GetIO();
    io.IniFilename = nullptr;
    io.LogFilename = nullptr;
    io.ConfigFlags = ImGuiConfigFlags_None;
    backendReady_ = ImGui_ImplDX11_Init(device_, context_);
    if (!backendReady_ || !ImGui_ImplDX11_CreateDeviceObjects() || !QueryPerformanceFrequency(&frequency_)) {
        Reset(); return false;
    }
    QueryPerformanceCounter(&previousFrame_);
    ImGui::StyleColorsDark();
    auto& style = ImGui::GetStyle();
    style.WindowRounding = 0;
    style.WindowPadding = ImVec2(12, 10);
    style.Colors[ImGuiCol_WindowBg] = ImVec4(0.157f, 0.157f, 0.157f, 0.98f);
    return true;
}

bool Renderer::CreateTarget(IDXGISwapChain* chain) {
    ID3D11Texture2D* buffer = nullptr;
    HRESULT result = chain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&buffer));
    if (SUCCEEDED(result)) { result = device_->CreateRenderTargetView(buffer, nullptr, &target_); }
    Release(buffer);
    return SUCCEEDED(result) && target_;
}

bool Renderer::Draw(IDXGISwapChain* chain, const MenuSnapshot& snapshot, IDXGISwapChain*& retiredChain) {
    // Closing resources does not allow a different thread to claim the private ImGui context
    if (thread_ != 0 && GetCurrentThreadId() != thread_) { return false; }
    // An existing owned reference must never be overwritten by another handoff
    if (retiredChain || !chain) { return false; }
    // One copied clock and sample serve both logical owners and the later paint
    const auto presentationClock = GetTickCount64();
    const auto nativeRainbowSample = SampleSmoothRainbow(presentationClock);
    std::optional<ChromeColorFrame> chromeColors;
    D3D11_TEXTURE2D_DESC description{};
    return RunInstructionRenderPhase(snapshot, instructionHudLifetime_, instructionHudColorHistory_,
        nativeRainbowSample, presentationClock,
        [&](bool instructionHudAdmitted) {
            // Chrome also observes before every resource preparation exit
            chromeColors = chromeColorHistory_.Update(snapshot, nativeRainbowSample, presentationClock);
            if (!snapshot.isOpen) {
                // Retire ordinary presentation without destroying a separately admitted footer
                ObserveClosedBasicScroller(basicScrollerHistory_, &snapshot);
                widthAnimation_.Reset(); scrollPresentation_.Reset(); creditsClock_.Reset();
                if (!instructionHudAdmitted) return false;
            }
            if (!PrepareSwapChain(chain, retiredChain)) { return false; }
            if (!chain_ && !Bind(chain)) { throw std::runtime_error("The target D3D11 device could not be bound"); }
            if (chain != chain_ || GetCurrentThreadId() != thread_) { return false; }
            if (!target_ && !CreateTarget(chain)) { throw std::runtime_error("The target back buffer could not be opened"); }
            ID3D11Texture2D* buffer = nullptr;
            if (FAILED(chain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&buffer)))) { return false; }
            buffer->GetDesc(&description);
            Release(buffer);
            if (description.Width == 0 || description.Height == 0) { return false; }
            width_ = description.Width; height_ = description.Height;
            return true;
        },
        [&](const InstructionPaintObservation& observation) {
            ContextScope scope(imgui_);
            auto& io = ImGui::GetIO();
            io.DisplaySize = ImVec2(static_cast<float>(description.Width), static_cast<float>(description.Height));
            LARGE_INTEGER current{};
            QueryPerformanceCounter(&current);
            io.DeltaTime = std::clamp(static_cast<float>(current.QuadPart - previousFrame_.QuadPart)
                / static_cast<float>(frequency_.QuadPart), 0.001f, 0.25f);
            previousFrame_ = current;
            ImGui_ImplDX11_NewFrame();
            ImGui::NewFrame();
            // Sample only on the owner thread; committed width and immutable vectors stay untouched
            const float presentationWidth = widthAnimation_.Update(snapshot.appearance.menuWidth,
                snapshot.isOpen, snapshot.widthEditor.has_value(), snapshot.navigationRevision, presentationClock);
            // Absolute uptime keeps the independent native phase running through closed menus
            const auto presentationColor = snapshot.smoothRainbowTheme
                ? std::optional<layout::Color>{nativeRainbowSample} : std::nullopt;
            // Hidden or clipped source scrollers still receive the admitted native Rainbow sample
            const auto basicScrollerColor = basicScrollerHistory_.Update(snapshot.menuChromeRevision,
                HasExistingBasicScroller(snapshot), presentationColor);
            // Row identities refer to current immutable data; private epochs prevent cross-page motion
            const auto rows = scrollPresentation_.Update(MakeScrollRequest(snapshot, io.DisplaySize.x,
                io.DisplaySize.y, presentationWidth), presentationClock);
            std::optional<double> creditsElapsed;
            if (snapshot.menuCredits) {
                creditsElapsed = creditsClock_.Update(snapshot.menuCredits->elapsedMilliseconds, snapshot.menuRevision,
                    snapshot.presentationFocusRevision, presentationClock);
            } else { creditsClock_.Reset(); }
            const bool drawn = DrawApparitionPanel(snapshot, nullptr, presentationWidth, presentationColor,
                &rows, creditsElapsed, basicScrollerColor, !snapshot.instructionHud || observation.colorsAdmitted,
                observation.colors, chromeColors ? &*chromeColors : nullptr);
            ImGui::Render();
            if (!drawn) { return false; }
            // The backend restores its common state; this guard supplies its missing bindings
            AdditionalState saved(context_);
            context_->OMSetRenderTargets(1, &target_, nullptr);
            ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
            return true;
        });
}

void Renderer::BeforeResize(IDXGISwapChain* chain) noexcept {
    if (chain == chain_) { Release(target_); }
}

IDXGISwapChain* Renderer::CloseOnPresentThread(const MenuSnapshot* closedSnapshot) noexcept {
    // UI and runtime workers cannot destroy resources created by the presenting thread
    if (thread_ != 0 && GetCurrentThreadId() != thread_) { return nullptr; }
    // Copy closed ownership before the graphics reset; the presenting thread owns both
    ObserveClosedBasicScroller(basicScrollerHistory_, closedSnapshot);
    instructionHudLifetime_.Observe(closedSnapshot);
    if (closedSnapshot) chromeColorHistory_.Update(*closedSnapshot,std::nullopt,GetTickCount64());
    IDXGISwapChain* retired = chain_;
    chain_ = nullptr;
    Reset();
    return retired;
}

void Renderer::Reset() noexcept {
    widthAnimation_.Reset();
    scrollPresentation_.Reset();
    creditsClock_.Reset();
    if (imgui_) {
        ImGuiContext* previous = ImGui::GetCurrentContext();
        ImGui::SetCurrentContext(imgui_);
        if (backendReady_) { ImGui_ImplDX11_Shutdown(); }
        ImGui::DestroyContext(imgui_);
        ImGui::SetCurrentContext(previous == imgui_ ? nullptr : previous);
    }
    imgui_ = nullptr; backendReady_ = false;
    Release(target_); Release(context_); Release(device_); Release(chain_);
    // Keep the first presenting thread as the lifetime owner across device recreation
    window_ = nullptr;
    featureLevel_ = 0; width_ = 0; height_ = 0;
}

}
