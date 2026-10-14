#include "OpeningPolicyScrollTests.h"
#include "../../../native/render/ScrollPresentation.h"
#include "../../../native/render/spacer/SpacerScrollPresentation.h"
#include <stdexcept>

namespace {
using namespace iw::overlay;
void Require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}
struct Rig {
    ScrollPresentation owner;
    ScrollRequest source, target;
    SpacerScrollReceipt receipt;
    MainOpeningControls main{1, {OpeningAction::Aim, OpeningAction::Melee}};
    ClosedMenuOpeningPolicies pair;
    Rig() {
        pair.main = main;
        source.active = true; source.menuRevision = 6; source.focusRevision = 8;
        source.navigationRevision = 10; source.rowCount = 14; source.shown = 10;
        source.selected = 1; source.layout = {640, 480, 260, 180, 10};
        source.topology.rowCount = 14; source.topology.spacerMask[0] = 4;
        source.openingPolicyIdentity = CaptureOpeningPolicyIdentity(main, pair);
        auto& plan = receipt.plan;
        plan.fromIndex = 1; plan.spacerIndex = 2; plan.spacerCount = 1;
        plan.targetIndex = 3; plan.capacity = 10; plan.topology = source.topology;
        receipt.sequence = 11; receipt.sourceNavigationRevision = 10; receipt.targetNavigationRevision = 11;
        receipt.menuRevision = 6; receipt.focusRevision = 8;
        receipt.issuedMilliseconds = 100; receipt.observedMilliseconds = 120;
        receipt.sourceOpeningControls = main; receipt.sourceOpeningPolicies = pair;
        target = source; target.selected = 3; target.navigationRevision = 11; target.spacerReceipt = &receipt;
    }
};
}
void AddOpeningPolicyScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("A new Quick policy cannot borrow old rendered Main history", [] {
        Rig rig;
        auto old = rig.source; auto oldPair = rig.pair; oldPair.revision = 2;
        old.openingPolicyIdentity = CaptureOpeningPolicyIdentity(rig.main, oldPair);
        rig.owner.Update(old, 0);
        const auto frame = rig.owner.Update(rig.target, 120);
        Require(!frame.spacerSequence, "Old policy history admitted a foreign receipt");
    });
    checks.emplace_back("Matching policy reports advance without restart and reject stale frame echoes", [] {
        Rig rig; rig.owner.Update(rig.source, 0);
        auto frame = rig.owner.Update(rig.target, 120);
        Require(frame.spacerSequence == 11 && frame.spacerPolicyIdentity == rig.source.openingPolicyIdentity,
            "The admitted frame lost its bounded policy proof");
        rig.receipt.observedMilliseconds = 140;
        frame = rig.owner.Update(rig.target, 140);
        Require(frame.spacerElapsed == 40, "New reports restarted dispatch age");
        frame.spacerPolicyIdentity.reset();
        Require(!spacer::MatchesReceiptFrame(frame, rig.target), "A stale frame reused current policy authority");
    });
    checks.emplace_back("Policy observation cannot precede installation or restart an owned opening", [] {
        Rig rig; auto old = rig.source; old.openingPolicyIdentity.reset();
        rig.owner.Update(old, 0); rig.owner.Update(rig.source, 110);
        Require(!rig.owner.Update(rig.target, 120).spacerSequence,
            "A policy observed after dispatch fabricated prior history");
        ScrollPresentation fade; old.opening = MenuOpening{1, 100};
        fade.Update(old, 0); auto current = old; current.openingPolicyIdentity = rig.source.openingPolicyIdentity;
        const auto frame = fade.Update(current, 50);
        Require(frame.openingElapsed && *frame.openingElapsed == 150,
            "Policy reseeding restarted the owner's row fade");
    });
}
