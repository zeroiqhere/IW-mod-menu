#pragma once
#include <imgui.h>
#include <filesystem>
#include <unordered_map>
#include <vector>

namespace iw::overlay::tests {
// Only test-owned CPU texture copies are used to inspect the production drawlist
class CpuTextures final {
public:
    void Update(ImDrawData& data);
    std::vector<unsigned char> Raster(const ImDrawData& data, int width, int height) const;
    unsigned Creates() const noexcept { return creates_; }
    unsigned Updates() const noexcept { return updates_; }
private:
    struct Texture { int width, height, bytes; std::vector<unsigned char> pixels; };
    std::unordered_map<ImTextureID, Texture> textures_;
    ImTextureID next_ = 1;
    unsigned creates_ = 0, updates_ = 0;
};
void WritePreview(const std::filesystem::path& path, const std::vector<unsigned char>& pixels, int width, int height);
}
