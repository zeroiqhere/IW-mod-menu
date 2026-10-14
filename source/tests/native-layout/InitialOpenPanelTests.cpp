#include "InitialOpenPanelTests.h"
#include "CpuDrawlist.h"
#include "BackendPreview.h"
#include "../../native/render/ApparitionPanel.h"
#include "../../native/render/InstructionPanel.h"
#include "../../native/render/ScrollRows.h"
#include <imgui.h>
#include <algorithm>
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
    CpuTextures textures;
private:
    ImGuiContext* context_;
};
MenuSnapshot Model(MenuDesign design = MenuDesign::Apparition) {
    MenuSnapshot model; model.isOpen = true; model.isRootMenu = false; model.title = "Menu Preferences";
    model.context = "CPU TEST / DISCONNECTED"; model.notice = "No game is connected";
    model.appearance.design = design; model.menuRevision = 3; model.presentationFocusRevision = 0;
    model.rows.resize(8, {"Current preference", "OFF", true});
    model.appearance.coloredCursor = false; model.menuOpenTransition = MenuOpenTransitionPhase::Rebuild; return model;
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
void AddInitialOpenPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Initial rebuild removes menu chrome while retaining independent footer and status", [] {
        CpuPanel panel; auto model = Model(); const auto& data = panel.Draw(model);
        Require(!HasColorIn(data, IM_COL32(55,55,55,255), {0,65,260,19}), "Logical opening drew the absent menu banner");
        Require(HasColorIn(data, IM_COL32_WHITE, {250,420,280,60}), "Initial rebuild removed independent instruction text");
        Require(HasColorIn(data, IM_COL32_WHITE, {280,0,360,400}), "Initial rebuild removed IW status text");
    });
    checks.emplace_back("Both initial phases show paused help without mutating saved controls", [] {
        auto model = Model(); const auto saved = model.instructions;
        for (auto phase : {MenuOpenTransitionPhase::Rebuild, MenuOpenTransitionPhase::Cooldown}) {
            model.menuOpenTransition = phase; const auto help = MakeInstructionContent(model);
            Require(help.LineCount()==3 && help.Line(0)=="Opening menu" && help.Line(1)=="Menu controls paused"
                && help.Line(2)=="Please wait" && model.instructions==saved, "Opening advertised unavailable normal controls");
        }
    });
    checks.emplace_back("Initial cooldown retains panel and bounded opening frames while scrolling remains locked", [] {
        CpuPanel panel; auto model = Model(); model.menuOpenTransition = MenuOpenTransitionPhase::Cooldown;
        // Input locking does not stop the independently reported row fade
        model.menuOpening = MenuOpening{1,0}; ScrollPresentation motion;
        auto request = MakeScrollRequest(model,640,480); Require(!request.active, "Initial cooldown admitted cursor animation");
        motion.Update(request,0); const auto frame = motion.Update(request,50); const auto& data = panel.Draw(model,640,480,&frame);
        Require(HasColorIn(data,IM_COL32(55,55,55,255),{0,65,260,19}), "Initial cooldown hid the visible banner");
        Require(HasColorIn(data,IM_COL32(255,255,255,25),{4,121,190,18}), "Initial cooldown discarded the confirmed opening clock frame");
    });
    checks.emplace_back("First ready cursor movement captures locked opening alpha without a ready heartbeat", [] {
        CpuPanel panel; auto model=Model(); model.menuOpenTransition=MenuOpenTransitionPhase::Cooldown;
        model.menuOpening=MenuOpening{1,0}; model.selectedIndex=1; ScrollPresentation motion;
        // A single observed ready packet can already contain accepted cursor movement
        motion.Update(MakeScrollRequest(model,640,480),0); model.menuOpenTransition.reset(); model.menuOpening.reset(); model.selectedIndex=2;
        motion.Update(MakeScrollRequest(model,640,480),40); const auto half=motion.Update(MakeScrollRequest(model,640,480),100);
        Require(half.transitioning && HasColorIn(panel.Draw(model,640,480,&half),IM_COL32(255,255,255,137),{4,121,190,18}),
            "Initial ready transition lost the current opening opacity");
    });
    checks.emplace_back("Initial cooldown without an opening event rejects prior moving caller rows", [] {
        CpuPanel panel; auto model=Model(); model.menuOpenTransition.reset(); ScrollPresentation motion;
        motion.Update(MakeScrollRequest(model,640,480),0); model.selectedIndex=1;
        motion.Update(MakeScrollRequest(model,640,480),100); const auto old=motion.Update(MakeScrollRequest(model,640,480),160);
        model.menuOpenTransition=MenuOpenTransitionPhase::Cooldown;
        const auto expected=panel.textures.Raster(panel.Draw(model),640,480);
        Require(panel.textures.Raster(panel.Draw(model,640,480,&old),640,480)==expected, "Initial cooldown reused an older cursor path");
    });
    checks.emplace_back("Initial opening rejects stale private identity and retains owner-only legacy timing", [] {
        CpuPanel panel; auto model=Model(); model.menuOpenTransition=MenuOpenTransitionPhase::Cooldown;
        model.menuOpening=MenuOpening{1,60}; model.presentationFocusRevision.reset();
        const auto expected=panel.textures.Raster(panel.Draw(model),640,480); ScrollPresentation legacy;
        legacy.Update(MakeScrollRequest(model,640,480),0); const auto frame=legacy.Update(MakeScrollRequest(model,640,480),5000);
        Require(frame.openingElapsed==60 && panel.textures.Raster(panel.Draw(model,640,480,&frame),640,480)==expected,
            "Initial cooldown invented a private clock without identity evidence");
        model.presentationFocusRevision=0;
        for (unsigned fault=0; fault<4; ++fault) {
            auto stale=StaticScrollFrame(MakeScrollRequest(model,640,480)); stale.rows[2].opacity=1;
            if (fault==0) stale.menuRevision=99;
            if (fault==1) stale.focusRevision=99;
            if (fault==2) stale.layout.viewportWidth++;
            if (fault==3) stale.opening->revision++;
            Require(panel.textures.Raster(panel.Draw(model,640,480,&stale),640,480)==expected,
                "An initial cooldown accepted stale presentation identity or geometry");
        }
    });
    checks.emplace_back("Empty single and populated root or retained child menus share initial visibility rules", [] {
        CpuPanel panel;
        for (bool root : {false,true}) for (unsigned count : {0u,1u,8u}) {
            auto model=Model(); model.isRootMenu=root; model.rows.resize(count);
            // These are ordinary logical menus, not invented modal or fade descriptors
            Require(!HasColorIn(panel.Draw(model),IM_COL32(55,55,55,255),{0,65,260,19}),
                "An empty single or child rebuild drew its banner");
            Require(!MakeScrollRequest(model,640,480).active, "A rebuilding ordinary menu admitted scrolling");
            model.menuOpenTransition=MenuOpenTransitionPhase::Cooldown;
            Require(HasColorIn(panel.Draw(model),IM_COL32(55,55,55,255),{0,65,260,19}), "An ordinary menu lost its cooldown banner");
        }
    });
    checks.emplace_back("Initial footer preserves visible text across designs alternate plain hidden and extreme anchors", [] {
        CpuPanel panel;
        for (auto design : {MenuDesign::Apparition,MenuDesign::Classic,MenuDesign::Native,MenuDesign::Aio,MenuDesign::Basic})
            for (bool visible : {false,true}) for (bool background : {false,true}) for (bool alternate : {false,true}) {
                auto model=Model(design); model.appearance.instructionsVisible=visible;
                model.appearance.instructionsBackground=background; model.appearance.alternateInstructions=alternate;
                model.appearance.adaptiveInstructions=!alternate;
                // Footer style changes remove only decoration or join the same paused actions
                const auto help=MakeInstructionContent(model);
                Require(help.LineCount()==(alternate?1u:3u) && help.Line(0).find("Opening menu")!=std::string_view::npos,
                    "An initial footer style lost its paused context");
                const auto& normal=panel.Draw(model);
                Require(HasColorIn(normal,IM_COL32_WHITE,{0,420,540,60})==visible,
                    "Plain or alternate initial help lost text or hidden help remained visible");
                model.appearance.menuPosition=MenuPosition{std::numeric_limits<int>::max(),std::numeric_limits<int>::min()};
                model.appearance.instructionPosition=InstructionPosition{std::numeric_limits<int>::min(),std::numeric_limits<int>::max()};
                FiniteClips(panel.Draw(model,960,720),960,720);
            }
    });
}
void VerifyInitialOpenBackendPreviews(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    const auto read=[&](const char* name) { return ReadBackendPreview(fixtures/(std::string("menu-initial-open-")+name+".json")); };
    auto rebuild=read("rebuild"), before=read("pre-draw"), drawn=read("drawn"), locked=read("pre-unlock"), ready=read("input-ready");
    const auto last=read("last"), done=read("complete");
    // Controls resume at 600 ms while the last visible ordinal continues its own fade
    Require(rebuild.menuOpenTransition==MenuOpenTransitionPhase::Rebuild && before.menuOpenTransition==MenuOpenTransitionPhase::Rebuild
        && !rebuild.menuOpening && !before.menuOpening && drawn.menuOpenTransition==MenuOpenTransitionPhase::Cooldown
        && drawn.menuOpening && drawn.menuOpening->elapsedMilliseconds==0
        && locked.menuOpenTransition==MenuOpenTransitionPhase::Cooldown && locked.menuOpening && locked.menuOpening->elapsedMilliseconds==549
        && !ready.menuOpenTransition && ready.menuOpening && ready.menuOpening->elapsedMilliseconds==550
        && last.menuOpening && last.menuOpening->elapsedMilliseconds==699 && !done.menuOpening,
        "Actual initial opening lost healthy 49/50/599/600 and fade completion boundaries");
    Require(rebuild.menuRevision==drawn.menuRevision && drawn.menuRevision==ready.menuRevision
        && ready.menuRevision==done.menuRevision && rebuild.isRootMenu && rebuild.rows.size()==8,
        "An initial redraw created a second structural epoch or changed the retained root rows");
    // Late redraws start a fresh sequential wait rather than borrowing time spent without a HUD
    const auto late=read("late"), lateLocked=read("late-pre-unlock"), lateReady=read("late-ready");
    Require(late.menuOpenTransition==MenuOpenTransitionPhase::Cooldown && late.menuOpening && late.menuOpening->elapsedMilliseconds==0
        && lateLocked.menuOpenTransition==MenuOpenTransitionPhase::Cooldown && lateLocked.menuOpening && lateLocked.menuOpening->elapsedMilliseconds==549
        && !lateReady.menuOpenTransition && lateReady.menuOpening && lateReady.menuOpening->elapsedMilliseconds==550
        && late.menuRevision==lateReady.menuRevision,
        "A late first redraw skipped its sequential 550 ms wait");
    const auto retained=read("retained-rebuild"), retainedDrawn=read("retained-drawn");
    const auto empty=read("empty-rebuild"), emptyDrawn=read("empty-drawn"), single=read("single-drawn");
    Require(!retained.isRootMenu && retained.rows.size()==retainedDrawn.rows.size()
        && std::equal(retained.rows.begin(),retained.rows.end(),retainedDrawn.rows.begin(),[](const auto& a,const auto& b) {
            return a.label==b.label && a.value==b.value && a.enabled==b.enabled; }) && retained.menuRevision==retainedDrawn.menuRevision
        && retainedDrawn.menuOpening && empty.rows.empty() && emptyDrawn.rows.empty()
        && !empty.menuOpening && !emptyDrawn.menuOpening && single.rows.size()==1 && !single.menuOpening,
        "Retained child empty or single-row initial menus were fabricated as modal fades");
    // Structural roster changes end the old fade without ending the owner input wait
    const auto dynamic=read("dynamic-drawn"), changed=read("dynamic-changed"), hidden=read("focus-hidden");
    Require(dynamic.rows.size()==3 && dynamic.menuOpening && changed.rows.size()==1 && !changed.menuOpening
        && dynamic.menuRevision!=changed.menuRevision && !hidden.isOpen && !hidden.menuOpenTransition && !hidden.menuOpening,
        "A changed roster or hidden producer projection retained a stale opening descriptor");
    CpuPanel panel;
    const auto& absent=panel.Draw(rebuild,960,720);
    Require(!HasColorIn(absent,IM_COL32(55,55,55,255),{0,97,390,29}), "Actual initial rebuild emitted menu chrome");
    // The same owned packet may have private focus evidence without exposing it in JSON
    drawn.presentationFocusRevision=0; ScrollPresentation motion;
    motion.Update(MakeScrollRequest(drawn,960,720),1000);
    const auto frame=motion.Update(MakeScrollRequest(drawn,960,720),1075);
    Require(frame.openingElapsed==75 && !MakeScrollRequest(drawn,960,720).active,
        "The actual locked cooldown discarded its bounded private presentation clock");
    drawn.context="PREVIEW / INITIAL OPEN CLOCK DEMO";
    drawn.notice="Actual initial-open packet; explicit private focus epoch and 75 ms local presentation lead. No game is connected.";
    const auto& data=panel.Draw(drawn,960,720,&frame);
    WritePreview(output/"menu-initial-open-local-clock-demo.ppm",panel.textures.Raster(data,960,720),960,720);
    // This labelled appearance variant isolates missing-HUD adaptive placement from ordinary saved placement
    auto adaptive=rebuild; adaptive.appearance.adaptiveInstructions=true;
    adaptive.appearance.menuPosition=MenuPosition{17,200}; adaptive.appearance.instructionPosition=InstructionPosition{300,460};
    adaptive.context="PREVIEW / ADAPTIVE ABSENT-HUD DEMO";
    adaptive.notice="Actual producer rows with explicit CPU appearance variant. Missing HUD keeps menu X; help falls back to saved Y minus its height.";
    const auto& adaptiveData=panel.Draw(adaptive,960,720);
    WritePreview(output/"menu-initial-open-adaptive-missing-hud-demo.ppm",panel.textures.Raster(adaptiveData,960,720),960,720);
    std::cout << "PASS Actual eighteen initial opening profiles, sequential timing, retained menus and private clock\n";
}

}
