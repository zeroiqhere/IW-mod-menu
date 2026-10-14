#include "CpuDrawlist.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <fstream>
#include <stdexcept>

namespace iw::overlay::tests {
void CpuTextures::Update(ImDrawData& data) {
    if (!data.Textures) { return; }
    for (auto* texture : *data.Textures) {
        if (texture->Status == ImTextureStatus_WantCreate) {
            texture->SetTexID(next_++); ++creates_;
            auto* begin = static_cast<unsigned char*>(texture->GetPixels());
            textures_[texture->GetTexID()] = {texture->Width, texture->Height, texture->BytesPerPixel,
                std::vector<unsigned char>(begin, begin + texture->GetSizeInBytes())};
            // Honor the same 1.92 texture request contract used by the production DX11 backend
            texture->SetStatus(ImTextureStatus_OK);
        } else if (texture->Status == ImTextureStatus_WantUpdates) {
            auto& stored = textures_.at(texture->GetTexID());
            ++updates_;
            // Only declared update regions change, matching the maintained DX11 backend
            for (const auto& rect : texture->Updates) {
                for (int y = rect.y; y < rect.y + rect.h; ++y) {
                    const auto* start = static_cast<const unsigned char*>(texture->GetPixelsAt(rect.x, y));
                    const auto offset = static_cast<std::size_t>(rect.x + y * stored.width) * stored.bytes;
                    std::copy_n(start, rect.w * stored.bytes, stored.pixels.begin() + offset);
                }
            }
            texture->SetStatus(ImTextureStatus_OK);
        } else if (texture->Status == ImTextureStatus_WantDestroy && texture->UnusedFrames > 0) {
            // An atlas retired during this frame can still be referenced by its draw commands
            textures_.erase(texture->GetTexID());
            texture->SetTexID(ImTextureID_Invalid); texture->SetStatus(ImTextureStatus_Destroyed);
        }
    }
}

namespace {
float Edge(ImVec2 a, ImVec2 b, ImVec2 p) { return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x); }
bool TopLeft(ImVec2 a, ImVec2 b) { return b.y < a.y || (b.y == a.y && b.x > a.x); }
std::array<float, 4> Tint(ImU32 color) {
    return {float((color >> IM_COL32_R_SHIFT) & 255), float((color >> IM_COL32_G_SHIFT) & 255),
        float((color >> IM_COL32_B_SHIFT) & 255), float((color >> IM_COL32_A_SHIFT) & 255)};
}
}

