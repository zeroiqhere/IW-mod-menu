#include "../../../native-runtime/instructions/color/fixtures/InstructionColorFixturePaint.h"
#include "ActualInstructionColorPanelTests.h"
#include "../InstructionFixturePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../native-runtime/instructions/color/fixtures/InstructionColorFixtureTests.h"
namespace iw::overlay::tests {
namespace {
bool HasSolid(const ImDrawData& data,layout::Color color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();const auto packed=IM_COL32(color.red,color.green,color.blue,color.alpha);
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.col==packed && vertex.uv.x==white.x && vertex.uv.y==white.y && vertex.pos.y>=430) return true;
    return false;
}
bool HasPixel(const std::vector<unsigned char>& pixels,layout::Color color) {
    for(unsigned y=430;y<480;++y) for(unsigned x=0;x<640;++x) {
        const auto offset=(y*640+x)*3;
        if(pixels[offset]==color.red && pixels[offset+1]==color.green && pixels[offset+2]==color.blue) return true;
    }
    return false;
}
}
void AddActualInstructionColorPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual instruction creation graph paints retained RGB endpoints and current-design Rainbow channels",[directory] {
        using namespace instruction_color;CheckInventory(directory);
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;
        instructions::InstructionFixturePanel panel;
        for(const auto& expected:Profiles) {
            const auto state=ReadAndCheck(directory,expected);
            const auto sample=expected.name.starts_with("design-out") ? layout::Color{121,242,0,255} : layout::Color{242,0,0,255};
            const bool admitted=lifetime.Observe(&state);
            const auto colors=history.Update(state,admitted,sample);CheckPaintChannels(expected,colors,state.instructionTheme.has_value());
            const auto& data=panel.Draw(state,admitted,sample);
            const auto pixels=panel.textures.Raster(data,640,480);
            if(!expected.creation) {
                palette::RequirePalette(!palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),
                    "Absent actual footer retained actionable glyphs");continue;
            }
            palette::RequirePalette(palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),
                "Current or retained instruction text lost source white ink");
            if(expected.creation==5) continue;
            // Independent source bytes bypass both production hex conversion and channel history
            const auto wanted=ExpectedPaint(expected,state.instructionTheme.has_value());
            const auto outline=wanted.outline,background=wanted.background;
            palette::RequirePalette(HasPixel(pixels,outline),"Actual footer outline pixels followed the wrong widget history");
            palette::RequirePalette(HasSolid(data,background),"Actual background primitive lost its independent RGB or alpha");
            if(background.alpha==255) palette::RequirePalette(HasPixel(pixels,background),
                "Actual background pixels used the outline channel or discarded retained assignments");
            if(!state.isOpen) {
                for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
                    palette::RequirePalette(vertex.pos.y>=430,"Closed actual color packet drew ordinary menu chrome");
            }
        }
        // Delayed first receipt must use the same production history path despite current fixed mode
        instructions::InstructionFixturePanel delayed;const auto state=ReadAndCheck(directory,Profiles[17]);
        InstructionHudLifetime delayedLifetime;
        const auto& data=delayed.Draw(state,delayedLifetime.Observe(&state),layout::Color{10,20,30,255});
        palette::RequirePalette(HasPixel(delayed.textures.Raster(data,640,480),{10,20,30,255}),
            "Delayed actual Rainbow creation guessed a fixed fallback outline");
    });
}
}
