#include "InstructionThemeFixtureTests.h"
#include <iostream>
int main(int argc,char** argv) {
    try {
        if(argc!=2)throw std::runtime_error("Expected actual instruction theme fixture directory");
        iw::overlay::tests::instruction_theme::ActualInstructionThemeJournalsJoinOwnedEpochsAndIndependentLinearColors(argv[1]);
        std::cout<<"PASS ActualInstructionThemeJournalsJoinOwnedEpochsAndIndependentLinearColors (34 profiles, one aggregate)\n";return 0;
    }catch(const std::exception& error){std::cerr<<"FAIL "<<error.what()<<'\n';return 1;}
}