std::vector<unsigned char> CpuTextures::Raster(const ImDrawData& data, int width, int height) const {
    if (width <= 0 || height <= 0 || width > 4096 || height > 4096) { throw std::runtime_error("CPU preview dimensions are invalid"); }
    std::vector<unsigned char> image(static_cast<std::size_t>(width) * height * 3);
    for (std::size_t i = 0; i < image.size(); i += 3) { image[i] = 12; image[i + 1] = 18; image[i + 2] = 25; }
    for (auto* list : data.CmdLists) {
        for (const auto& command : list->CmdBuffer) {
            if (command.UserCallback) { throw std::runtime_error("The production panel unexpectedly requested a draw callback"); }
            const auto& texture = textures_.at(command.GetTexID());
            auto pixel = [&](int x, int y, int component) {
                x = std::clamp(x, 0, texture.width - 1); y = std::clamp(y, 0, texture.height - 1);
                std::size_t offset = static_cast<std::size_t>(x + y * texture.width) * texture.bytes;
                return texture.bytes == 1 ? component == 3 ? float(texture.pixels[offset]) : 255.0f
                    : float(texture.pixels[offset + component]);
            };
            auto sample = [&](ImVec2 uv, int component) {
                // Bilinear atlas sampling follows the renderer's font texture filter
                float x = uv.x * texture.width - 0.5f, y = uv.y * texture.height - 0.5f;
                int ix = static_cast<int>(std::floor(x)), iy = static_cast<int>(std::floor(y));
                float fx = x - ix, fy = y - iy;
                return (pixel(ix, iy, component) * (1 - fx) + pixel(ix + 1, iy, component) * fx) * (1 - fy)
                    + (pixel(ix, iy + 1, component) * (1 - fx) + pixel(ix + 1, iy + 1, component) * fx) * fy;
            };
            for (unsigned index = 0; index + 2 < command.ElemCount; index += 3) {
                // Use the production command offsets and vertices without rebuilding any menu geometry
                ImDrawVert a = list->VtxBuffer[list->IdxBuffer[command.IdxOffset + index] + command.VtxOffset];
                ImDrawVert b = list->VtxBuffer[list->IdxBuffer[command.IdxOffset + index + 1] + command.VtxOffset];
                ImDrawVert c = list->VtxBuffer[list->IdxBuffer[command.IdxOffset + index + 2] + command.VtxOffset];
                float area = Edge(a.pos, b.pos, c.pos);
                if (area < 0) { std::swap(b, c); area = -area; }
                if (area < 0.000001f) { continue; }
                int left = std::max(0, int(std::floor(std::max(command.ClipRect.x, std::min({a.pos.x, b.pos.x, c.pos.x})))));
                int top = std::max(0, int(std::floor(std::max(command.ClipRect.y, std::min({a.pos.y, b.pos.y, c.pos.y})))));
                int right = std::min(width, int(std::ceil(std::min(command.ClipRect.z, std::max({a.pos.x, b.pos.x, c.pos.x})))));
                int bottom = std::min(height, int(std::ceil(std::min(command.ClipRect.w, std::max({a.pos.y, b.pos.y, c.pos.y})))));
                auto ca = Tint(a.col), cb = Tint(b.col), cc = Tint(c.col);
                // A top-left edge rule prevents shared triangle edges from blending twice
                for (int y = top; y < bottom; ++y) { for (int x = left; x < right; ++x) {
                    ImVec2 point{float(x) + 0.5f, float(y) + 0.5f};
                    if (point.x < command.ClipRect.x || point.x >= command.ClipRect.z
                        || point.y < command.ClipRect.y || point.y >= command.ClipRect.w) { continue; }
                    float ea = Edge(b.pos, c.pos, point), eb = Edge(c.pos, a.pos, point), ec = Edge(a.pos, b.pos, point);
                    if (ea < 0 || eb < 0 || ec < 0 || (ea == 0 && !TopLeft(b.pos, c.pos))
                        || (eb == 0 && !TopLeft(c.pos, a.pos)) || (ec == 0 && !TopLeft(a.pos, b.pos))) { continue; }
                    float wa = ea / area, wb = eb / area, wc = ec / area;
                    ImVec2 uv{a.uv.x * wa + b.uv.x * wb + c.uv.x * wc, a.uv.y * wa + b.uv.y * wb + c.uv.y * wc};
                    float alpha = (ca[3] * wa + cb[3] * wb + cc[3] * wc) * sample(uv, 3) / (255 * 255);
                    std::size_t offset = static_cast<std::size_t>(x + y * width) * 3;
                    for (int component = 0; component < 3; ++component) {
                        // Blend vertex tint and atlas coverage onto the preview background
                        float color = (ca[component] * wa + cb[component] * wb + cc[component] * wc) * sample(uv, component) / 255;
                        image[offset + component] = static_cast<unsigned char>(std::clamp(std::lround(color * alpha + image[offset + component] * (1 - alpha)), 0L, 255L));
                    }
                }}
            }
        }
    }
    return image;
}

void WritePreview(const std::filesystem::path& path, const std::vector<unsigned char>& pixels, int width, int height) {
    std::ofstream output(path, std::ios::binary);
    output << "P6\n" << width << ' ' << height << "\n255\n";
    output.write(reinterpret_cast<const char*>(pixels.data()), static_cast<std::streamsize>(pixels.size()));
    if (!output) { throw std::runtime_error("The CPU drawlist preview could not be saved"); }
}
}
