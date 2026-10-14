#include "ActualInstructionHudPanelTests.h"
#include "InstructionFixturePanel.h"
#include "../palette/PalettePanel.h"
#include "../../native-runtime/instructions/fixtures/InstructionHudFixtureFiles.h"
#include "../../../native/runtime/RuntimeSchema.h"
#include "../../../native/runtime/ControlLabels.h"
#include "../../../native/render/instructions/InstructionHudLifetime.h"
#include "../BackendPreview.h"
#include <memory>

namespace iw::overlay::tests {
namespace {
bool HasSolid(const ImDrawData& data, ImU32 color) {
    const auto white=ImGui::GetFontTexUvWhitePixel();
    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
        if(vertex.col==color && vertex.uv.x==white.x && vertex.uv.y==white.y && vertex.pos.y>=430) return true;
    return false;
}
}
void AddActualInstructionHudPanelChecks(std::vector<std::pair<std::string,std::function<void()>>>& checks,
    const std::filesystem::path& directory) {
    checks.emplace_back("Actual independent instruction graph draws closed footer only and preserves captured paint", [directory] {
        using namespace instructions;
        CheckInstructionInventory(directory);
        const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
        const auto labels=runtime::DescribeOpeningActions(bindings);
        const auto controls=runtime::DescribeControls(bindings), width=runtime::DescribeWidthEditorControls(bindings);
        auto panel=std::make_unique<InstructionFixturePanel>();
        InstructionHudLifetime lifetime;unsigned subgraph=0;
        for(const auto& expected:InstructionProfiles) {
            // Independent controller subgraphs cannot share a numeric creation namespace
            if(subgraph!=expected.subgraph) { panel=std::make_unique<InstructionFixturePanel>();lifetime=InstructionHudLifetime{};subgraph=expected.subgraph; }
            const auto path=directory/InstructionFilename(expected);
            const auto parsed=runtime::ParseState(ReadInstructionPacket(path),42,&controls,nullptr,nullptr,&width,
                nullptr,std::nullopt,nullptr,nullptr,&labels);
            const auto& state=*parsed.snapshot;const auto preview=ReadBackendPreview(path,&labels);
            RequireInstruction(state.instructionHud && preview.instructionHud
                && state.instructionHud->creationRevision==preview.instructionHud->creationRevision
                && state.instructionHud->instructions==preview.instructionHud->instructions,
                "Actual instruction readers disagree before drawing");
            const bool admitted=lifetime.Observe(&state);
            RequireInstruction(admitted==expected.exists,"Actual footer lifetime adopted an absent or stale owner");
            const auto& data=panel->Draw(state,admitted);
            if(!expected.open) {
                RequireInstruction(panel->drawn==expected.exists && panel->summary.shownRows==0,
                    "Closed actual packet painted an ordinary option list");
                if(expected.exists) {
                    RequireInstruction(data.TotalVtxCount>0 && palette::HasGlyphColor(data,IM_COL32_WHITE,{0,440,640,40}),
                        "Actual saved opening hints did not draw");
                    for(const auto* list:data.CmdLists) for(const auto& vertex:list->VtxBuffer)
                        RequireInstruction(vertex.pos.y>=440,"Closed actual packet leaked title, rows, counter or status");
                } else RequireInstruction(data.TotalVtxCount==0,"Absent closed actor emitted overlay pixels");
            }
            if(!expected.exists) {
                RequireInstruction(!palette::HasGlyphColor(data,IM_COL32_WHITE,{0,440,640,40}),
                    "Explicit absent open descriptor retained actionable help");
            } else if(expected.background && !expected.alternate) {
                // Literal source background bytes are independent of the production style resolver
                const auto gray=expected.creationDesign==MenuDesign::Classic ? IM_COL32(25,25,25,217) : IM_COL32(42,42,42,255);
                RequireInstruction(HasSolid(data,gray),"Created footer paint followed current body design instead of its own widget");
            }
            RequireInstruction(state.rows.size()==expected.rows && state.navigationRevision==expected.navigation,
                "Footer painting mutated immutable ordinary state");
        }
    });
}
}
