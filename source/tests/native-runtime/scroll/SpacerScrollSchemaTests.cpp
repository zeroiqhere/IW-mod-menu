#include "SpacerScrollTestModels.h"
namespace iw::overlay::tests {
using namespace runtime::scroll;
namespace {
Json SourcePacket() {
    auto packet = StateJson();
    packet["isOpen"] = true;
    packet["isRootMenu"] = false;
    packet["navigationRevision"] = 10u;
    packet["menuRevision"] = 6u;
    packet["selectedIndex"] = 1;
    packet["lastSequence"] = 10u;
    packet["rows"] = Json::array();
    for (int index = 0; index < 16; ++index) {
        packet["rows"].push_back({{"label", "Ordinary"}, {"value", "OFF"}, {"enabled", true}});
    }
    packet["rows"][2] = {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}};
    return packet;
}
runtime::ParsedState Parse(const Json &packet, const SpacerScrollObservation *observation = nullptr,
                           std::optional<std::uint64_t> focus = 8) {
    return runtime::ParseState(packet.dump(), GetCurrentProcessId(), nullptr, nullptr, nullptr,
                               nullptr, nullptr, focus, nullptr, nullptr, nullptr, observation);
}
}
void SpacerSchemaStampsExactPrivateAckBeforeReturningImmutableSnapshot() {
    auto packet = SourcePacket();
    const auto source = Parse(packet);
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(*source.snapshot, SpacerScrollDirection::Down, 11, 100), "Parser source capture failed");
    packet["selectedIndex"] = 3;
    packet["navigationRevision"] = 11u;
    packet["lastSequence"] = 11u;
    const SpacerScrollObservation observation{&tracker, 120};
    const auto target = Parse(packet, &observation);
    Check(target.snapshot->spacerScrollReceipt && target.lastSequence == 11 &&
              target.snapshot->spacerScrollReceipt->observedMilliseconds == 120 &&
              target.snapshot->spacerScrollReceipt->plan.targetIndex == 3,
          "Private parser observation was not stamped before immutable publication");
    Check(!source.snapshot->spacerScrollReceipt && source.snapshot->selectedIndex == 1,
          "Private stamping mutated a previously returned source");
}
void SpacerSchemaCannotTrustReceiptJsonOrWrongAckAndFocus() {
    auto packet = SourcePacket();
    packet["spacerScrollReceipt"] = {{"sequence", 11u}, {"branch", "StableWindow"}};
    Check(!Parse(packet).snapshot->spacerScrollReceipt, "JSON fabricated private presentation history");
    const auto source = Parse(packet);
    for (int mismatch = 0; mismatch < 3; ++mismatch) {
        SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(*source.snapshot, SpacerScrollDirection::Down, 11, 100), "Capture failed");
        auto target = packet;
        target["selectedIndex"] = 3;
        target["navigationRevision"] = 11u;
        target["lastSequence"] = mismatch == 0 ? 12u : 11u;
        const SpacerScrollObservation observation{&tracker, 120};
        const auto parsed = Parse(target, &observation, mismatch == 1 ? std::nullopt :
                                   std::optional<std::uint64_t>(mismatch == 2 ? 9 : 8));
        Check(!parsed.snapshot->spacerScrollReceipt, "Wrong raw ACK or private focus promoted a receipt");
    }
}
void SpacerSchemaFailureCannotPublishReceiptAndRetryKeepsDispatchTime() {
    auto packet = SourcePacket();
    const auto source = Parse(packet);
    SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(*source.snapshot, SpacerScrollDirection::Down, 11, 100), "Capture failed");
    packet["selectedIndex"] = 3;
    packet["navigationRevision"] = 11u;
    packet["lastSequence"] = 11u;
    const SpacerScrollObservation observation{&tracker, 120};
    auto malformed = packet;
    malformed["generation"] = "bad";
    Reject([&] { Parse(malformed, &observation); });
    const auto retried = Parse(packet, &observation);
    Check(retried.snapshot->spacerScrollReceipt &&
              retried.snapshot->spacerScrollReceipt->issuedMilliseconds == 100,
          "A failed publication retry restarted or lost its private event");
    const auto immutable = retried.snapshot;
    const SpacerScrollObservation later{&tracker, 145};
    const auto refreshed = Parse(packet, &later);
    Check(immutable->spacerScrollReceipt->observedMilliseconds == 120 &&
              refreshed.snapshot->spacerScrollReceipt->observedMilliseconds == 145,
          "A later report mutated an already owned immutable snapshot");
}
} // namespace iw::overlay::tests
