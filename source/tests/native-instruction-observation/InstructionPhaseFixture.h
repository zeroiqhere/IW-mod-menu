#pragma once
#include "../../native/render/instructions/observation/InstructionRenderPhase.h"
#include <functional>
#include <stdexcept>
#include <string>
#include <vector>
namespace iw::overlay::tests::instruction_observation {
inline MenuSnapshot Owned() {
    MenuSnapshot state;state.isOpen=true;state.accent="#3998FE";
    state.localMenuEligibility=LocalMenuEligibility{7,PlayerLifeState::Alive,true,false,false};
    state.instructionHud=InstructionHud{true,2,7,MenuDesign::Apparition,false,true,
        {},"#3998FE",false};
    state.instructionTheme=InstructionThemeState{2,true,{}};return state;
}
inline InstructionThemeEvent Red(std::uint64_t age,bool stopped=false) {
    return {1,InstructionThemeTarget::Outline,{255,0,0},age,std::nullopt,stopped};
}
inline void Require(bool value,const char* message) {if(!value) throw std::runtime_error(message);}
inline void Color(const InstructionPaintObservation& observation,unsigned red,unsigned green,unsigned blue) {
    Require(observation.colorsAdmitted && observation.colors,"Owned footer colors were not admitted");
    const auto color=observation.colors->outline;
    Require(color.red==red && color.green==green && color.blue==blue && color.alpha==255,
        "Observed footer seed or fixed age changed across preparation failure");
}
using Checks=std::vector<std::pair<std::string,std::function<void()>>>;
void AddInstructionPhaseChecks(Checks& checks);
}
