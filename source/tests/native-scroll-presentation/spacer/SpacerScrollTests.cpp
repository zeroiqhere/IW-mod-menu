#include "SpacerScrollTests.h"
#include "../../../native/render/ScrollPresentation.h"
#include <algorithm>
#include <cmath>
#include <stdexcept>

namespace {
using namespace iw::overlay;
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
void Equal(float actual, float expected) {
    Require(std::abs(actual - expected) < .001f, "Spacer presentation differs from the source phase");
}
const ScrollRow& Row(const ScrollFrame& frame, std::size_t index) {
    for (std::size_t i = 0; i < frame.count; ++i)
        if (frame.rows[i].index == index) return frame.rows[i];
    throw std::runtime_error("The expected current row or retiring edge is missing");
}
struct Rig {
    ScrollPresentation owner;
    ScrollRequest source, target;
    SpacerScrollReceipt receipt;
    explicit Rig(std::size_t capacity = 10, bool down = true, unsigned blanks = 1) {
        // Production spacer paths are nonwrapped and begin with a confirmed selectable row
        source.active = true;
        source.menuRevision = 9;
        source.focusRevision = 4;
        source.navigationRevision = 20;
        source.rowCount = 14;
        source.shown = capacity;
        source.layout = {640, 480, 260, static_cast<float>(capacity * 18), capacity};
        if (capacity == 5) {
            source.layout.booleanDisplay = 1;
            receipt.sourceAppearance.stealthUI = true;
            receipt.sourceAppearance.booleanDisplay = "Text";
        }
        if (capacity == 8) receipt.sourceAppearance.optionCounter = true;
        source.selected = down ? 1 : 3;
        source.first = !down && capacity == 5 ? 1 : 0;
        source.topology.rowCount = 14;
        for (unsigned i = 0; i < blanks; ++i) source.topology.spacerMask[0] |= 1ULL << (2 + i);
        auto& plan = receipt.plan;
        plan.direction = down ? SpacerScrollDirection::Down : SpacerScrollDirection::Up;
        plan.branch = capacity != 5 ? SpacerScrollBranch::StableWindow
            : down ? SpacerScrollBranch::FinalWindowShift : SpacerScrollBranch::InitialWindowShiftThenRebuild;
        plan.fromIndex = static_cast<unsigned>(source.selected);
        plan.spacerIndex = 2;
        plan.targetIndex = down ? 2 + blanks : 1;
        plan.spacerCount = blanks;
        plan.capacity = static_cast<unsigned>(capacity);
        plan.sourceFirst = static_cast<unsigned>(source.first);
        plan.spacerFirst = 0;
        plan.targetFirst = down && capacity == 5 ? 1 : 0;
        plan.topology = source.topology;
        receipt.sequence = 7;
        receipt.sourceNavigationRevision = 20;
        receipt.targetNavigationRevision = 21;
        receipt.menuRevision = 9;
        receipt.focusRevision = 4;
        receipt.issuedMilliseconds = 1000;
        receipt.observedMilliseconds = 1020;
        target = source;
        target.selected = plan.targetIndex;
        target.first = plan.targetFirst;
        target.navigationRevision = 21;
        target.spacerReceipt = &receipt;
        owner.Update(source, 990);
    }
    ScrollFrame At(std::uint64_t time) { return owner.Update(target, time); }
};
}
void AddSpacerScrollChecks(std::vector<std::pair<std::string, std::function<void()>>>& checks) {
    checks.emplace_back("Verified stable spacer receipt holds old paint before its final fade", [] {
        Rig rig;
        const auto hold = rig.At(1020);
        Require(hold.selected == 3, "Presentation changed the authoritative action selection");
        Equal(hold.scroller, 1);
        Equal(Row(hold, 1).selection, 1);
        Equal(Row(hold, 3).selection, 0);
        Equal(rig.At(1060).scroller, 1);
        Equal(rig.At(1120).scroller, 2);
        const auto done = rig.At(1180);
        Equal(done.scroller, 3);
        Require(!done.transitioning, "The completed spacer fade stayed active");
    });
    checks.emplace_back("Final window shift begins only after the unchanged blank half wait", [] {
        Rig rig(5);
        const auto hold = rig.At(1020);
        Equal(hold.scroller, 1);
        Equal(Row(hold, 1).position, 1);
        Equal(Row(hold, 5).visibility, 0);
        const auto half = rig.At(1120);
        Equal(half.scroller, 1.5f);
        Equal(Row(half, 1).position, .5f);
        Equal(Row(half, 5).visibility, .5f);
        Require(Row(half, 0).outgoing, "The retiring source edge disappeared before its fade ended");
    });
    checks.emplace_back("Initial window shift keeps old styles and bar then rebuilds with its detached ghost", [] {
        Rig rig(5, false);
        const auto first = rig.At(1030);
        Equal(first.scroller, 2);
        Equal(Row(first, 3).position, 2.25f);
        Equal(Row(first, 3).selection, 1);
        Equal(Row(first, 1).selection, 0);
        Equal(Row(first, 5).position, 4);
        Equal(Row(first, 5).visibility, .75f);
        const auto rebuilt = rig.At(1060);
        Equal(rebuilt.scroller, 1);
        Equal(Row(rebuilt, 3).position, 3);
        Equal(Row(rebuilt, 3).selection, 0);
        Equal(Row(rebuilt, 1).selection, 1);
        Equal(Row(rebuilt, 5).visibility, .5f);
        Require(Row(rebuilt, 5).outgoing, "Rebuild deleted the detached source edge too early");
        Require(rig.At(1120).count == 5, "A completed detached edge remained visible");
    });
    checks.emplace_back("Counter paint remains old during the half wait without changing action selection", [] {
        Rig rig(8);
        const auto hold = rig.At(1059);
        Require(hold.counterSelection == 1 && hold.selected == 3, "Hold counter replaced confirmed target selection");
        Require(rig.At(1060).counterSelection == 3, "Counter did not update at the final source style loop");
    });
    checks.emplace_back("Stable multiple blanks each contribute a half wait before one final fade", [] {
        Rig rig(10, true, 2);
        Equal(rig.At(1119).scroller, 1);
        Equal(rig.At(1120).scroller, 1);
        Equal(rig.At(1180).scroller, 2.5f);
        Require(!rig.At(1240).transitioning, "Multiple blanks stretched the final fade");
    });
    checks.emplace_back("Late receipt observation samples dispatch time and heartbeats cannot restart it", [] {
        Rig rig;
        rig.receipt.observedMilliseconds = 1120;
        Equal(rig.At(1120).scroller, 2);
        Equal(rig.At(1140).scroller, 1 + 2 * (80.0f / 120));
        Equal(rig.At(1140).scroller, 1 + 2 * (80.0f / 120));
        Equal(rig.At(1180).scroller, 3);
    });
    checks.emplace_back("First observed target and expired receipts never invent earlier paint", [] {
        Rig rig;
        rig.owner.Reset();
        const auto initial = rig.At(1020);
        Require(!initial.transitioning && !initial.counterSelection, "First target observation fabricated source history");
        Rig lateSource;
        lateSource.owner.Reset();
        lateSource.owner.Update(lateSource.source, 1005);
        Require(!lateSource.At(1020).transitioning, "A source first observed after dispatch invented earlier history");
        Rig expired;
        const auto done = expired.At(1180);
        Require(!done.transitioning && !done.counterSelection, "Expired receipt fabricated a new fade");
    });
    checks.emplace_back("Receipt requires the exact previously observed source navigation and topology", [] {
        for (unsigned fault = 0; fault < 5; ++fault) {
            Rig rig;
            if (fault == 0) ++rig.receipt.sourceNavigationRevision;
            if (fault == 1) ++*rig.target.navigationRevision;
            if (fault == 2) rig.target.topology.spacerMask[0] = 0;
            if (fault == 3) rig.receipt.plan.targetIndex = 4;
            if (fault == 4) rig.receipt.issuedMilliseconds = 980;
            const auto rejected = rig.At(1020);
            Require(!rejected.transitioning && !rejected.counterSelection, "Foreign source or target admitted a spacer event");
        }
    });
    checks.emplace_back("Changed page focus layout or row structure drops a captured receipt", [] {
        for (unsigned fault = 0; fault < 6; ++fault) {
            Rig rig;
            rig.At(1020);
            if (fault == 0) ++*rig.target.menuRevision;
            if (fault == 1) ++*rig.target.focusRevision;
            if (fault == 2) rig.target.layout.bodyX += 1;
            if (fault == 3) ++rig.target.rowCount;
            if (fault == 4) rig.target.active = false;
            if (fault == 5) rig.target.spacerReceipt = nullptr;
            const auto reset = rig.At(1040);
            Require(!reset.transitioning && !reset.counterSelection, "Changed context retained private old counter paint");
        }
    });
    checks.emplace_back("Malformed branch timestamp and unsupported wrapping receipts fall back to a snap", [] {
        for (unsigned fault = 0; fault < 6; ++fault) {
            Rig rig;
            if (fault == 0) rig.receipt.plan.branch = SpacerScrollBranch::InitialWindowShiftThenRebuild;
            if (fault == 1) rig.receipt.plan.spacerCount = 0;
            if (fault == 2) rig.receipt.observedMilliseconds = 999;
            if (fault == 3) rig.receipt.sequence = 0;
            if (fault == 4) rig.receipt.plan.targetIndex = 0;
            if (fault == 5) rig.receipt.plan.topology.spacerMask[1] = 1ULL << 63;
            Require(!rig.At(1020).transitioning, "Malformed receipt animated an unsupported history");
        }
    });
    checks.emplace_back("Arbitrary jumps without a private receipt retain their existing immediate fallback", [] {
        Rig rig;
        rig.target.spacerReceipt = nullptr;
        const auto snap = rig.At(1020);
        Equal(snap.scroller, 3);
        Require(!snap.transitioning && !snap.counterSelection, "Legacy jump became a fabricated spacer path");
    });
    checks.emplace_back("Opening takeover preserves the previously rendered bounded alpha during the hold", [] {
        Rig rig;
        rig.owner.Reset();
        rig.source.opening = MenuOpening{1, 20};
        const auto opening = rig.owner.Update(rig.source, 990);
        const float visible = *Row(opening, 3).opacity;
        const auto hold = rig.At(1020);
        Require(*Row(hold, 3).opacity >= visible && *Row(hold, 3).opacity < .4f,
            "Spacer hold forced a partially opened source row fully visible");
        Equal(*Row(rig.At(1180), 3).opacity, 1);
    });
    checks.emplace_back("Clock rollback cancels a receipt before unsigned elapsed arithmetic", [] {
        Rig rig;
        rig.At(1050);
        Require(!rig.At(1040).transitioning, "Rollback retained a receipt clock");
        Require(!rig.At(1060).transitioning, "A canceled receipt resurrected after clock recovery");

        Rig directRig;
        spacer::Presentation direct;
        Require(direct.Admit(directRig.source, directRig.target, StaticScrollFrame(directRig.source), 990, 1020),
            "Direct helper could not admit a validated source history");
        Require(direct.Sample(directRig.target, 1050).transitioning, "Direct helper did not sample its active event");
        const auto rolledBack = direct.Sample(directRig.target, 1040);
        Require(!rolledBack.transitioning && !rolledBack.counterSelection && !direct.Active(),
            "Direct helper clock rollback emitted old receipt paint");
        Require(!direct.Sample(directRig.target, 1060).transitioning,
            "Direct helper resurrected after clock recovery");
    });

    checks.emplace_back("Old source reports rendered after dispatch still join previously observed history", [] {
        Rig rig;
        rig.owner.Update(rig.source, 1016);
        const auto joined = rig.At(1020);
        Require(joined.transitioning && joined.counterSelection == 1, "Post-dispatch old report blocked genuine observed history");
        Equal(joined.scroller, 1);
    });
    checks.emplace_back("Monotonic receipt reports continue one event and regressing reports cancel it", [] {
        Rig rig;
        rig.At(1020);
        rig.receipt.observedMilliseconds = 1040;
        Equal(rig.At(1040).scroller, 1);
        rig.receipt.observedMilliseconds = 1120;
        Equal(rig.At(1120).scroller, 2);
        rig.receipt.observedMilliseconds = 1100;
        Require(!rig.At(1140).transitioning, "Regressing receipt report retained prior paint");
    });

    checks.emplace_back("A future latest receipt report cannot move the presentation clock ahead", [] {
        Rig rig;
        rig.At(1020);
        rig.receipt.observedMilliseconds = 1040;
        const auto future = rig.At(1030);
        Require(!future.transitioning && !future.counterSelection, "Future receipt report entered current paint");
    });

    checks.emplace_back("Initial shift uses the blank window before rebuilding a different final window", [] {
        Rig rig(5);
        rig.owner.Reset();
        rig.source.selected = 2;
        rig.source.topology.spacerMask[0] = 1ULL << 3;
        rig.target = rig.source;
        rig.target.selected = 4;
        rig.target.first = 2;
        rig.target.navigationRevision = 21;
        auto& plan = rig.receipt.plan;
        plan.branch = SpacerScrollBranch::InitialWindowShiftThenRebuild;
        plan.fromIndex = 2;
        plan.spacerIndex = 3;
        plan.targetIndex = 4;
        plan.sourceFirst = 0;
        plan.spacerFirst = 1;
        plan.targetFirst = 2;
        plan.topology = rig.source.topology;
        rig.target.spacerReceipt = &rig.receipt;
        rig.owner.Update(rig.source, 990);

        const auto shifting = rig.At(1030);
        Equal(Row(shifting, 2).position, 1.75f);
        Equal(Row(shifting, 2).selection, 1);
        Equal(shifting.scroller, 2);
        Equal(Row(shifting, 5).position, 4);
        Require(shifting.count == 6 && Row(shifting, 0).outgoing,
            "Initial shift painted the final window or detached more than its first edge");
        Require(std::none_of(shifting.rows.begin(), shifting.rows.begin() + shifting.count,
            [](const auto& row) { return row.index == 6; }), "Final-only incoming row appeared before rebuild");

        const auto rebuilt = rig.At(1060);
        Equal(Row(rebuilt, 2).position, 0);
        Equal(Row(rebuilt, 4).selection, 1);
        Require(rebuilt.count == 6 && Row(rebuilt, 0).outgoing,
            "Rebuild retained an attached row as a second ghost");
        Require(std::none_of(rebuilt.rows.begin(), rebuilt.rows.begin() + rebuilt.count,
            [](const auto& row) { return row.index == 1; }), "Rebuild retained an attached source row");
    });

    checks.emplace_back("Multiple unchanged blank windows hold before their final shift for the captured duration", [] {
        for (const int duration : {100, 250}) {
            Rig rig(5);
            rig.owner.Reset();
            rig.source.selected = 0;
            rig.source.durationMilliseconds = duration;
            rig.source.topology.spacerMask[0] = (1ULL << 1) | (1ULL << 2);
            rig.target = rig.source;
            rig.target.selected = 3;
            rig.target.first = 1;
            rig.target.navigationRevision = 21;
            auto& plan = rig.receipt.plan;
            plan.branch = SpacerScrollBranch::FinalWindowShift;
            plan.fromIndex = 0;
            plan.spacerIndex = 1;
            plan.targetIndex = 3;
            plan.spacerCount = 2;
            plan.durationMilliseconds = duration;
            plan.sourceFirst = plan.spacerFirst = 0;
            plan.targetFirst = 1;
            plan.topology = rig.source.topology;
            rig.target.spacerReceipt = &rig.receipt;
            rig.owner.Update(rig.source, 990);

            Equal(rig.At(1000 + duration - 1).scroller, 0);
            const auto half = rig.At(1000 + duration + duration / 2);
            Equal(half.scroller, 1);
            Equal(Row(half, 5).visibility, .5f);
            Require(!rig.At(1000 + 2 * duration).transitioning,
                "A multiple-blank final shift stretched the captured source duration");
        }
    });

}
