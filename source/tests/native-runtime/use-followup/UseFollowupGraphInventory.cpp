#include "../use-monitor/UseContinuationGraphSupport.h"
#include <array>

namespace iw::overlay::tests::useGraph {
namespace {
struct Expected {
    const char* name;
    std::uint64_t ack, receiptSequence;
    int callbackPhase;
    std::uint64_t waitRevision;
    char waitKind;
};
// Independent literal oracles keep agreement between readers from becoming the only proof
const std::array expected{
    Expected{"plain-next-before0.json",14,0,0,0,'-'},
    Expected{"plain-next-use0.json",15,15,2,0,'-'},
    Expected{"plain-next-use249.json",15,15,2,0,'-'},
    Expected{"plain-next-ready250.json",15,15,0,0,'-'},
    Expected{"plain-next-choice250.json",16,0,0,1,'c'},
    Expected{"plain-next-choice429.json",16,0,0,1,'c'},
    Expected{"plain-next-choice430.json",16,0,0,0,'-'},
    Expected{"plain-next-repeat430.json",17,0,0,2,'c'},
    Expected{"plain-next-repeat610.json",17,0,0,0,'-'},
    Expected{"plain-previous-before0.json",14,0,0,0,'-'},
    Expected{"plain-previous-use0.json",15,15,2,0,'-'},
    Expected{"plain-previous-use249.json",15,15,2,0,'-'},
    Expected{"plain-previous-ready250.json",15,15,0,0,'-'},
    Expected{"plain-previous-choice250.json",16,0,0,1,'c'},
    Expected{"plain-previous-choice429.json",16,0,0,1,'c'},
    Expected{"plain-previous-choice430.json",16,0,0,0,'-'},
    Expected{"plain-back-before0.json",14,0,0,0,'-'},
    Expected{"plain-back-use0.json",15,15,2,0,'-'},
    Expected{"plain-back-use249.json",15,15,2,0,'-'},
    Expected{"plain-back-ready250.json",15,15,0,0,'-'},
    Expected{"plain-back-child250.json",16,0,0,1,'b'},
    Expected{"plain-back-child499.json",16,0,0,1,'b'},
    Expected{"plain-back-child500.json",16,0,0,0,'-'},
    Expected{"plain-back-main500.json",17,0,0,2,'b'},
    Expected{"plain-back-main749.json",17,0,0,2,'b'},
    Expected{"plain-back-main750.json",17,0,0,0,'-'},
    Expected{"plain-back-root750.json",18,0,0,3,'b'},
    Expected{"plain-back-root999.json",18,0,0,3,'b'},
    Expected{"plain-back-root1000.json",18,0,0,0,'-'},
    Expected{"quick-exit-child-before0.json",27,0,0,0,'-'},
    Expected{"quick-exit-child-use0.json",28,28,2,0,'-'},
    Expected{"quick-exit-child-use249.json",28,28,2,0,'-'},
    Expected{"quick-exit-child-ready250.json",28,28,0,0,'-'},
    Expected{"quick-exit-child-ready339.json",28,28,0,0,'-'},
    Expected{"quick-exit-child-ready340.json",28,28,0,0,'-'},
    Expected{"quick-exit-child-close340.json",29,0,0,1,'b'},
    Expected{"quick-exit-child-close589.json",29,0,0,1,'b'},
    Expected{"quick-exit-child-close590.json",29,0,0,0,'-'},
    Expected{"quick-exit-root-before0.json",29,0,0,0,'-'},
    Expected{"quick-exit-root-back0.json",30,0,0,3,'b'},
    Expected{"quick-exit-root-back249.json",30,0,0,3,'b'},
    Expected{"quick-exit-root-back250.json",30,0,0,0,'-'},
    Expected{"boolean-back-before0.json",12,0,0,0,'-'},
    Expected{"boolean-back-use0.json",13,13,1,0,'-'},
    Expected{"boolean-back-use179.json",13,13,1,0,'-'},
    Expected{"boolean-back-use180.json",13,13,2,0,'-'},
    Expected{"boolean-back-use429.json",13,13,2,0,'-'},
    Expected{"boolean-back-ready430.json",13,13,0,0,'-'},
    Expected{"boolean-back-back430.json",14,0,0,1,'b'},
    Expected{"boolean-back-back679.json",14,0,0,1,'b'},
    Expected{"boolean-back-back680.json",14,0,0,0,'-'}};
}
void ActualFollowupInventoryMatchesIndependentOraclesAndBothReaders(const std::filesystem::path& directory) {
    std::set<std::string> names, actual;
    const auto labels=runtime::DescribeOpeningActions(Bindings());
    for(const auto& entry:std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file()&&entry.path().extension()==".json","Follow-up graph contains an unexpected entry");
        actual.insert(entry.path().filename().string());
    }
    for(const auto& item:expected) {
        names.insert(item.name);const auto state=Read(directory/item.name);const auto& frame=*state.snapshot;
        const auto preview=ReadBackendPreview(directory/item.name,&labels);
        Check(state.lastSequence==item.ack,"Actual follow-up changed its accepted command sequence");
        Check(frame.menuUseContinuation==preview.menuUseContinuation&&frame.menuOptionCallback==preview.menuOptionCallback
            &&frame.menuMonitorWait==preview.menuMonitorWait&&frame.localMenuEligibility==preview.localMenuEligibility,
            "Readers disagree about actual callback, wait or admission");
        Check(frame.menuUseContinuation.has_value()==(item.receiptSequence!=0),"Follow-up invented or erased the original Use receipt");
        const auto kind=std::string(item.name).starts_with("boolean-")?MenuOptionCallbackKind::Boolean:MenuOptionCallbackKind::Plain;
        if(item.receiptSequence)Check(*frame.menuUseContinuation==MenuUseContinuation{1,item.receiptSequence,10,kind},"Follow-up replaced original callback ownership");
        Check(frame.menuOptionCallback.has_value()==(item.callbackPhase!=0),"Follow-up changed actual callback retirement");
        if(item.callbackPhase)Check(*frame.menuOptionCallback==MenuOptionCallback{1,kind,item.callbackPhase==1
            ?MenuOptionCallbackPhase::Pending:MenuOptionCallbackPhase::Cooldown},"Follow-up changed callback kind or phase");
        Check(frame.menuMonitorWait.has_value()==(item.waitRevision!=0),"Follow-up changed mechanical pause readiness");
        if(item.waitRevision)Check(*frame.menuMonitorWait==MenuMonitorWait{item.waitRevision,item.waitKind=='c'
            ?MenuMonitorWaitKind::Choice:MenuMonitorWaitKind::Back},"New source branch borrowed another wait");
        Check(frame.navigationRevision==preview.navigationRevision&&frame.menuRevision==preview.menuRevision
            &&frame.selectedIndex==preview.selectedIndex&&frame.rows.size()==preview.rows.size()&&frame.isOpen==preview.isOpen,
            "Reader changed actual follow-up navigation or visibility");
        for(std::size_t i=0;i<frame.rows.size();++i)Check(frame.rows[i].label==preview.rows[i].label
            &&frame.rows[i].value==preview.rows[i].value&&frame.rows[i].enabled==preview.rows[i].enabled,
            "Reader changed actual follow-up row state");
    }
    Check(names==actual,"Actual51 follow-up inventory changed silently");
}
}
