#include "DesignPanelTests.h"
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
    model.rows.resize(8, {"Current preference", "OFF", true}); return model;
}
constexpr std::array<MenuDesign, 5> Designs{MenuDesign::Apparition, MenuDesign::Classic, MenuDesign::Native, MenuDesign::Aio, MenuDesign::Basic};
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
void AddDesignPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Design transitions retain pause help and hide only rebuilding menu chrome", [] {
        CpuPanel panel; auto model = Model(); const auto saved = model.instructions;
        model.menuDesignTransition = MenuDesignTransitionPhase::Rebuild;
        auto help = MakeInstructionContent(model);
        Require(help.Line(0) == "Changing menu design" && help.Line(1) == "Menu controls paused",
            "A design transition advertised live navigation");
        const auto& hidden = panel.Draw(model);
        Require(!HasColorIn(hidden, IM_COL32(55, 55, 55, 255), {0, 65, 260, 200})
            && HasColorIn(hidden, IM_COL32_WHITE, {255, 425, 385, 48}), "Rebuild hid the footer or retained menu chrome");
        model.menuDesignTransition = MenuDesignTransitionPhase::Cooldown;
        Require(HasColorIn(panel.Draw(model), IM_COL32(55, 55, 55, 255), {0, 65, 260, 200}), "Cooldown kept the menu hidden");
        Require(model.instructions == saved && !MakeScrollRequest(model, 640, 480).active, "A design transition mutated help or enabled motion");
    });
    checks.emplace_back("Five design painters use their distinct source fills and scroller alpha", [] {
        CpuPanel panel;
        const auto& classic = panel.Draw(Model(MenuDesign::Classic));
        Require(HasColorIn(classic, IM_COL32(25, 25, 25, 217), {0, 85, 260, 160}), "Classic reused the Apparition body alpha");
        const auto& native = panel.Draw(Model(MenuDesign::Native));
        Require(HasColorIn(native, IM_COL32(57, 152, 254, 230), {0, 30, 260, 38})
            && HasColorIn(native, IM_COL32(57, 152, 254, 179), {0, 85, 260, 18}), "Native banner or scroller opacity differs");
        const auto& aio = panel.Draw(Model(MenuDesign::Aio));
        Require(HasColorIn(aio, IM_COL32(0, 0, 0, 77), {0, 58, 265, 210})
            && HasColorIn(aio, IM_COL32(0, 0, 0, 115), {0, 85, 260, 160}), "AIO lost its outer frame or black body");
        const auto& basic = panel.Draw(Model(MenuDesign::Basic));
        Require(HasColorIn(basic, IM_COL32(0, 0, 0, 255), {0, 85, 260, 20})
            && HasColorIn(basic, IM_COL32(0, 0, 0, 115), {0, 85, 260, 180}), "Basic reused another design chrome");
    });
    checks.emplace_back("Classic and Native keep ordinary selected text white even for malformed direct appearance", [] {
        CpuPanel panel;
        for (const auto design : {MenuDesign::Classic, MenuDesign::Native}) {
            auto model = Model(design); model.appearance.coloredCursor = true;
            const auto& data = panel.Draw(model); const float offset = design == MenuDesign::Classic ? 11 : 8;
            Require(HasColorIn(data, IM_COL32_WHITE, {4, 85 + offset - 8, 190, 18})
                && !HasColorIn(data, IM_COL32(57, 152, 254, 255), {4, 85 + offset - 8, 190, 18}),
                "A design without Colored Cursor recolored selected option text");
        }
    });
    checks.emplace_back("Native brand and honest AIO status disappear only for width editing", [] {
        CpuPanel panel;
        for (const auto design : {MenuDesign::Native, MenuDesign::Aio}) for (unsigned modal = 0; modal < 5; ++modal) {
            auto model = Model(design); if (modal) model.rows.clear();
            if (modal == 1) model.widthEditor = WidthEditor{};
            if (modal == 2) model.menuPositionEditor = MenuPositionEditor{};
            if (modal == 3) model.instructionPositionEditor = InstructionPositionEditor{};
            if (modal == 4) model.menuCredits = MenuCredits{500};
            const auto hud = layout::MakeHud({640, 480, model.rows.size(), 0, 260, 3, false, 0, model.appearance,
                modal == 1, std::nullopt, modal == 3, false, modal == 2, modal == 4});
            const auto& data = panel.Draw(model);
            Require(HasColorIn(data, IM_COL32_WHITE, hud.secondaryClip) == (modal != 1),
                "A modal lost its retained secondary text or width editing retained it");
        }
    });
    checks.emplace_back("Design footers retain white help with source gray or black backgrounds", [] {
        CpuPanel panel;
        for (const auto design : Designs) {
            auto model = Model(design); const auto& data = panel.Draw(model);
            const auto background = design == MenuDesign::Apparition ? IM_COL32(42, 42, 42, 255)
                : design == MenuDesign::Classic ? IM_COL32(25, 25, 25, 217) : IM_COL32(0, 0, 0, 255);
            Require(HasColorIn(data, background, {255, 430, 380, 44})
                && HasColorIn(data, IM_COL32_WHITE, {255, 430, 380, 44}), "Design footer lost its declared fill or white text");
            model.appearance.instructionsBackground = false;
            const auto& plain = panel.Draw(model);
            Require(HasColorIn(plain, IM_COL32_WHITE, {255, 430, 380, 44})
                && !HasColorIn(plain, background, {255, 430, 380, 44}), "Disabling design footer backgrounds removed text or retained fill");
        }
    });
    checks.emplace_back("Current rainbow sample colors Basic scroller and AIO footer without borrowing privileges", [] {
        CpuPanel panel; constexpr layout::Color sample{242, 121, 0, 255};
        auto basic = Model(MenuDesign::Basic); basic.smoothRainbowTheme = true;
        const auto& basicData = panel.Draw(basic, 640, 480, nullptr, sample);
        Require(HasColorIn(basicData, IM_COL32(242, 121, 0, 255), {0, 107, 260, 18}), "Basic rainbow scroller did not use the current cycle");
        auto aio = Model(MenuDesign::Aio); aio.smoothRainbowTheme = true;
        const auto& aioData = panel.Draw(aio, 640, 480, nullptr, sample);
        Require(HasColorIn(aioData, IM_COL32(242, 121, 0, 255), {255, 430, 380, 44})
            && HasColorIn(aioData, IM_COL32(57, 152, 254, 255), {254, 429, 382, 46}),
            "AIO rainbow fill or retained static outline differs");
    });
    checks.emplace_back("Exact design identity resets motion even when body dimensions match", [] {
        auto model = Model(); model.appearance.coloredCursor = false; model.selectedIndex = 1; ScrollPresentation motion;
        motion.Update(MakeScrollRequest(model, 640, 480), 0); model.selectedIndex = 2;
        const auto old = motion.Update(MakeScrollRequest(model, 640, 480), 10); Require(old.transitioning, "Setup did not create motion");
        model.appearance.design = MenuDesign::Native;
        const auto next = motion.Update(MakeScrollRequest(model, 640, 480), 30);
        Require(!next.transitioning && next.design == MenuDesign::Native, "An equal-size new design inherited old cursor motion");
        CpuPanel panel; const auto expected = panel.textures.Raster(panel.Draw(model), 640, 480);
        const auto actual = panel.textures.Raster(panel.Draw(model, 640, 480, &old), 640, 480);
        Require(actual == expected, "A caller's stale design frame changed the new design");
    });
    checks.emplace_back("All design modals and saved origins keep bounded finite clipping across viewports", [] {
        for (const auto design : Designs) for (const auto size : {ImVec2{320, 240}, ImVec2{960, 720}, ImVec2{480, 960}})
            for (unsigned modal = 0; modal < 4; ++modal) {
                CpuPanel panel; auto model = Model(design); model.appearance.menuPosition = MenuPosition{-20, 126};
                model.appearance.adaptiveInstructions = true; model.notice.assign(1500, 'N');
                if (modal == 1) { model.rows.clear(); model.widthEditor = WidthEditor{}; }
                if (modal == 2) { model.rows.clear(); model.menuPositionEditor = MenuPositionEditor{}; }
                if (modal == 3) { model.rows.clear(); model.menuCredits = MenuCredits{6000}; }
                FiniteClips(panel.Draw(model, size.x, size.y), size.x, size.y);
            }
    });
    checks.emplace_back("Long UTF8 headers keep readable source size and ellipsis beside narrow counters", [] {
        CpuPanel panel;
        for (const auto design : {MenuDesign::Classic, MenuDesign::Native}) for (const int width : {200, 260}) {
            auto model = Model(design); model.appearance.menuWidth = width; model.appearance.optionCounter = true;
            model.title.clear();
            for (unsigned character = 0; character < 100; ++character) model.title += "Wé";
            panel.Draw(model);
            const auto hud = layout::MakeHud({640, 480, model.rows.size(), 0, static_cast<float>(width), 3, false, 0, model.appearance});
            const auto fitted = FitDesignTitle(model, hud);
            Require(fitted.font == hud.titleFont && fitted.text.ends_with("..."), "A long header shrank below readable source size");
            Require(fitted.text.size() < model.title.size() && fitted.width <= fitted.available,
                "A fitted header overlapped its counter or lost bounded ellipsis");
            const auto* next = fitted.text.data(); const auto* end = next + fitted.text.size();
            while (next < end) {
                unsigned codepoint = 0; const int bytes = ImTextCharFromUtf8(&codepoint, next, end);
                Require(bytes > 0 && codepoint != 0xfffd, "Header fitting split a UTF-8 character"); next += bytes;
            }
            const auto longPixels = panel.textures.Raster(panel.Draw(model), 640, 480);
            model.title = "Short";
            const auto shortPixels = panel.textures.Raster(panel.Draw(model), 640, 480);
            // The title must not alter any counter ink, even after UTF-8 fitting in a narrow header
            for (int y = static_cast<int>(hud.titleClip.y); y < hud.titleClip.Bottom(); ++y)
                for (int x = width - 24; x < width; ++x) for (unsigned channel = 0; channel < 3; ++channel) {
                    const auto pixel = static_cast<std::size_t>((y * 640 + x) * 3 + channel);
                    Require(longPixels[pixel] == shortPixels[pixel], "A long title contaminated counter glyphs");
                }
            FiniteClips(panel.Draw(model), 640, 480);
        }
    });
    checks.emplace_back("Repeated design cycles stabilize warmed font bakes and texture dimensions", [] {
        CpuPanel panel; std::array<int, 4> warm{};
        for (unsigned frame = 0; frame < 600; ++frame) {
            auto model = Model(Designs[frame % Designs.size()]); model.appearance.largeCursor = (frame / 5) % 2;
            panel.Draw(model); const auto current = panel.Atlas();
            if (frame == 100) warm = current;
            if (frame > 100) Require(current == warm, "Design cycles grew or replaced a warmed font atlas");
        }
    });
}
void VerifyDesignBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path&) {
    const auto read = [&](const std::string& name) { return ReadBackendPreview(fixtures / (name + ".json")); };
    CpuPanel panel;
    const std::array<std::pair<const char*, MenuDesign>, 4> targets{{
        {"classic", MenuDesign::Classic}, {"native", MenuDesign::Native}, {"aio", MenuDesign::Aio}, {"basic", MenuDesign::Basic}}};
    for (const auto& [name, design] : targets) {
        const std::string prefix = std::string("preferences-design-") + name;
        const auto rebuild = read(prefix + "-rebuild"), before = read(prefix + "-pre-rebuild");
        const auto cooldown = read(prefix + "-cooldown"), last = read(prefix + "-pre-unlock"), unlocked = read(prefix + "-unlocked");
        Require(rebuild.menuDesignTransition == MenuDesignTransitionPhase::Rebuild
            && before.menuDesignTransition == MenuDesignTransitionPhase::Rebuild
            && rebuild.appearance.design == MenuDesign::Apparition && before.appearance == rebuild.appearance,
            "The actual rebuilding design lost its old footer appearance");
        Require(cooldown.menuDesignTransition == MenuDesignTransitionPhase::Cooldown
            && last.menuDesignTransition == MenuDesignTransitionPhase::Cooldown && !unlocked.menuDesignTransition
            && cooldown.appearance.design == design && last.appearance == cooldown.appearance
            && unlocked.appearance == cooldown.appearance, "The actual new design did not settle at the committed appearance");
        Require(rebuild.menuRevision && cooldown.menuRevision && *cooldown.menuRevision == *rebuild.menuRevision + 1
            && last.menuRevision == cooldown.menuRevision && unlocked.menuRevision == cooldown.menuRevision
            && rebuild.navigationRevision == cooldown.navigationRevision && cooldown.navigationRevision == unlocked.navigationRevision,
            "Design publication invalidated context more than once or changed cursor identity");
        const bool noColor = design == MenuDesign::Classic || design == MenuDesign::Native;
        const bool noCounter = design == MenuDesign::Aio || design == MenuDesign::Basic;
        Require(cooldown.appearance.coloredCursor == !noColor && cooldown.appearance.optionCounter == !noCounter
            && cooldown.rows.size() == 12 && cooldown.rows.front().label == "Design" && cooldown.selectedIndex == 0,
            "Actual design capabilities or selected preference differ from the source contract");
        Require(!MakeScrollRequest(cooldown, 640, 480).active && MakeInstructionContent(cooldown).Line(1) == "Menu controls paused",
            "Actual cooldown advertised active menu navigation");
        const auto& hidden = panel.Draw(rebuild);
        Require(!HasColorIn(hidden, IM_COL32(55, 55, 55, 255), {0, 65, 260, 220}), "Actual rebuilding menu retained its chrome");
        FiniteClips(panel.Draw(cooldown), 640, 480); FiniteClips(panel.Draw(unlocked), 640, 480);
    }
    const auto same = read("preferences-design-same-cooldown"), sameLast = read("preferences-design-same-pre-unlock"), sameDone = read("preferences-design-same-unlocked");
    Require(same.menuDesignTransition == MenuDesignTransitionPhase::Cooldown && sameLast.menuDesignTransition == same.menuDesignTransition
        && !sameDone.menuDesignTransition && same.menuRevision == sameLast.menuRevision && same.menuRevision == sameDone.menuRevision,
        "Selecting the current design rebuilt instead of keeping its visible cooldown");
    Require(HasColorIn(panel.Draw(same), IM_COL32(55, 55, 55, 255), {0, 65, 260, 220}), "Actual same-design cooldown hid its menu");
    const auto draft = read("preferences-design-draft");
    Require(draft.appearance.design == MenuDesign::Apparition && !draft.menuDesignTransition
        && draft.rows.front().value == "Classic" && draft.menuRevision == same.menuRevision,
        "The actual design draft changed its applied painter");
    const auto native = read("main-design-native"), width = read("width-editor-design-classic");
    Require(native.appearance.design == MenuDesign::Native && native.title == "Main Menu" && native.isRootMenu,
        "Native root lost its source title or actual identity");
    Require(width.appearance.design == MenuDesign::Classic && width.widthEditor && width.rows.empty()
        && width.appearance.optionCounter && !width.appearance.coloredCursor, "Classic width editor lost source capabilities");
    FiniteClips(panel.Draw(native), 640, 480); FiniteClips(panel.Draw(width), 640, 480);
    std::cout << "PASS Actual 26 design profiles, transition epochs, constraints and modal painter joins\n";
}

}
