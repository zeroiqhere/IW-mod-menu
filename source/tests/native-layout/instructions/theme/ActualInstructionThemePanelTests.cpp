#include "ActualInstructionThemePanelTests.h"
#include "../InstructionFixturePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../native-runtime/instructions/theme/fixtures/InstructionThemeFixtureTests.h"
#include <memory>
namespace iw::overlay::tests {
namespace {
bool Solid(const ImDrawData& data,layout::Color color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();const auto packed=IM_COL32(color.red,color.green,color.blue,color.alpha);
    for(const auto* list:data.CmdLists)for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430&&vertex.uv.x==white.x&&vertex.uv.y==white.y&&vertex.col==packed)return true;
    return false;
}
bool Pixel(const std::vector<unsigned char>& pixels,layout::Color color) {
    for(unsigned y=430;y<480;++y)for(unsigned x=0;x<640;++x){const auto offset=(y*640+x)*3;
        if(pixels[offset]==color.red&&pixels[offset+1]==color.green&&pixels[offset+2]==color.blue)return true;}
    return false;
}
}
void AddActualInstructionThemePanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual instruction callback journal paints independent linear RGB without rewriting current permission or raw values",[directory] {
        using namespace instruction_theme;CheckInventory(directory);
        std::unique_ptr<InstructionHudLifetime> lifetime;std::unique_ptr<InstructionHudColorHistory> history;
        std::unique_ptr<instructions::InstructionFixturePanel> panel;unsigned graph=99;
        for(const auto& expected:Profiles) {
            if(graph!=expected.graph) {
                graph=expected.graph;lifetime=std::make_unique<InstructionHudLifetime>();history=std::make_unique<InstructionHudColorHistory>();
                panel=std::make_unique<instructions::InstructionFixturePanel>();
            }
            const auto state=ReadAndCheck(directory,expected);const bool admitted=lifetime->Observe(&state);
            const layout::Color sample{80,160,240,255};
            CheckColors(expected,history->Update(state,admitted,sample,expected.clock));
            const auto& data=panel->Draw(state,admitted,sample,expected.clock);const auto pixels=panel->textures.Raster(data,640,480);
            if(!expected.creation) {
                palette::RequirePalette(!palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),"Withdrawn actual theme drew footer hints");continue;
            }
            palette::RequirePalette(palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),"Actual theme lost white instruction text");
            if(state.instructionHud->creationBackground==true) {
                palette::RequirePalette(Solid(data,expected.outline)&&Pixel(pixels,expected.outline),"Actual outline missed independent RGB pixels");
                palette::RequirePalette(Solid(data,expected.background)&&Pixel(pixels,expected.background),"Actual background missed independent RGB pixels");
            } else palette::RequirePalette(!Solid(data,expected.outline),"Actual absent rectangle received a theme assignment");
            if(!state.isOpen)for(const auto* list:data.CmdLists)for(const auto& vertex:list->VtxBuffer)
                palette::RequirePalette(vertex.pos.y>=430,"Closed actual callback graph drew ordinary menu chrome");
            if(expected.name=="same-red70") {panel->ResetGraphics();const auto& reset=panel->Draw(state,admitted,sample,expected.clock);
                palette::RequirePalette(Pixel(panel->textures.Raster(reset,640,480),expected.outline),"Graphics reset lost the interrupted RGB channel");}
        }
        // This separate native owner deliberately missed the prefix rather than borrowing another graph's history
        InstructionHudLifetime delayedLifetime;instructions::InstructionFixturePanel delayed;
        for(unsigned index:{29U,30U,31U}) {
            const auto& expected=Profiles[index];const auto state=ReadAndCheck(directory,expected);
            const auto& data=delayed.Draw(state,delayedLifetime.Observe(&state),layout::Color{80,160,240,255},expected.clock);
            const auto expectedOutline=index==31?layout::Color{255,0,0,255}:layout::Color{57,152,254,255};
            palette::RequirePalette(Pixel(delayed.textures.Raster(data,640,480),expectedOutline),"Delayed actual journal guessed a missing fade start");
        }
    });
}
}
