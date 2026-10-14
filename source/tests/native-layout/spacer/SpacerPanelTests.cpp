#include "SpacerPanelTests.h"
#include "../BackendPreview.h"
#include "../CpuDrawlist.h"
#include "../../../native/render/ApparitionPanel.h"
#include "../../../native/render/ScrollRows.h"
#include "../../../native/runtime/scroll/SpacerScrollReceiptTracker.h"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
class Panel final {
public:
    Panel() {
        previous_ = ImGui::GetCurrentContext();
        context_ = ImGui::CreateContext();
        // Nested actual-packet joins keep their caller context separate from test-owned texture state
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.BackendFlags |= ImGuiBackendFlags_RendererHasTextures | ImGuiBackendFlags_RendererHasVtxOffset;
        ImGui::GetPlatformIO().Renderer_TextureMaxWidth = 4096;
        ImGui::GetPlatformIO().Renderer_TextureMaxHeight = 4096;
    }
    ~Panel() {
        ImGui::DestroyContext(context_);
        ImGui::SetCurrentContext(previous_);
    }
    Panel(const Panel&) = delete;
    Panel& operator=(const Panel&) = delete;
    std::vector<unsigned char> Draw(const MenuSnapshot& snapshot, const ScrollFrame* rows = nullptr) {
        ImGui::SetCurrentContext(context_);
        auto& io = ImGui::GetIO();
        io.DisplaySize = {640, 480};
        io.DeltaTime = 1.0f / 60;
        ImGui::NewFrame();
        DrawApparitionPanel(snapshot, nullptr, std::nullopt, std::nullopt, rows);
        ImGui::Render();
        auto& draw = *ImGui::GetDrawData();
        for (auto* list : draw.CmdLists) for (const auto& vertex : list->VtxBuffer)
            Require(std::isfinite(vertex.pos.x) && std::isfinite(vertex.pos.y), "Spacer paint produced a nonfinite vertex");
        textures_.Update(draw);
        return textures_.Raster(draw, 640, 480);
    }
private:
    ImGuiContext* context_;
    ImGuiContext* previous_;
    CpuTextures textures_;
};
MenuSnapshot Source(bool stealth = false, bool counter = false, bool up = false) {
    MenuSnapshot source;
    source.isOpen = true;
    source.isRootMenu = false;
    source.title = "Main Opening Controls";
    source.menuRevision = 10;
    source.navigationRevision = 20;
    source.presentationFocusRevision = 0;
    source.appearance.stealthUI = stealth;
    if (stealth) source.appearance.booleanDisplay = "Text";
    source.appearance.optionCounter = counter;
    source.rows.resize(16, {"Current logical row", "OFF", true});
    source.rows[2] = {"", "", false, true};
    source.selectedIndex = up ? 3 : 1;
    return source;
}
MenuSnapshot Target(const MenuSnapshot& source, bool up = false, std::uint64_t observed = 1020) {
    runtime::scroll::SpacerScrollReceiptTracker tracker;
    Require(tracker.Capture(source, up ? SpacerScrollDirection::Up : SpacerScrollDirection::Down, 7, 1000),
        "The real private planner rejected a valid source spacer path");
    auto target = source;
    target.selectedIndex = up ? 1 : 3;
    ++target.navigationRevision;
    target.spacerScrollReceipt = tracker.Observe(target, 7, observed);
    Require(target.spacerScrollReceipt.has_value(), "The real private ACK tracker rejected its confirmed target");
    return target;
}
bool SameRegion(const std::vector<unsigned char>& left, const std::vector<unsigned char>& right,
    layout::Rect rect) {
    for (int y = std::max(0, static_cast<int>(rect.y)); y < std::min(480, static_cast<int>(std::ceil(rect.Bottom()))); ++y)
        for (int x = std::max(0, static_cast<int>(rect.x)); x < std::min(640, static_cast<int>(std::ceil(rect.Right()))); ++x)
            for (int channel = 0; channel < 3; ++channel) {
                const auto index = static_cast<std::size_t>((y * 640 + x) * 3 + channel);
                if (left[index] != right[index]) return false;
            }
    return true;
}
std::uint64_t Ack(const std::filesystem::path& file) {
    Require(std::filesystem::file_size(file) <= 32768, "Actual ACK fixture exceeded the private packet bound");
    std::ifstream input(file);
    const auto document = nlohmann::json::parse(input);
    Require(document.at("lastSequence").is_number_unsigned(), "Actual ACK fixture is not an unsigned sequence");
    return document.at("lastSequence").get<std::uint64_t>();
}
}
void AddSpacerPanelChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Private spacer hold paints the old counter while keeping confirmed target selection", [] {
        const auto source = Source(false, true);
        const auto target = Target(source);
        ScrollPresentation owner;
        owner.Update(MakeScrollRequest(source, 640, 480), 990);
        const auto hold = owner.Update(MakeScrollRequest(target, 640, 480), 1040);
        Require(hold.selected == 3 && hold.counterSelection == 1, "Counter paint changed action selection");
        Panel panel;
        const auto held = panel.Draw(target, &hold);
        const auto old = panel.Draw(source);
        const auto actual = panel.Draw(target);
        const auto hud = layout::MakeHud({640, 480, target.rows.size(), target.selectedIndex, 260, 3, false, 0, target.appearance});
        Require(SameRegion(held, old, {hud.body.Right() - 60, hud.counter.y - 8, 60, 16}), "Hold counter pixels differ from the observed source counter");
        Require(!SameRegion(held, actual, {hud.body.Right() - 60, hud.counter.y - 8, 60, 16}), "Counter advanced before the source half wait");
    });
    checks.emplace_back("Final half wait updates counter before the final row fade has completed", [] {
        const auto source = Source(false, true);
        const auto target = Target(source);
        ScrollPresentation owner;
        owner.Update(MakeScrollRequest(source, 640, 480), 990);
        const auto final = owner.Update(MakeScrollRequest(target, 640, 480), 1060);
        Panel panel;
        const auto painted = panel.Draw(target, &final);
        const auto current = panel.Draw(target);
        const auto hud = layout::MakeHud({640, 480, target.rows.size(), target.selectedIndex, 260, 3, false, 0, target.appearance});
        Require(final.transitioning && final.counterSelection == target.selectedIndex, "Final counter waited for row fading");
        Require(SameRegion(painted, current, {hud.body.Right() - 60, hud.counter.y - 8, 60, 16}), "Final counter pixels differ from the current counter oracle");
    });
    checks.emplace_back("Stealth shifted branches retain finite paint and never create a forbidden counter", [] {
        for (bool up : {false, true}) {
            const auto source = Source(true, false, up);
            const auto target = Target(source, up);
            ScrollPresentation owner;
            owner.Update(MakeScrollRequest(source, 640, 480), 990);
            Panel panel;
            for (const auto now : {1020u, 1059u, 1060u, 1090u, 1120u, 1180u}) {
                const auto frame = owner.Update(MakeScrollRequest(target, 640, 480), now);
                const auto hud = layout::MakeHud({640, 480, target.rows.size(), target.selectedIndex, 260, 3, false, 0, target.appearance});
                Require(!hud.counterVisible && MatchesScrollFrame(frame, hud, MakeScrollRequest(target, 640, 480)),
                    "Stealth paint lost bounded receipt validity or created a counter");
                Require(!panel.Draw(target, &frame).empty(), "Stealth source branch produced no bounded CPU paint");
            }
        }
    });
    checks.emplace_back("Tampered receipt phase counter and identity fall back to confirmed current pixels", [] {
        const auto source = Source(false, true);
        const auto target = Target(source);
        ScrollPresentation owner;
        owner.Update(MakeScrollRequest(source, 640, 480), 990);
        const auto hold = owner.Update(MakeScrollRequest(target, 640, 480), 1040);
        for (unsigned fault = 0; fault < 4; ++fault) {
            auto invalid = hold;
            if (fault == 0) invalid.counterSelection = 3;
            if (fault == 1) invalid.spacerSequence = 8;
            if (fault == 2) invalid.spacerElapsed = 180;
            if (fault == 3) invalid.spacerElapsed.reset();
            Panel panel;
            Require(panel.Draw(target, &invalid) == panel.Draw(target), "Invalid counter evidence overrode confirmed pixels");
        }
    });
    checks.emplace_back("Captured appearance accent root and opening policy mismatch cannot offer a render receipt", [] {
        const auto source = Source();
        const auto valid = Target(source);
        for (unsigned fault = 0; fault < 5; ++fault) {
            auto target = valid;
            if (fault == 0) target.spacerScrollReceipt->sourceAppearance.largeCursor = true;
            if (fault == 1) target.spacerScrollReceipt->sourceAccent = "#FF0000";
            if (fault == 2) target.spacerScrollReceipt->sourceRootMenu = true;
            if (fault == 3) target.spacerScrollReceipt->sourceRainbow = true;
            if (fault == 4) target.spacerScrollReceipt->sourceOpeningControls = MainOpeningControls{1, {OpeningAction::Aim}};
            Require(!MakeScrollRequest(target, 640, 480).spacerReceipt, "Foreign captured paint offered a private event");
        }
    });
}
void CheckActualSpacerProfiles(const std::filesystem::path& fixtures, const std::filesystem::path& output) {
    // Actual managed pairs join the real private planner, ACK tracker, presentation owner and CPU drawlist
    for (const std::string style : {"ordinary", "counter", "stealth"}) for (const std::string direction : {"down", "up"}) {
        const auto stem = "spacer-scroll-" + style + "-" + direction;
        auto source = ReadBackendPreview(fixtures / (stem + "-before.json"));
        auto target = ReadBackendPreview(fixtures / (stem + "-after.json"));
        source.presentationFocusRevision = target.presentationFocusRevision = 0;
        runtime::scroll::SpacerScrollReceiptTracker tracker;
        Require(tracker.Capture(source, direction == "up" ? SpacerScrollDirection::Up : SpacerScrollDirection::Down,
            Ack(fixtures / (stem + "-after.json")), 1000), "Actual pair cannot capture its declared source topology");
        target.spacerScrollReceipt = tracker.Observe(target, Ack(fixtures / (stem + "-after.json")), 1020);
        Require(target.spacerScrollReceipt.has_value(), "Actual pair cannot confirm the exact accepted target");
        const auto expected = style != "stealth" ? SpacerScrollBranch::StableWindow
            : direction == "down" ? SpacerScrollBranch::FinalWindowShift : SpacerScrollBranch::InitialWindowShiftThenRebuild;
        Require(target.spacerScrollReceipt->plan.branch == expected, "Actual pair selected the wrong source branch");
        ScrollPresentation owner;
        owner.Update(MakeScrollRequest(source, 640, 480), 990);
        Panel panel;
        for (const auto now : {1020u, 1060u, 1090u, 1120u, 1180u}) {
            const auto frame = owner.Update(MakeScrollRequest(target, 640, 480), now);
            Require(frame.selected == target.selectedIndex, "Actual CPU pair changed action selection");
            const auto pixels = panel.Draw(target, &frame);
            WritePreview(output / (stem + "-clock-" + std::to_string(now - 1000) + ".ppm"), pixels, 640, 480);
        }
    }
}
} // namespace iw::overlay::tests
