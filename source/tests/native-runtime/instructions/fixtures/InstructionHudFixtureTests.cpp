#include "InstructionHudFixtureTests.h"
#include "InstructionHudFixtureFiles.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include "../../../../native/runtime/ControlLabels.h"
#include "../../../../native/render/instructions/InstructionHudLifetime.h"
#include <map>
#include <set>

namespace iw::overlay::tests::instructions {
void ActualInstructionHudGraphJoinsCopiedAdmissionCreationSavedRoutesAndWithdrawal(const std::filesystem::path& directory) {
    CheckInstructionInventory(directory);
    // These saved groups belong to the offline fixture, not a guessed live desktop binding
    const runtime::Bindings bindings{{2},{86,6},{2,38},{1,40},{70},{51},{52},{1},{38},{40},{71},{69},{16},{17},{32}};
    const auto labels = runtime::DescribeOpeningActions(bindings);
    InstructionHudLifetime lifetime;
    unsigned subgraph = 0;
    std::map<std::string,std::shared_ptr<const MenuSnapshot>> states;
    for (const auto& expected : InstructionProfiles) {
        if (subgraph != expected.subgraph) { lifetime = InstructionHudLifetime{}; subgraph = expected.subgraph; }
        const auto path = directory / InstructionFilename(expected);
        const auto bytes = ReadInstructionPacket(path);
        const auto parsed = runtime::ParseState(bytes,42,nullptr,nullptr,nullptr,nullptr,nullptr,
            std::nullopt,nullptr,nullptr,&labels);
        const auto& state = *parsed.snapshot;
        const auto preview = ReadBackendPreview(path,&labels);
        const auto wire = Json::parse(bytes);
        RequireInstruction(state.instructionHud && state.instructionHud->exists == expected.exists
            && state.isOpen == expected.open && state.navigationRevision == expected.navigation
            && state.menuRevision == expected.menu && state.menuChromeRevision == (expected.chrome ? std::optional<std::uint64_t>{expected.chrome} : std::nullopt)
            && parsed.lastSequence == expected.sequence && state.rows.size() == expected.rows && state.selectedIndex == expected.selected
            && state.menuSurface == (expected.quick ? MenuSurface::Quick : MenuSurface::Main),
            "Actual independent instruction or body epochs changed");
        RequireInstruction(state.localMenuEligibility && state.localMenuEligibility->revision == expected.admission
            && state.localMenuEligibility->life == expected.life && state.localMenuEligibility->hasMenu == expected.hasMenu
            && state.localMenuEligibility->controlsDisabled == expected.controlsDisabled && !state.localMenuEligibility->blocked,
            "Instruction painting changed the current actor admission facts");
        RequireInstruction(state.mainOpeningControls == MainOpeningControls{1,{OpeningAction::Aim,OpeningAction::Melee}}
            && state.openingPolicies && state.openingPolicies->revision == (expected.subgraph==1?2u:1u)
            && state.openingPolicies->main == *state.mainOpeningControls
            && state.openingPolicies->quick == MainOpeningControls{1,{OpeningAction::Aim,OpeningAction::SecondaryOffhand}}
            && state.openingPolicies->quickMenuDisabled == (expected.subgraph==1),
            "Independent footer ownership changed configured opening policy");
        const auto& hud = *state.instructionHud;
        if (expected.exists) {
            RequireInstruction(hud.creationRevision == expected.creation && hud.admissionRevision == expected.admission
                && hud.creationDesign == expected.creationDesign && hud.creationAlternate == expected.alternate
                && hud.creationBackground == expected.background, "Copied creation styles or ownership changed");
        } else {
            RequireInstruction(!hud.creationRevision && !hud.admissionRevision && !hud.creationDesign
                && !hud.creationAlternate && !hud.creationBackground && hud.instructions.empty(), "Explicit absence retained active fields");
        }
        RequireInstruction(lifetime.Observe(&state) == expected.exists, "Actual HUD history confused rows, chrome, focus or independent owners");
        RequireInstruction(preview.instructionHud && preview.instructionHud->exists == hud.exists
            && preview.instructionHud->creationRevision == hud.creationRevision && preview.instructionHud->admissionRevision == hud.admissionRevision
            && preview.instructionHud->creationDesign == hud.creationDesign && preview.instructionHud->creationAlternate == hud.creationAlternate
            && preview.instructionHud->creationBackground == hud.creationBackground
            && preview.instructionHud->creationAccent == hud.creationAccent
            && preview.instructionHud->creationRainbow == hud.creationRainbow && preview.instructionHud->instructions == hud.instructions
            && preview.localMenuEligibility == state.localMenuEligibility && preview.menuSurface == state.menuSurface,
            "Production and preview readers disagree on actual instruction ownership");
        for (std::size_t row=0;row<state.rows.size();++row)
            RequireInstruction(state.rows[row].value == wire.at("rows").at(row).at("value").get<std::string>()
                && state.rows[row].enabled == wire.at("rows").at(row).at("enabled").get<bool>()
                && preview.rows[row].value == state.rows[row].value && preview.rows[row].enabled == state.rows[row].enabled,
                "Instruction metadata changed current values or feature permission");
        const auto& descriptor = wire.at("instructionHud");
        RequireInstruction((descriptor.size()==6 || descriptor.size()==8) && !descriptor.contains("instructions") && !descriptor.contains("actor")
            && !descriptor.contains("accountId"), "Instruction wire exposed private bindings or actor identity");
        if (!state.isOpen && expected.exists) {
            if (expected.life == Alive) RequireInstruction(hud.instructions == std::vector<std::string>{
                "RMB + V / Mouse 5: Open Main Menu", "RMB + E: Open Quick Menu"}, "Actual living closed help lost saved independent tuples");
            else RequireInstruction(hud.instructions == std::vector<std::string>{expected.subgraph==1
                ? "RMB + Space: Quick Menu unavailable (disabled)" : "RMB + Space: Open Quick Menu"},
                "Actual dead closed help invented an enabled or configured chord");
        }
        states.emplace(expected.name, parsed.snapshot);
    }
    // Production routing uses the same exact fixture groups; a footer never authorizes an extra route
    runtime::NativeInputRouter router(bindings); std::set<int> keys;
    const auto poll = [&](const char* name,std::uint64_t clock) {
        const auto& state = *states.at(name);
        return router.Poll(true,state.isOpen,false,[&](int key){return keys.contains(key);},clock,
            state.navigationRevision,state.appearance.quickExit,state.isRootMenu,runtime::InputModeForSnapshot(state),
            state.appearance.scrollAnimationMilliseconds,state.menuRevision,
            state.mainOpeningControls ? &*state.mainOpeningControls : nullptr,0,0,
            state.openingPolicies ? &*state.openingPolicies : nullptr,
            state.localMenuEligibility ? &*state.localMenuEligibility : nullptr,state.menuSurface);
    };
    RequireInstruction(!poll("alive-closed",0),"Copied closed policy replayed on installation");
    keys={2,69}; RequireInstruction(poll("alive-closed",1)==runtime::Input::OpenQuick,"Living saved Quick help disagrees with routing");
    keys.clear(); RequireInstruction(!poll("dead-closed-quick-disabled",2),"Dead context inherited an alive chord");
    keys={2,32}; RequireInstruction(!poll("dead-closed-quick-disabled",3),"Disabled dead Quick was authorized by footer existence");
    keys.clear(); RequireInstruction(!poll("quick-dead",4),"Dead Quick surface inherited a held key");
    keys={32}; RequireInstruction(poll("quick-dead",5)==runtime::Input::JumpBack,"Saved dead Jump hint disagrees with ordinary Quick routing");
}
}
