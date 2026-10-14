#include "TestSupport.h"
#include "NativeInputRouter.h"
#include "SnapshotHeartbeat.h"
#include <limits>
#include <set>

namespace iw::overlay::tests {
namespace {
struct Keys {
    runtime::NativeInputRouter router{ runtime::ParseBindings(BindingsJson().dump()) };
    std::set<int> held;
    bool allowed = true;
    bool open = false;
    bool pending = false;
    int reads = 0;
    std::uint64_t now = 0;
    std::optional<runtime::Input> Poll() {
        return router.Poll(allowed, open, pending, [&](int key) { ++reads; return held.count(key) != 0; }, now);
    }
};
void None(const std::optional<runtime::Input>& input) { Check(!input, "Unexpected input"); }
void One(runtime::Input expected, const std::optional<runtime::Input>& input) {
    Check(input && *input == expected, "Input did not match the expected command");
}
}

void InputStartupAndClosedCombination() {
    Keys keys;
    keys.held = {2, 86};
    None(keys.Poll());
    None(keys.Poll());
    keys.held = {2};
    None(keys.Poll());
    keys.held = {2, 86};
    One(runtime::Input::Toggle, keys.Poll());
    None(keys.Poll());
    keys.held = {};
    None(keys.Poll());
    keys.held = {86};
    None(keys.Poll());
    keys.held = {86, 2};
    // Adding aim while melee is already held cannot create a fresh opening
    None(keys.Poll());
    keys.held = {2};
    None(keys.Poll());
    keys.held = {2, 6};
    One(runtime::Input::Toggle, keys.Poll());
}

void InputFocusAndMenuReseeding() {
    Keys keys;
    None(keys.Poll());
    int reads = keys.reads;
    keys.allowed = false;
    keys.held = {2, 86};
    None(keys.Poll());
    Check(keys.reads == reads, "Focus loss still queried keys");
    keys.allowed = true;
    None(keys.Poll());
    None(keys.Poll());
    keys.open = true;
    None(keys.Poll());
    None(keys.Poll());
    keys.held = {};
    None(keys.Poll());
    keys.held = {86};
    One(runtime::Input::Back, keys.Poll());
    keys.open = false;
    None(keys.Poll());
    keys.held = {2, 86};
    None(keys.Poll());
}

void InputPriorityAndHeldKeys() {
    // Neutral scrolling allows Use while a single direction still precedes knife
    const std::set<int> combinations[] = { {86, 2, 1, 70}, {2, 1, 70}, {1, 70}, {70}, {2, 86}, {86} };
    const runtime::Input expected[] = { runtime::Input::Select, runtime::Input::Select,
        runtime::Input::Down, runtime::Input::Select, runtime::Input::Up, runtime::Input::Back };
    for (std::size_t index = 0; index < std::size(combinations); ++index) {
        Keys keys;
        keys.open = true;
        None(keys.Poll());
        keys.held = combinations[index];
        One(expected[index], keys.Poll());
        // Other simultaneous edges are consumed and holding does not repeat an action
        None(keys.Poll());
        None(keys.Poll());
        keys.held = {};
        None(keys.Poll());
        // Fresh Select follows the completed scroll wait even after its direction was released
        keys.now = 195;
        keys.held = {70};
        One(runtime::Input::Select, keys.Poll());
    }
}

void InputPendingDropsEdges() {
    Keys keys;
    keys.open = true;
    None(keys.Poll());
    keys.pending = true;
    keys.held = {86, 2, 1, 70};
    None(keys.Poll());
    keys.pending = false;
    None(keys.Poll());
    keys.held = {};
    None(keys.Poll());
    keys.held = {70};
    One(runtime::Input::Select, keys.Poll());
}

void BindingsRejectAmbiguityAndInvalidKeys() {
    const auto valid = runtime::ParseBindings(BindingsJson().dump());
    runtime::ValidateBindings(valid);
    for (int variation = 0; variation < 6; ++variation) {
        auto bindings = valid;
        if (variation == 0) bindings.aim.clear();
        if (variation == 1) bindings.melee = {86, 86};
        if (variation == 2) bindings.select = {0};
        if (variation == 3) bindings.select = {255};
        if (variation == 4) bindings.down = {2};
        if (variation == 5) bindings.melee = {70};
        Reject([&] { runtime::NativeInputRouter rejected(bindings); });
    }
}

void AckGateKeepsOneInflightAndRejectsRollback() {
    runtime::CommandGate gate(0);
    Check(gate.NextSequence() == 1, "Initial sequence is incorrect");
    gate.MarkSent(1);
    Check(gate.Pending(), "A sent command did not become pending");
    Reject([&] { gate.NextSequence(); });
    gate.ObserveAck(0);
    Check(gate.Pending(), "An old ack released the pending command");
    gate.ObserveAck(1);
    Check(!gate.Pending() && gate.NextSequence() == 2, "Matching ack did not release the command");
    gate.ObserveAck(3);
    Check(gate.NextSequence() == 4, "A higher acknowledged sequence was not retained");
    Reject([&] { gate.ObserveAck(2); });
    Reject([&] { gate.MarkSent(5); });
    runtime::CommandGate exhausted(std::numeric_limits<unsigned long long>::max());
    Reject([&] { exhausted.NextSequence(); });
}

void HeartbeatExpiresRecoversAndRejectsRollback() {
    runtime::SnapshotHeartbeat heartbeat(1, 1000);
    Check(heartbeat.Observe(1, 4000), "Heartbeat expired before the allowed boundary");
    Check(!heartbeat.Observe(1, 4001), "Stale generation was considered fresh");
    Check(heartbeat.Observe(2, 4002), "Fresh generation did not recover the heartbeat");
    Check(heartbeat.Observe(2, 7002), "Recovered heartbeat lost its new start time");
    Reject([&] { heartbeat.Observe(1, 7003); });
    Reject([&] { heartbeat.Observe(2, 4001); });
}
}
