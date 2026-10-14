#include "UseContinuationGraphSupport.h"
#include <array>

namespace iw::overlay::tests::useGraph {
namespace {
struct Expected {
    const char* name;
    std::uint64_t ack, revision, sequence;
    bool boolean;
    // Zero means retired or absent, one means pending, and two means cooldown
    int phase;
};
const std::array expected{
    Expected{"plain-before0.json",12,0,0,false,0},
    Expected{"plain-first0.json",13,1,13,false,2},
    Expected{"plain-first249.json",13,1,13,false,2},
    Expected{"plain-first250.json",13,1,13,false,0},
    Expected{"plain-second250.json",14,2,14,false,2},
    Expected{"plain-second499.json",14,2,14,false,2},
    Expected{"plain-second500.json",14,2,14,false,0},
    Expected{"plain-release750.json",14,2,14,false,0},
    Expected{"boolean-before0.json",10,0,0,true,0},
    Expected{"boolean-first0.json",11,1,11,true,1},
    Expected{"boolean-first179.json",11,1,11,true,1},
    Expected{"boolean-first180.json",11,1,11,true,2},
    Expected{"boolean-first429.json",11,1,11,true,2},
    Expected{"boolean-first430.json",11,1,11,true,0},
    Expected{"boolean-second430.json",12,2,12,true,1},
    Expected{"boolean-second609.json",12,2,12,true,1},
    Expected{"boolean-second610.json",12,2,12,true,2},
    Expected{"boolean-second859.json",12,2,12,true,2},
    Expected{"boolean-second860.json",12,2,12,true,0},
    Expected{"boolean-release1000.json",12,2,12,true,0},
    Expected{"instructions-before0.json",9,0,0,true,0},
    Expected{"instructions-first0.json",10,1,10,true,1},
    Expected{"instructions-first180.json",10,1,10,true,2},
    Expected{"instructions-first430.json",10,1,10,true,0},
    Expected{"instructions-second430.json",11,2,11,true,1},
    Expected{"instructions-second610.json",11,2,11,true,2},
    Expected{"instructions-second860.json",11,2,11,true,0},
    Expected{"queued-before0.json",3,0,0,true,0},
    Expected{"queued-intent0.json",4,0,0,true,0},
    Expected{"queued-later-ack0.json",5,0,0,true,0},
    Expected{"queued-start0.json",5,1,4,true,1},
    Expected{"queued-expired-busy430.json",5,1,4,true,0},
    Expected{"queued-complete430.json",5,1,4,true,0},
    Expected{"navigation-ready430.json",11,1,11,true,0},
    Expected{"navigation-moved430.json",12,0,0,true,0},
    Expected{"owner-active0.json",11,1,11,true,1},
    Expected{"owner-replaced0.json",11,0,0,true,0},
    Expected{"focus-active0.json",2,1,2,true,1},
    Expected{"focus-hidden0.json",2,0,0,true,0},
    Expected{"focus-return430.json",2,0,0,true,0},
    Expected{"peer-active0.json",10,2,10,true,1},
    Expected{"peer-ready430.json",10,2,10,true,0},
    Expected{"peer-replaced430.json",10,0,0,true,0},
    Expected{"cohort-active0.json",12,1,12,true,1},
    Expected{"cohort-ready430.json",12,1,12,true,0},
    Expected{"cohort-replaced430.json",12,0,0,true,0},
    Expected{"rollback-before100.json",11,1,11,true,1},
    Expected{"rollback-recovered10.json",11,1,11,true,1},
    Expected{"rollback-ready440.json",11,1,11,true,0}};
}
void ActualInventoryOwnsExactReceiptsAndBothReaders(const std::filesystem::path& directory) {
    std::set<std::string> actual, wanted;
    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        Check(entry.is_regular_file() && entry.path().extension() == ".json", "Graph inventory contains an unexpected entry");
        actual.insert(entry.path().filename().string());
    }
    const auto labels = runtime::DescribeOpeningActions(Bindings());
    for (const auto& item : expected) {
        wanted.insert(item.name);
        const auto state = Read(directory / item.name);
        const auto& frame = *state.snapshot;
        const auto preview = ReadBackendPreview(directory / item.name, &labels);
        Check(state.lastSequence == item.ack, "Actual graph changed its independent acknowledgment oracle");
        Check(frame.menuUseContinuation == preview.menuUseContinuation && frame.menuOptionCallback == preview.menuOptionCallback
            && frame.localMenuEligibility == preview.localMenuEligibility, "Strict readers disagree about actual causal ownership");
        Check(frame.menuUseContinuation.has_value() == (item.revision != 0), "Reader invented or erased an actual receipt");
        const auto kind = item.boolean ? MenuOptionCallbackKind::Boolean : MenuOptionCallbackKind::Plain;
        if (item.revision) Check(*frame.menuUseContinuation == MenuUseContinuation{item.revision,item.sequence,10,kind},
            "Receipt differs from original invocation, sequence, or admission");
        Check(frame.menuOptionCallback.has_value() == (item.phase != 0), "Actual callback retirement boundary changed");
        if (item.phase) Check(*frame.menuOptionCallback == MenuOptionCallback{item.revision,kind,
            item.phase == 1 ? MenuOptionCallbackPhase::Pending : MenuOptionCallbackPhase::Cooldown},
            "Actual callback kind, revision, or phase changed");
        Check(frame.navigationRevision == preview.navigationRevision && frame.menuRevision == preview.menuRevision
            && frame.selectedIndex == preview.selectedIndex && frame.isOpen == preview.isOpen
            && frame.rows.size() == preview.rows.size(), "Reader changed actual navigation or redraw state");
        for (std::size_t index = 0; index < frame.rows.size(); ++index) {
            Check(frame.rows[index].label == preview.rows[index].label && frame.rows[index].value == preview.rows[index].value
                && frame.rows[index].enabled == preview.rows[index].enabled, "Reader changed actual row presentation");
        }
    }
    // Exact membership prevents a partial graph from silently presenting as complete coverage
    Check(actual == wanted, "Actual Use graph has missing or unexpected packets");
}
}
