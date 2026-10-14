#include "PipelineSentinel.h"
#include <array>
#include <cstring>
#include <d3dcompiler.h>

namespace iw::overlay::tests {
namespace {
template<class T> void Release(T*& value) { if (value) { value->Release(); value = nullptr; } }
template<class T> bool Same(T* observed, T* expected) {
    bool equal = observed == expected;
    Release(observed);
    return equal;
}
ID3DBlob* Compile(const char* source, const char* target) {
    ID3DBlob* code = nullptr;
    ID3DBlob* diagnostic = nullptr;
    HRESULT result = D3DCompile(source, std::strlen(source), "fixture", nullptr, nullptr, "Main", target,
        D3DCOMPILE_ENABLE_STRICTNESS, 0, &code, &diagnostic);
    Release(diagnostic);
    if (FAILED(result)) { Release(code); }
    return code;
}
}

PipelineSentinel::~PipelineSentinel() {
    for (auto& view : targets_) { Release(view); }
    Release(depth_); Release(resource_); Release(pixelUav_); Release(computeUav_);
    Release(hull_); Release(domain_); Release(compute_); Release(vertex_); Release(pixel_);
    Release(raster_); Release(blend_); Release(depthState_);
}

bool PipelineSentinel::Create(ID3D11Device* device, std::string& error) {
    D3D11_TEXTURE2D_DESC texture{};
    texture.Width = 64; texture.Height = 64; texture.MipLevels = 1; texture.ArraySize = 1;
    texture.Format = DXGI_FORMAT_R8G8B8A8_UNORM; texture.SampleDesc.Count = 1;
    texture.BindFlags = D3D11_BIND_RENDER_TARGET;
    for (UINT index : {0u, 2u}) {
        ID3D11Texture2D* value = nullptr;
        HRESULT result = device->CreateTexture2D(&texture, nullptr, &value);
        if (SUCCEEDED(result)) { result = device->CreateRenderTargetView(value, nullptr, &targets_[index]); }
        Release(value);
        if (FAILED(result)) { error = "Sentinel render target creation failed"; return false; }
    }
    texture.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
    texture.BindFlags = D3D11_BIND_DEPTH_STENCIL;
    ID3D11Texture2D* value = nullptr;
    HRESULT result = device->CreateTexture2D(&texture, nullptr, &value);
    if (SUCCEEDED(result)) { result = device->CreateDepthStencilView(value, nullptr, &depth_); }
    Release(value);
    if (FAILED(result)) { error = "Sentinel depth creation failed"; return false; }
    texture.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    texture.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    result = device->CreateTexture2D(&texture, nullptr, &value);
    if (SUCCEEDED(result)) { result = device->CreateShaderResourceView(value, nullptr, &resource_); }
    Release(value);
    if (FAILED(result)) { error = "Sentinel resource creation failed"; return false; }
    // Separate buffers avoid asking D3D11 to read and write the same resource at once
    for (auto* destination : {&pixelUav_, &computeUav_}) {
        D3D11_BUFFER_DESC buffer{};
        buffer.ByteWidth = 64; buffer.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
        buffer.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED; buffer.StructureByteStride = 4;
        ID3D11Buffer* owned = nullptr;
        result = device->CreateBuffer(&buffer, nullptr, &owned);
        if (SUCCEEDED(result)) { result = device->CreateUnorderedAccessView(owned, nullptr, destination); }
        Release(owned);
        if (FAILED(result)) { error = "Sentinel UAV creation failed"; return false; }
    }
    const char* hullCode = R"(
        struct Point { float3 position : POSITION; };
        struct Constant { float edge[3] : SV_TessFactor; float inside : SV_InsideTessFactor; };
        Constant Patch(InputPatch<Point, 3> p, uint id : SV_PrimitiveID) {
            Constant c; c.edge[0]=1; c.edge[1]=1; c.edge[2]=1; c.inside=1; return c;
        }
        [domain("tri")][partitioning("integer")][outputtopology("triangle_cw")]
        [outputcontrolpoints(3)][patchconstantfunc("Patch")]
        Point Main(InputPatch<Point,3> p, uint i:SV_OutputControlPointID, uint id:SV_PrimitiveID) { return p[i]; }
    )";
    const char* domainCode = R"(
        struct Point { float3 position : POSITION; };
        struct Constant { float edge[3] : SV_TessFactor; float inside : SV_InsideTessFactor; };
        [domain("tri")]
        float4 Main(Constant c, float3 uv:SV_DomainLocation, const OutputPatch<Point,3> p):SV_Position {
            return float4(p[0].position,1);
        }
    )";
    ID3DBlob* code = Compile(hullCode, "hs_5_0");
    if (!code) { error = "Sentinel hull shader compile failed"; return false; }
    result = device->CreateHullShader(code->GetBufferPointer(), code->GetBufferSize(), nullptr, &hull_);
    Release(code);
    if (FAILED(result)) { error = "Sentinel hull shader creation failed"; return false; }
    code = Compile(domainCode, "ds_5_0");
    if (!code) { error = "Sentinel domain shader compile failed"; return false; }
    result = device->CreateDomainShader(code->GetBufferPointer(), code->GetBufferSize(), nullptr, &domain_);
    Release(code);
    if (FAILED(result)) { error = "Sentinel domain shader creation failed"; return false; }
    code = Compile("[numthreads(1,1,1)] void Main(uint3 id:SV_DispatchThreadID) {}", "cs_5_0");
    if (!code) { error = "Sentinel compute shader compile failed"; return false; }
    result = device->CreateComputeShader(code->GetBufferPointer(), code->GetBufferSize(), nullptr, &compute_);
    Release(code);
    if (FAILED(result)) { error = "Sentinel compute shader creation failed"; return false; }
    code = Compile("float4 Main(uint id:SV_VertexID):SV_Position { return float4(0,0,0,1); }", "vs_5_0");
    if (!code) { error = "Sentinel vertex shader compile failed"; return false; }
    result = device->CreateVertexShader(code->GetBufferPointer(), code->GetBufferSize(), nullptr, &vertex_);
    Release(code);
    if (FAILED(result)) { error = "Sentinel vertex shader creation failed"; return false; }
    code = Compile("float4 Main():SV_Target { return float4(1,0,0,1); }", "ps_5_0");
    if (!code) { error = "Sentinel pixel shader compile failed"; return false; }
    result = device->CreatePixelShader(code->GetBufferPointer(), code->GetBufferSize(), nullptr, &pixel_);
    Release(code);
    if (FAILED(result)) { error = "Sentinel pixel shader creation failed"; return false; }
    D3D11_RASTERIZER_DESC raster{};
    raster.FillMode = D3D11_FILL_WIREFRAME; raster.CullMode = D3D11_CULL_FRONT;
    raster.DepthClipEnable = TRUE; raster.ScissorEnable = TRUE;
    D3D11_BLEND_DESC blend{};
    blend.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
    blend.RenderTarget[0].DestBlend = D3D11_BLEND_ZERO;
    blend.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
    blend.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
    blend.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
    blend.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
    blend.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_RED;
    D3D11_DEPTH_STENCIL_DESC depth{};
    depth.DepthEnable = TRUE; depth.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
    depth.DepthFunc = D3D11_COMPARISON_GREATER;
    depth.StencilReadMask = D3D11_DEFAULT_STENCIL_READ_MASK;
    depth.StencilWriteMask = D3D11_DEFAULT_STENCIL_WRITE_MASK;
    depth.FrontFace = {D3D11_STENCIL_OP_KEEP, D3D11_STENCIL_OP_KEEP, D3D11_STENCIL_OP_KEEP, D3D11_COMPARISON_ALWAYS};
    depth.BackFace = depth.FrontFace;
    if (FAILED(device->CreateRasterizerState(&raster, &raster_))
        || FAILED(device->CreateBlendState(&blend, &blend_))
        || FAILED(device->CreateDepthStencilState(&depth, &depthState_))) {
        error = "Sentinel fixed pipeline state creation failed"; return false;
    }
    return true;
}

