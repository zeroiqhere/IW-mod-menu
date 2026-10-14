#include "ActualFooterWithdrawalPanelTests.h"
#include "../InstructionFixturePanel.h"
#include "../../palette/PalettePanel.h"
#include "../../../native-runtime/instructions/withdrawal/fixtures/FooterWithdrawalFixtureTests.h"
#include <algorithm>
#include <memory>
namespace iw::overlay::tests {
namespace {
bool FooterInk(const ImDrawData& data) {
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430) return true;
    return false;
}
bool Solid(const ImDrawData& data,layout::Color color) {
    const auto packed=IM_COL32(color.red,color.green,color.blue,color.alpha);
    const auto white=ImGui::GetFontTexUvWhitePixel();
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.pos.y>=430 && vertex.col==packed && vertex.uv.x==white.x && vertex.uv.y==white.y) return true;
    return false;
}
bool Pixel(const std::vector<unsigned char>& pixels,layout::Color color) {
    for(unsigned y=430;y<480;++y) for(unsigned x=0;x<640;++x) {
        const auto offset=(y*640+x)*3;
        if(pixels[offset]==color.red && pixels[offset+1]==color.green && pixels[offset+2]==color.blue) return true;
    }
    return false;
}
}
void AddActualFooterWithdrawalPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual qualified footer withdrawal suppresses all footer ink and recovers original source colors and styles",[directory] {
        using namespace footer_withdrawal;using palette::RequirePalette;CheckInventory(directory);
        unsigned graph=99;std::uint64_t nativeClock=0;
        std::unique_ptr<InstructionHudLifetime> lifetime;std::unique_ptr<instructions::InstructionFixturePanel> panel;
        for(const auto& expected:Profiles) {
            if(graph!=expected.graph) {
                graph=expected.graph;nativeClock=0;lifetime=std::make_unique<InstructionHudLifetime>();
                panel=std::make_unique<instructions::InstructionFixturePanel>();
            }
            const auto state=ReadAndCheck(directory,expected);nativeClock=std::max(nativeClock,expected.clock);
            const bool admitted=lifetime->Observe(&state);
            const auto& data=panel->Draw(state,admitted,layout::Color{80,160,240,255},nativeClock);
            if(!expected.creation) {
                RequirePalette(!FooterInk(data),"Actual unknown or revoked footer emitted legacy glyphs or rectangles");
                panel->ResetGraphics();continue;
            }
            RequirePalette(palette::HasGlyphColor(data,IM_COL32_WHITE,{0,430,640,50}),"Restored actual footer lost its saved hints");
            RequirePalette(Solid(data,expected.colors.outline) && Solid(data,expected.colors.background),
                "Recovered actual footer lost its original style or fixed invocation age");
            const auto pixels=panel->textures.Raster(data,640,480);
            RequirePalette(Pixel(pixels,expected.colors.outline),"Actual recovered outline missed independent RGB pixels");
            if(!state.isOpen) for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
                RequirePalette(vertex.pos.y>=430,"Closed actual footer painted ordinary menu chrome");
        }
    });
}
}
