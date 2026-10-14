#include "OpeningPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/designs/DesignText.h"
#include "../../native/render/ScrollRows.h"
#include <imgui_internal.h>
#include <array>
#include <cmath>
#include <iostream>
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
    ImDrawData& Draw(const MenuSnapshot& model, float width = 640, float height = 480,
        const ScrollFrame* rows = nullptr, std::optional<layout::Color> color = std::nullopt) {
        // The production panel and official texture-update contract run without a graphics backend
        ImGui::SetCurrentContext(context_); auto& io = ImGui::GetIO(); io.DisplaySize = {width, height}; io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame(); DrawApparitionPanel(model, nullptr, std::nullopt, color, rows); ImGui::Render();
        auto& data = *ImGui::GetDrawData(); textures.Update(data); return data;
    }
    std::array<int, 4> Atlas() {
        // Internal atlas counters are inspected only in this regression, never in the renderer
        ImGui::SetCurrentContext(context_); const auto& atlas = *ImGui::GetIO().Fonts;
        return {atlas.Builder->BakedPool.Size, atlas.TexData->Width, atlas.TexData->Height, atlas.TexList.Size};
    }
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model(MenuDesign design = MenuDesign::Apparition) {
    MenuSnapshot model; model.isOpen = true; model.isRootMenu = false; model.title = "Menu Preferences";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "No game is connected";
    model.appearance.design = design; model.menuRevision = 3; model.presentationFocusRevision = 0;
    model.rows.resize(8, {"Current preference", "OFF", true});
    model.appearance.coloredCursor = false; model.menuOpening = MenuOpening{1, 0}; return model;
}
bool HasColorIn(const ImDrawData& data, ImU32 color, layout::Rect area) {
    for (auto* list : data.CmdLists) for (const auto& vertex : list->VtxBuffer)
        if (vertex.col == color && vertex.pos.x >= area.x && vertex.pos.x < area.Right()
            && vertex.pos.y >= area.y && vertex.pos.y < area.Bottom()) return true;
    return false;
}
void FiniteClips(const ImDrawData& data, float width, float height) {
    Require(data.TotalVtxCount < 100000 && data.TotalIdxCount < 150000, "A design exceeded bounded drawing work");
    for (auto* list : data.CmdLists) {
        for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y) && std::isfinite(vertex.uv.x)
                && std::isfinite(vertex.uv.y), "A design emitted nonfinite vertices");
        for (const auto& command : list->CmdBuffer)
            Require(command.ClipRect.x >= 0 && command.ClipRect.y >= 0 && command.ClipRect.z <= width
                && command.ClipRect.w <= height, "A design escaped its viewport clip");
    }
}
}
void AddOpeningPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Reported opening alpha affects current row text and boxes without hiding the panel", [] {
        CpuPanel panel; auto model = Model();
        Require(!HasColorIn(panel.Draw(model), IM_COL32(255, 255, 255, 102), {4, 103, 190, 18}),
            "Opening rows appeared fully opaque instead of starting their fade");
        model.menuOpening->elapsedMilliseconds = 50;
        const auto& data = panel.Draw(model);
        Require(HasColorIn(data, IM_COL32(255, 255, 255, 51), {4, 103, 190, 18})
            && HasColorIn(data, IM_COL32(255, 255, 255, 25), {4, 121, 190, 18}), "Concurrent source ordinal alpha differs");
        Require(HasColorIn(data, IM_COL32(55, 55, 55, 255), {0, 65, 260, 19}), "Opening alpha hid its banner");
    });
    checks.emplace_back("All row value checkbox and green text channels retain RGB at their ordinal alpha", [] {
        CpuPanel panel; auto model = Model(); model.menuOpening->elapsedMilliseconds = 50;
        model.rows[1].value = "ON"; model.rows[2].value = "OFF"; model.rows[3].value = ">";
        model.rows[4].value = "< Classic >"; model.rows[5].value = "120 ms";
        const auto& boxes = panel.Draw(model);
        Require(HasColorIn(boxes, IM_COL32(57, 152, 254, 51), {248, 107, 8, 8})
            && HasColorIn(boxes, IM_COL32(64, 64, 64, 25), {247, 124, 10, 10})
            && HasColorIn(boxes, IM_COL32(0, 0, 0, 25), {248, 125, 8, 8}), "Opening checkbox layers lost their RGB or alpha");
        Require(HasColorIn(boxes, IM_COL32(255, 255, 255, 17), {200, 139, 56, 18})
            && HasColorIn(boxes, IM_COL32(255, 255, 255, 12), {160, 157, 96, 18})
            && HasColorIn(boxes, IM_COL32(255, 255, 255, 10), {200, 175, 56, 18}), "Opening submenu choice or numeric value alpha differs");
        model.appearance.booleanDisplay = "Text"; const auto& text = panel.Draw(model);
        Require(HasColorIn(text, IM_COL32(255, 255, 255, 51), {210, 103, 46, 18})
            && HasColorIn(text, IM_COL32(255, 255, 255, 25), {210, 121, 46, 18}), "Text boolean values did not fade");
        model.appearance.booleanDisplay = "Text Color";
        Require(HasColorIn(panel.Draw(model), IM_COL32(0, 255, 0, 51), {4, 103, 190, 18}), "Green ON labels did not retain source RGB at faded alpha");
    });
    checks.emplace_back("Opening does not fade title counter status or ordinary instruction channels", [] {
        CpuPanel panel; auto model = Model(); model.appearance.optionCounter = true;
        const auto opening = panel.textures.Raster(panel.Draw(model), 640, 480);
        model.menuOpening.reset(); const auto normal = panel.textures.Raster(panel.Draw(model), 640, 480);
        for (unsigned y = 0; y < 480; ++y) for (unsigned x = 0; x < 640; ++x) {
            if (!(y < 85 || x > 280 || y > 250)) continue;
            for (unsigned channel = 0; channel < 3; ++channel) {
                const auto pixel = (y * 640 + x) * 3 + channel;
                Require(opening[pixel] == normal[pixel], "Opening alpha leaked into another HUD channel");
            }
        }
    });
    checks.emplace_back("IW unavailable markers fade with opening and preserve their legacy endpoint on takeover", [] {
        CpuPanel panel; auto model = Model(); model.selectedIndex = 1; model.rows[2].enabled = false;
        Require(!HasColorIn(panel.Draw(model), IM_COL32(57, 152, 254, 102), {30, 128, 200, 8}),
            "An unavailable marker stayed visible while its label was hidden");
        ScrollPresentation motion; motion.Update(MakeScrollRequest(model, 640, 480), 0);
        model.menuOpening.reset(); model.selectedIndex = 2;
        motion.Update(MakeScrollRequest(model, 640, 480), 40);
        const auto half = motion.Update(MakeScrollRequest(model, 640, 480), 100);
        const auto& data = panel.Draw(model, 640, 480, &half);
        Require(HasColorIn(data, IM_COL32(57, 152, 254, 61), {30, 110, 200, 45}),
            "The unavailable marker did not linearly approach its legacy opacity endpoint");
    });
    checks.emplace_back("Opening takeover draws explicit linear alpha instead of multiplying style fades", [] {
        CpuPanel panel; auto model = Model(); model.selectedIndex = 1; ScrollPresentation motion;
        motion.Update(MakeScrollRequest(model, 640, 480), 0); model.selectedIndex = 2; model.menuOpening.reset();
        motion.Update(MakeScrollRequest(model, 640, 480), 40);
        const auto half = motion.Update(MakeScrollRequest(model, 640, 480), 100);
        Require(HasColorIn(panel.Draw(model, 640, 480, &half), IM_COL32(255, 255, 255, 137), {4, 121, 190, 18}),
            "The production painter used a quadratic opening and spotlight product");
    });
    checks.emplace_back("Stale context report elapsed and malformed alpha frames fall back to current owner data", [] {
        CpuPanel panel; auto model = Model(); model.menuOpening->elapsedMilliseconds = 50;
        const auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
        for (unsigned fault = 0; fault < 14; ++fault) {
            auto frame = StaticScrollFrame(MakeScrollRequest(model, 640, 480));
            if (fault == 0) frame.opening->revision++;
            if (fault == 1) frame.opening->elapsedMilliseconds--;
            if (fault == 2) frame.menuRevision = 99;
            if (fault == 3) frame.focusRevision = 99;
            if (fault == 4) frame.openingElapsed = std::numeric_limits<double>::quiet_NaN();
            if (fault == 5) frame.openingElapsed = 151;
            if (fault == 6) frame.rows[1].opacity = std::numeric_limits<float>::quiet_NaN();
            if (fault == 7) frame.rows[1].decorationVisibility = 2;
            if (fault == 8) frame.rows[1].opacity.reset();
            if (fault == 9) frame.rows[1].decorationVisibility.reset();
            if (fault == 10) frame.layout.viewportWidth++;
            if (fault == 11) frame.layout.bodyWidth++;
            if (fault == 12) frame.layout.menuX++;
            if (fault == 13) frame.layout.bodyY++;
            // A stale caller's visible weights make acceptance observable in the raster
            frame.rows[2].opacity = 1;
            Require(panel.textures.Raster(panel.Draw(model, 640, 480, &frame), 640, 480) == expected,
                "An unconfirmed opening frame changed current row ink");
        }
    });
    checks.emplace_back("A design cooldown without opening rejects a prior moving caller frame", [] {
        CpuPanel panel; auto model = Model(); model.menuOpening.reset(); ScrollPresentation motion;
        motion.Update(MakeScrollRequest(model, 640, 480), 0); model.selectedIndex = 1;
        motion.Update(MakeScrollRequest(model, 640, 480), 100);
        const auto old = motion.Update(MakeScrollRequest(model, 640, 480), 160);
        model.menuDesignTransition = MenuDesignTransitionPhase::Cooldown;
        const auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
        Require(panel.textures.Raster(panel.Draw(model, 640, 480, &old), 640, 480) == expected,
            "A same-design cooldown inherited an older moving cursor");
    });
    checks.emplace_back("Reported fallback and a legacy clock frame draw the same opening pixels", [] {
        CpuPanel panel; auto model = Model(); model.presentationFocusRevision.reset(); model.menuOpening->elapsedMilliseconds = 60;
        const auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
        ScrollPresentation motion; motion.Update(MakeScrollRequest(model, 640, 480), 0);
        const auto frame = motion.Update(MakeScrollRequest(model, 640, 480), 5000);
        Require(panel.textures.Raster(panel.Draw(model, 640, 480, &frame), 640, 480) == expected,
            "A legacy opening guessed a local clock phase");
        auto later = model; later.menuOpening->elapsedMilliseconds = 80;
        auto guessed = StaticScrollFrame(MakeScrollRequest(later, 640, 480)); guessed.opening = model.menuOpening;
        Require(panel.textures.Raster(panel.Draw(model, 640, 480, &guessed), 640, 480) == expected,
            "A caller injected a local phase into legacy opening metadata");
    });
    checks.emplace_back("Repeated opening and interrupted scroll frames keep warmed font textures bounded", [] {
        CpuPanel panel; ScrollPresentation motion; std::array<int, 4> warm{};
        for (unsigned tick = 0; tick < 600; ++tick) {
            auto model = Model(); model.menuOpening->revision = 1 + tick / 10;
            model.menuOpening->elapsedMilliseconds = (tick % 10) * 25;
            if (tick % 10 > 5) { model.menuOpening.reset(); model.selectedIndex = 1; }
            const auto frame = motion.Update(MakeScrollRequest(model, 640, 480), tick * 25);
            FiniteClips(panel.Draw(model, 640, 480, &frame), 640, 480); const auto atlas = panel.Atlas();
            if (tick == 100) warm = atlas;
            if (tick > 100) Require(atlas == warm, "Opening alpha grew warmed font textures");
        }
    });
}
void VerifyOpeningBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    const auto read = [&](const char* name) { return ReadBackendPreview(fixtures / (std::string("menu-opening-") + name + ".json")); };
    auto start = read("start"), progress = read("progress"), ready = read("input-ready"), last = read("last"), done = read("complete");
    const auto draft = read("draft"), same = read("same"), moved = read("scroll"), late = read("late");
    Require(start.menuOpening && start.menuOpening->revision && start.menuOpening->elapsedMilliseconds == 0
        && progress.menuOpening && progress.menuOpening->elapsedMilliseconds == 100
        && ready.menuOpening && ready.menuOpening->elapsedMilliseconds == 200
        && last.menuOpening && last.menuOpening->elapsedMilliseconds == 899 && !done.menuOpening,
        "Actual ten-row opening lifecycle differs from the owner reports");
    Require(start.menuDesignTransition == MenuDesignTransitionPhase::Cooldown && !ready.menuDesignTransition
        && start.menuRevision == ready.menuRevision && ready.menuRevision == done.menuRevision
        && ready.menuOpening == draft.menuOpening && ready.menuOpening == same.menuOpening
        && draft.menuRevision == ready.menuRevision && same.menuRevision == ready.menuRevision,
        "Draft no-op or input readiness incorrectly restarted the actual fade");
    Require(!moved.menuOpening && moved.menuRevision == ready.menuRevision && moved.selectedIndex == 1
        && moved.navigationRevision != ready.navigationRevision && late.menuOpening && late.menuOpening->elapsedMilliseconds == 0,
        "An accepted scroll or late redraw carried the wrong opening event");
    const auto eight = read("eight-start"), eightLast = read("eight-last"), eightDone = read("eight-complete");
    Require(eight.appearance.design == MenuDesign::Native && eight.appearance.optionCounter && eight.menuOpening
        && eightLast.menuOpening && eightLast.menuOpening->elapsedMilliseconds == 699 && !eightDone.menuOpening,
        "Actual counter-constrained opening did not finish at eight visible rows");
    CpuPanel panel; const auto initial = StaticScrollFrame(MakeScrollRequest(start, 640, 480));
    Require(initial.shown == 10 && initial.rows[1].opacity == 0
        && StaticScrollFrame(MakeScrollRequest(eight, 640, 480)).shown == 8, "Actual design capacity was guessed by the painter");
    // Private focus and local time are explicit test inputs, not fabricated producer packet fields
    ready.presentationFocusRevision = 0; auto movedOwned = moved; movedOwned.presentationFocusRevision = 0;
    ScrollPresentation motion; motion.Update(MakeScrollRequest(ready, 640, 480), 1000);
    motion.Update(MakeScrollRequest(movedOwned, 640, 480), 1000);
    const auto takeover = motion.Update(MakeScrollRequest(movedOwned, 640, 480), 1060);
    Require(takeover.transitioning && std::abs(*takeover.rows[8].opacity - .25f) < .001f,
        "Actual accepted scrolling did not capture the owner's current opening alpha");
    movedOwned.context = "PREVIEW / OPENING TAKEOVER CLOCK DEMO";
    movedOwned.notice = "Actual producer rows; explicit private focus epoch and 60 ms local scroll clock. No game is connected.";
    // Recreate the frame at the preview viewport so its geometry context is not borrowed from 640x480
    ScrollPresentation previewMotion; previewMotion.Update(MakeScrollRequest(ready, 960, 720), 1000);
    previewMotion.Update(MakeScrollRequest(movedOwned, 960, 720), 1000);
    const auto previewFrame = previewMotion.Update(MakeScrollRequest(movedOwned, 960, 720), 1060);
    const auto& preview = panel.Draw(movedOwned, 960, 720, &previewFrame);
    WritePreview(output / "menu-opening-scroll-clock-demo.ppm", panel.textures.Raster(preview, 960, 720), 960, 720);
    ready.context = "PREVIEW / OPENING LOCAL CLOCK DEMO";
    ready.notice = "Actual elapsed report 200 ms; explicit local presentation sample 275 ms. No game is connected.";
    ScrollPresentation lead; lead.Update(MakeScrollRequest(ready, 960, 720), 1000);
    const auto leadFrame = lead.Update(MakeScrollRequest(ready, 960, 720), 1075);
    Require(leadFrame.openingElapsed == 275, "Actual owner report did not receive its bounded explicit clock lead");
    const auto& leadData = panel.Draw(ready, 960, 720, &leadFrame);
    WritePreview(output / "menu-opening-local-clock-demo.ppm", panel.textures.Raster(leadData, 960, 720), 960, 720);
    std::cout << "PASS Actual twelve opening profiles, eight and ten-row limits, no-op identity and scroll takeover\n";
}

}
