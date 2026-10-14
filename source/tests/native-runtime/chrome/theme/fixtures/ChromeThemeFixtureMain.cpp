#include "ChromeThemeFixtureTests.h"
#include <iostream>
int main(int argc,char** argv) {
    try {
        if (argc<2 || argc>3 || (argc==3 && std::string_view(argv[2])!="--dump")) throw std::runtime_error("Expected fixture directory and optional --dump");
        iw::overlay::tests::chrome_theme::ActualChromeThemesJoinCreationScopesAndIndependentRoleColors(argv[1],argc==3?&std::cout:nullptr);
        std::cout<<"PASS ActualChromeThemesJoinCreationScopesAndIndependentRoleColors (54 profiles, one aggregate)\n";return 0;
    } catch (const std::exception& error) { std::cerr<<"FAIL "<<error.what()<<'\n';return 1; }
}
