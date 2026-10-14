#pragma once
#include <d3d11.h>
#include <string>

namespace iw::overlay::tests {

// Real pipeline objects reveal bindings that an overlay accidentally clears
class PipelineSentinel final {
public:
    ~PipelineSentinel();
    bool Create(ID3D11Device* device, std::string& error);
    void Bind(ID3D11DeviceContext* context);
    bool Matches(ID3D11DeviceContext* context, std::string& error);
private:
    ID3D11RenderTargetView* targets_[3]{};
    ID3D11DepthStencilView* depth_ = nullptr;
    ID3D11ShaderResourceView* resource_ = nullptr;
    ID3D11UnorderedAccessView* pixelUav_ = nullptr;
    ID3D11UnorderedAccessView* computeUav_ = nullptr;
    ID3D11HullShader* hull_ = nullptr;
    ID3D11DomainShader* domain_ = nullptr;
    ID3D11ComputeShader* compute_ = nullptr;
    ID3D11VertexShader* vertex_ = nullptr;
    ID3D11PixelShader* pixel_ = nullptr;
    ID3D11RasterizerState* raster_ = nullptr;
    ID3D11BlendState* blend_ = nullptr;
    ID3D11DepthStencilState* depthState_ = nullptr;
};

}
