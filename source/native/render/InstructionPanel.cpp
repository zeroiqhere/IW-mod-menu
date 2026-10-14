#include "InstructionPanel.h"
#include "instructions/InstructionHudPresence.h"
#include "PanelText.h"
#include <algorithm>
#include <limits>
#include <imgui.h>

namespace iw::overlay {
namespace {
constexpr std::array<std::string_view, 3> InitialOpenHelp{
    "Opening menu", "Menu controls paused", "Please wait"};
constexpr std::array<std::string_view, 3> UnlockHelp{
    "Returning to menu", "Menu controls paused", "Please wait"};
constexpr std::array<std::string_view, 3> PreferenceRefreshHelp{
    "Updating menu", "Menu controls paused", "Please wait"};
constexpr std::array<std::string_view, 3> CallbackHelp{
    "Updating option", "Menu controls paused", "Please wait"};
constexpr std::array<std::string_view, 3> DesignTransitionHelp{
    "Changing menu design", "Menu controls paused", "Please wait"};
ImVec2 Top(const layout::Rect& rect) { return {rect.x, rect.y}; }
ImVec2 Bottom(const layout::Rect& rect) { return {rect.Right(), rect.Bottom()}; }
layout::Rect VisiblePart(const layout::Rect& rect) {
    const auto viewport = ImGui::GetIO().DisplaySize;
    const float x = std::clamp(rect.x, 0.0f, viewport.x), y = std::clamp(rect.y, 0.0f, viewport.y);
    return {x, y, std::max(0.0f, std::clamp(rect.Right(), 0.0f, viewport.x) - x),
        std::max(0.0f, std::clamp(rect.Bottom(), 0.0f, viewport.y) - y)};
}
void Decoration(ImDrawList& draw, const layout::Rect& rect, ImU32 color) {
    // Clip only emitted pixels, keeping even offscreen saved anchors unchanged
    const auto visible = VisiblePart(rect);
    if (visible.width > 0 && visible.height > 0) { draw.AddRectFilled(Top(visible), Bottom(visible), color); }
}
}

float FitInstructionText(const InstructionPanelContent& content, const layout::Hud& hud) {
    const float step = hud.instructionText.height / std::max<std::size_t>(content.LineCount(), 1);
    // ImGui rounds baked sizes to whole pixels, so a positive subpixel size can become zero
    float font = std::max(1.0f, std::min(hud.instructionFont, step));
    // Font sizes can choose different baked glyphs, so confirm the final size after each fit
    const float space = std::max(hud.scale, hud.instructionText.width - 4 * hud.scale);
    for (unsigned attempt = 0; attempt < 8; ++attempt) {
        float longest = 0;
        for (std::size_t index = 0; index < content.LineCount(); ++index) {
            const auto line = content.Line(index);
            const auto measured = ImGui::GetFont()->CalcTextSizeA(font, std::numeric_limits<float>::max(),
                0, line.data(), line.data() + line.size());
            longest = std::max(longest, measured.x);
        }
        if (longest <= space) { break; }
        const float fitted = std::max(1.0f, font * space / longest);
        if (fitted >= font) break;
        font = fitted;
    }
    return font;
}

std::string_view InstructionPanelContent::Line(std::size_t index) const noexcept {
    if (index >= LineCount()) { return {}; }
    // Form the joined view after moves so it never points at a former stack buffer
    return alternate ? std::string_view(joined.data(), joinedLength) : lines[index];
}

InstructionPanelContent MakeInstructionContent(const MenuSnapshot& snapshot) noexcept {
    InstructionPanelContent result;
    // Explicit ownership can suppress a footer independently of menu visibility
    if (snapshot.instructionHud && !HasOwnedInstructionHud(snapshot)) return result;
    result.alternate = snapshot.instructionHud
        ? *snapshot.instructionHud->creationAlternate : snapshot.appearance.alternateInstructions;
    auto append = [&](std::string_view line) {
        line = BoundedPanelText(line, 256);
        if (!line.empty() && result.count < result.lines.size()) { result.lines[result.count++] = line; }
    };
    if (!snapshot.isOpen && snapshot.instructionHud) {
        for (const auto& hint : snapshot.instructionHud->instructions) { append(hint); }
    } else if (snapshot.menuOptionCallback) {
        // Callback cadence pauses input without claiming that backend work has completed
        for (auto line : CallbackHelp) { append(line); }
    } else if (snapshot.menuOpenTransition) {
        // Saved normal controls return only after the initial owner wait ends
        for (auto line : InitialOpenHelp) { append(line); }
    } else if (snapshot.menuUnlock) {
        for (auto line : UnlockHelp) { append(line); }
    } else if (snapshot.menuDesignTransition) {
        // Both design phases consume normal menu actions while keeping the independent footer
        for (auto line : DesignTransitionHelp) { append(line); }
    } else if (snapshot.menuPreferenceRebuild) {
        // Layout changes keep paused help visible while ordinary rows are hidden or settling
        for (auto line : PreferenceRefreshHelp) { append(line); }
    } else if (snapshot.menuPreferenceRefresh) {
        // Deferred preference rows stay visible while the owner pauses normal menu actions
        for (auto line : PreferenceRefreshHelp) { append(line); }
    } else {
        const auto& footer = snapshot.menuCredits ? snapshot.menuCredits->instructions
            : snapshot.menuPositionEditor ? snapshot.menuPositionEditor->footerInstructions
            : snapshot.instructionPositionEditor ? snapshot.instructionPositionEditor->instructions
            : snapshot.widthEditor ? snapshot.widthEditor->footerInstructions
            : snapshot.instructionHud && !snapshot.instructionHud->instructions.empty()
                ? snapshot.instructionHud->instructions : snapshot.instructions;
        const auto limit = std::min<std::size_t>(footer.size(), snapshot.menuCredits ? 1 : snapshot.menuPositionEditor ? 3 : snapshot.instructionPositionEditor ? 5
            : snapshot.widthEditor ? 3 : result.lines.size());
        for (std::size_t index = 0; index < limit; ++index) { append(footer[index]); }
    }
    if (result.alternate) {
        for (std::size_t index = 0; index < result.count; ++index) {
            if (index) {
                for (char byte : std::string_view("  |  ")) { result.joined[result.joinedLength++] = byte; }
            }
            // A local newline cannot turn alternate help back into multiple rows
            for (char byte : result.lines[index]) {
                result.joined[result.joinedLength++] = byte == '\n' || byte == '\r' ? ' ' : byte;
            }
        }
    }
    return result;
}

float MeasureInstructionWidth(const InstructionPanelContent& content, const layout::Hud& hud) {
    float width = 1;
    for (std::size_t index = 0; index < content.LineCount(); ++index) {
        const auto line = content.Line(index);
        const auto measured = ImGui::GetFont()->CalcTextSizeA(std::max(1.0f, hud.instructionFont),
            std::numeric_limits<float>::max(), 0, line.data(), line.data() + line.size());
        width = std::max(width, measured.x / hud.scale + 2);
    }
    return width;
}

void PrepareInstructionText(const InstructionPanelContent& content, const layout::Hud& hud, float font) {
    if (!hud.instructionsVisible || !content.LineCount()) { return; }
    for (std::size_t index = 0; index < content.LineCount(); ++index) {
        const auto line = content.Line(index);
        ImGui::GetFont()->CalcTextSizeA(font, 10000, 0, line.data(), line.data() + line.size());
    }
}

void DrawInstructionPanel(ImDrawList& draw, const InstructionPanelContent& content,
    const layout::Hud& hud, layout::Color theme, float font, bool rainbow, std::optional<layout::Color> fixedTheme, std::optional<InstructionHudColors> retainedColors) {
    if (!hud.instructionsVisible || !content.LineCount()) { return; }
    if (hud.instructionsBackground) {
        const auto outline = retainedColors ? retainedColors->outline : hud.design == MenuDesign::Aio ? fixedTheme.value_or(theme) : theme;
        Decoration(draw, hud.outline, IM_COL32(outline.red, outline.green, outline.blue, outline.alpha));
        // Colorless legacy AIO starts black; retained endpoints do not claim fixed fade curves
        const auto fill = retainedColors ? retainedColors->background : hud.design == MenuDesign::Aio && rainbow ? theme
            : hud.design == MenuDesign::Classic ? layout::Color{25, 25, 25, 217}
            : hud.design == MenuDesign::Apparition ? layout::Instructions : layout::Color{0, 0, 0, 255};
        Decoration(draw, hud.instructions, IM_COL32(fill.red, fill.green, fill.blue, fill.alpha));
    }
    const auto visibleText = VisiblePart(hud.instructionText);
    if (visibleText.width <= 0 || visibleText.height <= 0) { return; }
    const float step = hud.instructionText.height / content.LineCount();
    draw.PushClipRect(Top(visibleText), Bottom(visibleText), true);
    for (std::size_t index = 0; index < content.LineCount(); ++index) {
        const auto line = content.Line(index);
        ImVec2 point{hud.instructionText.x + hud.scale, hud.instructionText.y + step * index};
        if (content.alternate) {
            const float measured = ImGui::GetFont()->CalcTextSizeA(font, 10000, 0,
                line.data(), line.data() + line.size()).x;
            point = {hud.instructionAnchor.x - (hud.instructionsCentered ? measured / 2 : 0),
                hud.instructionAnchor.y - font / 2};
        }
        draw.AddText(ImGui::GetFont(), font, point, IM_COL32_WHITE, line.data(), line.data() + line.size());
    }
    draw.PopClipRect();
}
}
