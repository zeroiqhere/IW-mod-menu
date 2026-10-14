#include "ScrollPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include <iostream>
#include <algorithm>
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/ScrollRows.h"
#include <imgui_internal.h>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool value, const char* reason) { if (!value) throw std::runtime_error(reason); }
class CpuPanel final {
public:
    CpuPanel() {
        context_ = ImGui::CreateContext(); ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO(); io.IniFilename = nullptr; io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~CpuPanel() { ImGui::DestroyContext(context_); }
    ImDrawData& Draw(const MenuSnapshot& model, const ScrollFrame* rows, float width = 640, float height = 480) {
        // Texture updates follow the official dynamic font-atlas contract without a GPU
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(model, nullptr, std::nullopt, std::nullopt, rows); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
    }
    struct AtlasStats {
        int baked, width, height, textures;
        bool operator==(const AtlasStats&) const = default;
    };
    AtlasStats Atlas() {
        ImGui::SetCurrentContext(context_); const auto& atlas = *ImGui::GetIO().Fonts;
        return {atlas.Builder->BakedPool.Size, atlas.TexData->Width, atlas.TexData->Height, atlas.TexList.Size};
    }
    bool HasBakedSize(float size) {
        ImGui::SetCurrentContext(context_); const auto& pool = ImGui::GetIO().Fonts->Builder->BakedPool;
        for (int i = 0; i < pool.Size; ++i) if (pool[i].Size == size && !pool[i].WantDestroy) return true;
        return false;
    }
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model(std::size_t count = 37) {
    MenuSnapshot model; model.isOpen = true; model.title = "Scroll fixture";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "No game is connected";
    model.menuRevision = 5; model.presentationFocusRevision = 0;
    model.rows.resize(count, {"Current player", "ON", true}); return model;
}
ScrollFrame Half(ScrollPresentation& animation, MenuSnapshot& model, std::size_t initial, std::size_t selected,
    float width = 640, float height = 480) {
    model.selectedIndex = initial; animation.Update(MakeScrollRequest(model, width, height), 0);
    model.selectedIndex = selected; animation.Update(MakeScrollRequest(model, width, height), 100);
    return animation.Update(MakeScrollRequest(model, width, height), 160);
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right()
            && vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) return true;
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000, "Scroll drawing work exceeded its fixed bound");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y), "Scroll emitted nonfinite vertices");
        for (const auto& command : list->CmdBuffer)
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height, "Scroll escaped its viewport clip");
    }
}
}
void AddScrollPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Window shifts draw common rows between slots and stationary faded edge boxes", [] {
        for (bool up : {false, true}) {
            CpuPanel panel; auto model = Model(); ScrollPresentation animation;
            const auto half = Half(animation, model, up ? 5 : 4, up ? 4 : 5);
            const auto& data = panel.Draw(model, &half);
            Require(HasColorIn(data, IM_COL32(64, 64, 64, 102), {247, 97, 10, 10}),
                "The common row did not move to its half-slot position");
            Require(HasColorIn(data, IM_COL32(64, 64, 64, 51), {247, 88, 10, 10})
                && HasColorIn(data, IM_COL32(64, 64, 64, 51), {247, 250, 10, 10}),
                "Source stationary outgoing and incoming edges did not fade at their slots");
        }
    });
    checks.emplace_back("Retiring rows use current feature values rather than cached ON state", [] {
        CpuPanel panel; auto model = Model(); ScrollPresentation animation; const auto half = Half(animation, model, 4, 5);
        model.rows[0].value = "UNKNOWN"; const auto& data = panel.Draw(model, &half);
        Require(!HasColorIn(data, IM_COL32(57, 152, 254, 51), {248, 89, 8, 8}),
            "A retired row displayed an old confirmed ON fill");
        Require(HasColorIn(data, IM_COL32(255, 255, 255, 51), {247, 85, 10, 16}),
            "A current UNKNOWN state lost its explicit question marker");
    });
    checks.emplace_back("Retiring row labels are read from the current snapshot on each frame", [] {
        auto model = Model(); ScrollPresentation animation; const auto half = Half(animation, model, 4, 5);
        CpuPanel changed; model.rows[0].label = "UPDATED";
        auto changedPixels = changed.textures.Raster(changed.Draw(model, &half), 640, 480);
        CpuPanel blank; model.rows[0].label.clear();
        auto blankPixels = blank.textures.Raster(blank.Draw(model, &half), 640, 480);
        bool differs = false;
        for (int y = 85; y < 103; ++y) for (int x = 4; x < 80; ++x)
            for (int channel = 0; channel < 3; ++channel)
                differs |= changedPixels[(x + y * 640) * 3 + channel] != blankPixels[(x + y * 640) * 3 + channel];
        Require(differs, "The outgoing row label was cached rather than reread");
    });
    checks.emplace_back("Large cursor interpolates text size while boolean box geometry stays ten and eight units", [] {
        CpuPanel panel; auto model = Model(2); model.appearance.largeCursor = true; ScrollPresentation animation;
        const auto half = Half(animation, model, 0, 1); const auto& data = panel.Draw(model, &half);
        Require(panel.HasBakedSize(14), "A halfway large cursor did not request the interpolated text size");
        Require(HasColorIn(data, IM_COL32(64, 64, 64, 178), {247, 88, 0.2f, 0.2f})
            && HasColorIn(data, IM_COL32(64, 64, 64, 178), {256.9f, 97.9f, 0.2f, 0.2f}),
            "Large cursor scaled the source fixed ten-unit box");
    });
    checks.emplace_back("Malformed presentation frames safely fall back to the confirmed static row", [] {
        auto model = Model(8); model.selectedIndex = 1;
        for (unsigned failure = 0; failure < 4; ++failure) {
            auto bad = StaticScrollFrame(MakeScrollRequest(model, 640, 480));
            if (failure == 0) bad.rows[0].position = std::numeric_limits<float>::quiet_NaN();
            if (failure == 1) bad.rows[1].index = bad.rows[0].index;
            if (failure == 2) bad.count = MaximumScrollRows + 1;
            if (failure == 3) bad.scroller = -1;
            CpuPanel panel; const auto& data = panel.Draw(model, &bad);
            Require(HasColorIn(data, IM_COL32(55, 55, 55, 255), {0, 102.9f, 260, .2f}),
                "Invalid local presentation changed the confirmed cursor"); FiniteClips(data, 640, 480);
        }
        layout::Hud empty; ScrollFrame frame;
        Require(MatchesScrollFrame(frame, empty, ScrollRequest{}), "An empty frame used unsigned row subtraction");
    });
    checks.emplace_back("Editor unlock close and focus changes discard previous row motion", [] {
        for (unsigned context = 0; context < 5; ++context) {
            auto model = Model(8); ScrollPresentation animation; Half(animation, model, 0, 1);
            if (context == 0) model.widthEditor = WidthEditor{};
            if (context == 1) model.instructionPositionEditor = InstructionPositionEditor{};
            if (context == 2) model.menuUnlock = MenuUnlockPhase::Pause;
            if (context == 3) model.isOpen = false;
            if (context == 4) model.presentationFocusRevision = 1;
            const auto settled = animation.Update(MakeScrollRequest(model, 640, 480), 180);
            Require(!settled.transitioning, "A noninteractive or new-focus context retained scroll motion");
        }
    });
    checks.emplace_back("Every source capacity clips moving Unicode rows across tested viewports", [] {
        for (bool boxes : {false, true}) for (bool instructions : {false, true}) for (bool counter : {false, true}) {
            for (const auto dimensions : {ImVec2{640, 480}, ImVec2{960, 540}, ImVec2{360, 640}}) {
                CpuPanel panel; auto model = Model(); model.appearance.booleanDisplay = boxes ? "Boxes" : "Text";
                model.appearance.instructionsVisible = instructions; model.appearance.optionCounter = counter;
                model.rows[0].label = std::string(600, 'W') + " Ä🌈"; ScrollPresentation animation;
                const auto request = MakeScrollRequest(model, dimensions.x, dimensions.y);
                const auto half = Half(animation, model, request.shown / 2 - 1, request.shown / 2, dimensions.x, dimensions.y);
                FiniteClips(panel.Draw(model, &half, dimensions.x, dimensions.y), dimensions.x, dimensions.y);
            }
        }
    });
    checks.emplace_back("One thousand scrolling frames stabilize font bakes and texture size after warmup", [] {
        CpuPanel panel; auto model = Model(); model.appearance.largeCursor = true; ScrollPresentation animation;
        CpuPanel::AtlasStats warm{}; unsigned minimum = 100, maximum = 0;
        for (unsigned frame = 0; frame < 1000; ++frame) {
            const unsigned tick = frame % 26; const unsigned cycle = frame / 26;
            model.selectedIndex = cycle % 2 ? 4 : 5;
            const auto sampled = animation.Update(MakeScrollRequest(model, 640, 480), frame * 5);
            panel.Draw(model, &sampled); const auto stats = panel.Atlas();
            if (frame == 300) warm = stats;
            if (frame > 300) Require(stats == warm, "Repeated scroll motion grew or replaced a warmed font atlas");
            if (tick > 0 && tick < 24) for (std::size_t row = 0; row < sampled.count; ++row) {
                const unsigned size = static_cast<unsigned>(std::lround(13 * (1 + .2f * sampled.rows[row].selection)));
                minimum = std::min(minimum, size); maximum = std::max(maximum, size);
            }
        }
        Require(minimum == 13 && maximum == 16 && panel.HasBakedSize(14) && panel.HasBakedSize(15),
            "The sustained atlas check did not exercise changing interpolated font sizes");
    });
}
void VerifyScrollBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    const auto read = [&](const char* name) { return ReadBackendPreview(fixtures / name); };
    const auto draft = read("preferences-scroll-draft.json");
    const auto committed = read("preferences-scroll-committed.json");
    const auto minimum = read("preferences-scroll-min.json");
    const auto maximum = read("preferences-scroll-max.json");
    const auto durationValue = [](const MenuSnapshot& model) {
        const auto row = std::find_if(model.rows.begin(), model.rows.end(), [](const auto& value) { return value.label == "Scroll Animation Time (ms)"; });
        Require(row != model.rows.end(), "Actual preferences lost the scroll duration row"); return row->value;
    };
    Require(draft.appearance.scrollAnimationMilliseconds == 120 && durationValue(draft) == "130 ms"
        && committed.appearance.scrollAnimationMilliseconds == 130 && durationValue(committed) == "130 ms"
        && minimum.appearance.scrollAnimationMilliseconds == 100 && maximum.appearance.scrollAnimationMilliseconds == 250,
        "Actual scroll draft and committed duration fields were conflated");
    auto start = read("scroll-motion-root-start.json");
    auto moved = read("scroll-motion-root-down.json");
    auto reopened = read("scroll-motion-root-reopened.json");
    auto colors = read("scroll-motion-colors-start.json");
    auto window = read("scroll-motion-colors-window.json");
    auto parent = read("scroll-motion-customization.json");
    Require(start.menuRevision && start.menuRevision == moved.menuRevision
        && start.navigationRevision != moved.navigationRevision && reopened.menuRevision != moved.menuRevision
        && colors.menuRevision == window.menuRevision && parent.menuRevision != window.menuRevision,
        "Actual cursor and menu context revisions were conflated");
    // Private focus observations are supplied by the native worker, never by backend JSON
    for (auto* model : {&start, &moved, &reopened, &colors, &window, &parent}) {
        Require(!model->presentationFocusRevision, "Producer JSON supplied a private renderer focus epoch");
        model->presentationFocusRevision = 0;
    }
    ScrollPresentation animation;
    animation.Update(MakeScrollRequest(start, 960, 720), 0);
    animation.Update(MakeScrollRequest(moved, 960, 720), 100);
    const auto rootHalf = animation.Update(MakeScrollRequest(moved, 960, 720), 160);
    Require(rootHalf.transitioning && std::abs(rootHalf.scroller - .5f) < .001f,
        "Actual adjacent producer cursors did not animate within their shared menu epoch");
    const auto opening = animation.Update(MakeScrollRequest(reopened, 960, 720), 170);
    Require(!opening.transitioning && opening.scroller == 1, "A reopened actual menu retained previous cursor motion");
    animation.Reset(); animation.Update(MakeScrollRequest(colors, 960, 720), 0);
    animation.Update(MakeScrollRequest(window, 960, 720), 100);
    const auto edgeHalf = animation.Update(MakeScrollRequest(window, 960, 720), 160);
    Require(edgeHalf.transitioning && edgeHalf.count == 11 && edgeHalf.first == 1,
        "Actual color page window movement did not retain its stationary fading edge");
    const auto returned = animation.Update(MakeScrollRequest(parent, 960, 720), 170);
    Require(!returned.transitioning, "Actual Back context inherited color-page row motion");
    CpuPanel panel;
    const auto demo = [&](MenuSnapshot model, const ScrollFrame& rows, const char* name, const char* notice) {
        model.context = "PREVIEW / FIXED SCROLL CLOCK DEMO"; model.notice = notice;
        const auto& data = panel.Draw(model, &rows, 960, 720); FiniteClips(data, 960, 720);
        WritePreview(output / name, panel.textures.Raster(data, 960, 720), 960, 720);
    };
    demo(moved, rootHalf, "scroll-root-midpoint.ppm",
        "60 ms of inferred linear 120 ms cursor motion. Actual controller rows; declared private focus epoch. No game connected.");
    demo(window, edgeHalf, "scroll-window-midpoint.ppm",
        "60 ms of inferred linear 120 ms window motion. Common rows move; stationary edge rows fade. No game connected.");
    demo(window, StaticScrollFrame(MakeScrollRequest(window, 960, 720)), "scroll-window-complete.ppm",
        "Completed 120 ms window motion. Actual current rows and committed duration. No game connected.");
    std::cout << "PASS Actual backend scroll drafts, durations, context epochs and production motion previews\n";
}

}