void PipelineSentinel::Bind(ID3D11DeviceContext* context) {
    context->OMSetRenderTargets(3, targets_, depth_);
    const UINT keep = static_cast<UINT>(-1);
    context->OMSetRenderTargetsAndUnorderedAccessViews(D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL,
        nullptr, nullptr, 3, 1, &pixelUav_, &keep);
    context->CSSetUnorderedAccessViews(7, 1, &computeUav_, &keep);
    context->VSSetShaderResources(127, 1, &resource_);
    context->HSSetShaderResources(127, 1, &resource_);
    context->DSSetShaderResources(127, 1, &resource_);
    context->GSSetShaderResources(127, 1, &resource_);
    context->PSSetShaderResources(127, 1, &resource_);
    context->CSSetShaderResources(127, 1, &resource_);
    context->HSSetShader(hull_, nullptr, 0); context->DSSetShader(domain_, nullptr, 0);
    context->CSSetShader(compute_, nullptr, 0); context->VSSetShader(vertex_, nullptr, 0);
    context->PSSetShader(pixel_, nullptr, 0);
    context->RSSetState(raster_);
    context->OMSetBlendState(blend_, nullptr, 0x12345678);
    context->OMSetDepthStencilState(depthState_, 23);
    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_3_CONTROL_POINT_PATCHLIST);
}

