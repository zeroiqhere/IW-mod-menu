#include "FooterWithdrawalFixtureTests.h"
#include <iostream>
int main(int argc,char** argv) {
    try {
        if(argc<2 || argc>3 || (argc==3 && std::string_view(argv[2])!="--dump"))
            throw std::runtime_error("Expected actual footer withdrawal directory and optional --dump");
        iw::overlay::tests::footer_withdrawal::ActualFooterWithdrawalsJoinSuppressedPaintAndRetainedHistory(argv[1],argc==3?&std::cout:nullptr);
        std::cout<<"PASS ActualFooterWithdrawalsJoinSuppressedPaintAndRetainedHistory (one aggregate)\n";return 0;
    }catch(const std::exception& error){std::cerr<<"FAIL "<<error.what()<<'\n';return 1;}
}
