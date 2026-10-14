#include "InstructionPhaseFixture.h"
namespace iw::overlay::tests::instruction_observation {
void AddInstructionPhaseChecks(Checks& checks) {
    checks.emplace_back("Failed graphics preparation retains an observed Rainbow start for a later fixed stop",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();state.smoothRainbowTheme=true;
        unsigned paints=0;
        Require(!RunInstructionRenderPhase(state,lifetime,history,layout::Color{80,160,240,255},1000,
            [](bool admitted){Require(admitted,"Known source creation was denied");return false;},
            [&](const auto&){++paints;return true;}),"Failed preparation reported a successful paint");
        Require(paints==0,"Failed preparation invoked painting");
        state.smoothRainbowTheme=false;state.instructionTheme->events={Red(70,true)};
        Require(RunInstructionRenderPhase(state,lifetime,history,layout::Color{20,40,60,255},1070,
            [](bool){return true;},[&](const auto& observation){++paints;Color(observation,105,138,206);return true;}),
            "Recovered preparation failed to paint");
        Require(paints==1,"Recovered preparation painted more than once");
    });
    checks.emplace_back("Rainbow born creation records its first owner sample before graphics are available",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();
        state.instructionHud->creationRainbow=true;
        RunInstructionRenderPhase(state,lifetime,history,layout::Color{20,40,60,255},1000,
            [](bool){return false;},[](const auto&){throw std::runtime_error("Failed preparation painted");return true;});
        // Current mode and sample cannot replace the immutable first observation of this creation
        state.accent="#00FF00";state.smoothRainbowTheme=false;
        Require(RunInstructionRenderPhase(state,lifetime,history,layout::Color{80,160,240,255},1100,
            [](bool){return true;},[](const auto& observation){Color(observation,20,40,60);return true;}),
            "Known creation could not paint after preparation recovered");
    });
    checks.emplace_back("Preparation exceptions propagate without losing observed colors or invoking paint",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();state.smoothRainbowTheme=true;
        unsigned paints=0;bool threw=false;
        try {
            RunInstructionRenderPhase(state,lifetime,history,layout::Color{30,60,90,255},1000,
                [](bool)->bool{throw std::runtime_error("Owned prepare failure");},[&](const auto&){++paints;return true;});
        }catch(const std::runtime_error& error){threw=std::string_view(error.what())=="Owned prepare failure";}
        Require(threw && paints==0,"Preparation exception changed its contract or painted");
        state.smoothRainbowTheme=false;state.instructionTheme->events={Red(0,true)};
        RunInstructionRenderPhase(state,lifetime,history,layout::Color{80,160,240,255},1100,
            [](bool){return true;},[](const auto& observation){Color(observation,30,60,90);return true;});
    });
    checks.emplace_back("Failed fixed receipts remain part of immutable continuity and repeated valid ages do not restart",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1000,[](bool){return true;},[](const auto&){return true;});
        state.instructionTheme->events={Red(0)};
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1000,[](bool){return false;},[](const auto&){return true;});
        state.instructionTheme->events[0].elapsedMilliseconds=100;
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1100,[](bool){return false;},[](const auto&){return true;});
        // The same invocation's identity must also be checked on frames that never reach painting
        state.instructionTheme->events[0].accent={0,0,255};
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1100,[](bool){return false;},[](const auto&){return true;});
        state.instructionTheme->events[0]=Red(250);
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1250,[](bool){return true;},[](const auto& observation){
            Require(!observation.colorsAdmitted && !observation.colors,"Failed frame mutation was forgotten before recovery");return true;});
        // A separate uncorrupted owner uses source ages rather than the number of preparation attempts
        lifetime=InstructionHudLifetime{};history=InstructionHudColorHistory{};state=Owned();state.instructionTheme->events={Red(0)};
        for(auto now:{1000U,1050U,1100U})
            RunInstructionRenderPhase(state,lifetime,history,std::nullopt,now,[](bool){return false;},[](const auto&){return true;});
        state.instructionTheme->events[0].elapsedMilliseconds=250;
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1250,[](bool){return true;},[](const auto& observation){Color(observation,156,76,127);return true;});
    });
    checks.emplace_back("Unknown focus and corrupt creations never sample or regain the same retired color authority",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;auto state=Owned();state.smoothRainbowTheme=true;
        RunInstructionRenderPhase(state,lifetime,history,layout::Color{40,80,120,255},1000,[](bool){return true;},[](const auto&){return true;});
        auto hidden=state;hidden.instructionHud=InstructionHud{};hidden.instructionTheme.reset();
        hidden.localMenuEligibility->life=PlayerLifeState::Unknown;
        RunInstructionRenderPhase(hidden,lifetime,history,layout::Color{200,100,10,255},1050,[](bool){return true;},[](const auto& observation){
            Require(!observation.lifetimeAdmitted && !observation.colorsAdmitted && !observation.colors,"Unknown frame sampled or granted footer authority");return true;});
        hidden.isOpen=false;hidden.instructionHud.reset();
        RunInstructionRenderPhase(hidden,lifetime,history,layout::Color{255,255,255,255},1075,
            [](bool admitted){return admitted;},[](const auto&){throw std::runtime_error("Unowned closed focus painted");return true;});
        state.smoothRainbowTheme=false;state.instructionTheme->events={Red(0,true)};
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1100,[](bool){return true;},[](const auto& observation){Color(observation,40,80,120);return true;});
        state.instructionHud->creationAccent="#00FF00";
        RunInstructionRenderPhase(state,lifetime,history,layout::Color{1,2,3,255},1150,[](bool){return true;},[](const auto& observation){
            Require(!observation.colorsAdmitted && !observation.colors,"Corrupt creation sampled a replacement color");return true;});
        state.instructionHud->creationAccent="#3998FE";
        RunInstructionRenderPhase(state,lifetime,history,std::nullopt,1200,[](bool){return true;},[](const auto& observation){
            Require(!observation.colorsAdmitted,"Same quarantined creation recovered authority");return true;});
    });
    checks.emplace_back("Successful phase preserves legacy painting and invokes preparation and paint exactly once",[] {
        InstructionHudLifetime lifetime;InstructionHudColorHistory history;MenuSnapshot legacy;legacy.isOpen=true;
        unsigned prepares=0,paints=0;
        Require(RunInstructionRenderPhase(legacy,lifetime,history,std::nullopt,1000,[&](bool admitted){
            ++prepares;Require(!admitted,"Legacy footer gained independent ownership");return true;},[&](const auto& observation){
            ++paints;Require(!observation.lifetimeAdmitted && !observation.colorsAdmitted && !observation.colors,"Legacy phase gained owned color metadata");return true;}),
            "Legacy open paint was denied");
        Require(prepares==1 && paints==1,"Successful phase duplicated callbacks");
        legacy.isOpen=false;
        Require(!RunInstructionRenderPhase(legacy,lifetime,history,std::nullopt,1100,
            [](bool admitted){return admitted;},[&](const auto&){++paints;return true;}),"Legacy closed phase invented a footer");
        Require(paints==1,"Unowned closed phase invoked paint");
    });
}
}
