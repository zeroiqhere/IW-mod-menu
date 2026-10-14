#include "InstructionPhaseFixture.h"
#include <iostream>
int main() {
    iw::overlay::tests::instruction_observation::Checks checks;
    iw::overlay::tests::instruction_observation::AddInstructionPhaseChecks(checks);
    unsigned passed=0;
    for(const auto& [name,check]:checks) {
        try {check();++passed;std::cout<<"PASS "<<name<<'\n';}
        catch(const std::exception& error){std::cerr<<"FAIL "<<name<<": "<<error.what()<<'\n';}
    }
    std::cout<<passed<<'/'<<checks.size()<<" instruction observation checks passed\n";
    return passed==checks.size()?0:1;
}