bool PipelineSentinel::Matches(ID3D11DeviceContext* context, std::string& error) {
    bool matched = true;
    std::array<ID3D11RenderTargetView*, 3> targets{};
    ID3D11DepthStencilView* depth = nullptr;
    context->OMGetRenderTargets(3, targets.data(), &depth);
    for (std::size_t index = 0; index < targets.size(); ++index) {
        matched = Same(targets[index], targets_[index]) && matched;
    }
    matched = Same(depth, depth_) && matched;
    ID3D11HullShader* hull = nullptr; ID3D11DomainShader* domain = nullptr;
    ID3D11ComputeShader* compute = nullptr; ID3D11VertexShader* vertex = nullptr; ID3D11PixelShader* pixel = nullptr;
    context->HSGetShader(&hull, nullptr, nullptr); context->DSGetShader(&domain, nullptr, nullptr);
    context->CSGetShader(&compute, nullptr, nullptr); context->VSGetShader(&vertex, nullptr, nullptr);
    context->PSGetShader(&pixel, nullptr, nullptr);
    matched = Same(hull, hull_) && Same(domain, domain_) && Same(compute, compute_)
        && Same(vertex, vertex_) && Same(pixel, pixel_) && matched;
    // Check a high resource slot that the common ImGui backup does not cover
    for (UINT stage = 0; stage < 6; ++stage) {
        ID3D11ShaderResourceView* resource = nullptr;
        switch (stage) {
        case 0: context->VSGetShaderResources(127, 1, &resource); break;
        case 1: context->HSGetShaderResources(127, 1, &resource); break;
        case 2: context->DSGetShaderResources(127, 1, &resource); break;
        case 3: context->GSGetShaderResources(127, 1, &resource); break;
        case 4: context->PSGetShaderResources(127, 1, &resource); break;
        default: context->CSGetShaderResources(127, 1, &resource); break;
        }
        matched = Same(resource, resource_) && matched;
    }
    ID3D11UnorderedAccessView* pixelUav = nullptr; ID3D11UnorderedAccessView* computeUav = nullptr;
    context->OMGetRenderTargetsAndUnorderedAccessViews(0, nullptr, nullptr, 3, 1, &pixelUav);
    context->CSGetUnorderedAccessViews(7, 1, &computeUav);
    matched = Same(pixelUav, pixelUav_) && Same(computeUav, computeUav_) && matched;
    ID3D11RasterizerState* raster = nullptr; ID3D11BlendState* blend = nullptr; ID3D11DepthStencilState* depthState = nullptr;
    context->RSGetState(&raster);
    UINT mask = 0, reference = 0;
    context->OMGetBlendState(&blend, nullptr, &mask); context->OMGetDepthStencilState(&depthState, &reference);
    matched = Same(raster, raster_) && Same(blend, blend_) && Same(depthState, depthState_)
        && mask == 0x12345678 && reference == 23 && matched;
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    context->IAGetPrimitiveTopology(&topology);
    matched = topology == D3D11_PRIMITIVE_TOPOLOGY_3_CONTROL_POINT_PATCHLIST && matched;
    if (!matched) { error = "The overlay changed one or more real D3D11 sentinel bindings"; }
    return matched;
}

}
