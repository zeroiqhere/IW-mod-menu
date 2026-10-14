#include "TestSupport.h"
#include "RuntimeStartup.h"
#include "OpeningPolicyScope.h"
#include "scroll/SpacerScrollReceiptTracker.h"
#include <limits>

namespace iw::overlay::tests {
namespace {
Json PairPacket() {
    auto packet = StateJson();
    packet["mainOpeningControls"] = {{"revision", 3u}, {"actions", {5, 4}}};
    packet["openingPolicies"] = {{"revision", 7u}, {"main", packet["mainOpeningControls"]},
        {"quick", {{"revision", 2u}, {"actions", {5, 10}}}}, {"quickDisabled", false}};
    return packet;
}
runtime::ParsedState ParsePair(const Json &packet, const runtime::OpeningActionLabels *labels = nullptr) {
    return runtime::ParseState(packet.dump(), GetCurrentProcessId(), nullptr, nullptr, nullptr,
                               nullptr, nullptr, 8, nullptr, nullptr, labels);
}
}
void OpeningPoliciesOwnIndependentEpochsAndPreserveLegacyMain() {
    auto packet = PairPacket();
    auto parsed = ParsePair(packet);
    Check(parsed.snapshot->openingPolicies && parsed.snapshot->openingPolicies->revision == 7 &&
              parsed.snapshot->openingPolicies->main.revision == 3 &&
              parsed.snapshot->openingPolicies->quick.revision == 2 &&
              parsed.snapshot->openingPolicies->quick.actions == std::vector<OpeningAction>{OpeningAction::Aim, OpeningAction::SecondaryOffhand},
          "Parser did not own both policies and their independent epochs");
    packet["openingPolicies"]["quick"]["actions"] = {12};
    Check(parsed.snapshot->openingPolicies->quick.actions.size() == 2, "Caller mutation changed an owned pair");
    packet.erase("openingPolicies");
    Check(!ParsePair(packet).snapshot->openingPolicies && ParsePair(packet).snapshot->mainOpeningControls,
          "Legacy Main-only packet lost its policy");
    packet.erase("mainOpeningControls");
    Check(!ParsePair(packet).snapshot->mainOpeningControls, "Absent legacy metadata fabricated a tuple");
    packet = PairPacket();
    packet["openingPolicies"]["revision"] = std::numeric_limits<std::uint64_t>::max();
    packet["openingPolicies"]["quick"]["revision"] = std::numeric_limits<std::uint64_t>::max();
    Check(ParsePair(packet).snapshot->openingPolicies->revision == std::numeric_limits<std::uint64_t>::max(),
          "A valid unsigned policy boundary was narrowed");
}
void OpeningPoliciesRejectExactFieldsTypesBoundsAndDecodedDuplicates() {
    for (int malformed = 0; malformed < 23; ++malformed) {
        auto packet = PairPacket(); auto &pair = packet["openingPolicies"];
        if (malformed == 0) pair = nullptr;
        if (malformed == 1) pair = Json::array();
        if (malformed == 2) pair.erase("quick");
        if (malformed == 3) pair["extra"] = true;
        if (malformed == 4) pair["revision"] = 0;
        if (malformed == 5) pair["revision"] = -1;
        if (malformed == 6) pair["revision"] = 1.0;
        if (malformed == 7) pair["revision"] = "7";
        if (malformed == 8) pair["quickDisabled"] = nullptr;
        if (malformed == 9) pair["quickDisabled"] = 0;
        if (malformed == 10) pair["quick"] = nullptr;
        if (malformed == 11) pair["quick"]["actions"] = {4, 4};
        if (malformed == 12) pair["quick"]["actions"] = {9, 10};
        if (malformed == 13) pair["quick"]["actions"] = {13};
        if (malformed == 14) pair["quick"]["actions"] = {true};
        if (malformed == 15) pair["quick"]["actions"] = {1, 2, 3, 4};
        if (malformed == 16) pair["quick"]["revision"] = 0;
        if (malformed == 17) pair["main"] = nullptr;
        if (malformed == 18) pair["quick"]["actions"] = {5.0};
        if (malformed == 19) pair["quick"]["actions"] = Json::array();
        if (malformed == 20) pair["quick"]["extra"] = false;
        if (malformed == 21) pair.erase("quickDisabled");
        if (malformed == 22) pair["quick"]["revision"] = true;
        Reject([&] { ParsePair(packet); });
    }
    auto bytes = PairPacket().dump();
    const auto position = bytes.find("\"quickDisabled\":false");
    bytes.replace(position, std::string("\"quickDisabled\":false").size(),
                  "\"quickDisabled\":false,\"quick\\u0044isabled\":true");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
    bytes = PairPacket().dump();
    const auto revision = bytes.find("\"revision\":7");
    bytes.replace(revision, std::string("\"revision\":7").size(), "\"revision\":18446744073709551616");
    Reject([&] { runtime::ParseState(bytes, GetCurrentProcessId()); });
}
void OpeningPoliciesRequireExactMainMirrorButAllowCrossFamilyOverlap() {
    for (int mismatch = 0; mismatch < 4; ++mismatch) {
        auto packet = PairPacket();
        if (mismatch == 0) packet.erase("mainOpeningControls");
        if (mismatch == 1) packet["mainOpeningControls"] = nullptr;
        if (mismatch == 2) packet["mainOpeningControls"]["revision"] = 4;
        if (mismatch == 3) packet["mainOpeningControls"]["actions"] = {4, 5};
        Reject([&] { ParsePair(packet); });
    }
    auto packet = PairPacket(); packet["openingPolicies"]["quick"] = packet["mainOpeningControls"];
    const auto parsed = ParsePair(packet);
    Check(parsed.snapshot->openingPolicies && parsed.snapshot->openingPolicies->quick == *parsed.snapshot->mainOpeningControls,
          "Cross-family tuple overlap was rejected");
}
void OpeningPoliciesStartupRejectsUnverifiedQuickEvenClosedOrDisabled() {
    auto packet = PairPacket();
    for (bool open : {false, true}) for (bool disabled : {false, true}) {
        packet["isOpen"] = open; packet["openingPolicies"]["quickDisabled"] = disabled;
        Reject([&] { runtime::PrepareRuntimeStartup(packet.dump(), BindingsJson().dump(), GetCurrentProcessId()); });
    }
    // The same startup path must also accept the explicitly verified saved group
    auto bindings = BindingsJson(); bindings["secondaryOffhand"] = {69};
    const auto prepared = runtime::PrepareRuntimeStartup(packet.dump(), bindings.dump(), GetCurrentProcessId());
    Check(prepared.state.snapshot->openingPolicies && prepared.bindings.secondaryOffhand == std::vector<int>{69},
          "Startup failed to own verified policy and bindings together");
}
void OpeningPoliciesCommandsCaptureObservedGlobalEpochWithoutQuickActivation() {
    const auto parsed = ParsePair(PairPacket());
    const auto epoch = runtime::ObservedOpeningControlsRevision(*parsed.snapshot);
    Check(epoch == 7, "Command capture used a tuple epoch instead of the global epoch");
    const auto command = Json::parse(runtime::CommandJson(GetCurrentProcessId(), 1, runtime::Input::Select, 11, epoch));
    Check(command.at("openingControlsRevision") == 7 && command.at("navigationRevision") == 11,
          "Command serialization did not preserve its observed scopes");
    auto legacy = *parsed.snapshot; legacy.openingPolicies.reset();
    Check(runtime::ObservedOpeningControlsRevision(legacy) == 3, "Legacy command scope changed");
    // A fresh pair-only snapshot keeps the missing mirror explicit without destroying an optional payload
    MenuSnapshot malformed{};
    malformed.openingPolicies = parsed.snapshot->openingPolicies;
    Check(malformed.openingPolicies && !malformed.mainOpeningControls, "Missing-mirror fixture lost its invalid owner shape");
    Reject([&] { runtime::ObservedOpeningControlsRevision(malformed); });
    auto fake = PairPacket(); fake["spacerScrollReceipt"] = {{"sequence", 1}, {"sourceOpeningPolicies", fake["openingPolicies"]}};
    Check(!ParsePair(fake).snapshot->spacerScrollReceipt, "Wire data fabricated private policy history");
}
void OpeningPoliciesReceiptsRetireQuickOnlyGlobalAndDisableChanges() {
    auto source = *ParsePair(PairPacket()).snapshot;
    Check(source.openingPolicies.has_value(), "Pair was absent before private capture");
    source.isOpen = true; source.isRootMenu = false; source.menuRevision = 6; source.navigationRevision = 10; source.selectedIndex = 1;
    source.rows.assign(16, {"Ordinary", "OFF", true}); source.rows[2] = {"", "", false, true};
    // Quick-only and preference changes retire receipts even though Main remains identical
    for (int change = 0; change < 4; ++change) {
        runtime::scroll::SpacerScrollReceiptTracker tracker;
        Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Valid dual source capture failed");
        auto target = source; target.selectedIndex = 3; ++target.navigationRevision;
        if (change == 0) ++target.openingPolicies->revision;
        if (change == 1) ++target.openingPolicies->quick.revision;
        if (change == 2) target.openingPolicies->quickMenuDisabled = true;
        if (change == 3) target.openingPolicies->quick.actions = {OpeningAction::Jump};
        Check(!tracker.Observe(target, 11, 120), "Receipt used stale Quick or global policy history");
    }
    runtime::scroll::SpacerScrollReceiptTracker tracker;
    Check(tracker.Capture(source, SpacerScrollDirection::Down, 11, 100), "Capture failed");
    auto target = source; target.selectedIndex = 3; ++target.navigationRevision;
    const auto receipt = tracker.Observe(target, 11, 120);
    Check(receipt && receipt->sourceOpeningPolicies == source.openingPolicies, "Receipt did not copy dual source identity");
}
void SelectorRefreshPreservesOpeningOnlyForExactOpenControlsContext() {
    auto packet = PairPacket();
    packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["rows"] = {{{"label", "Menu"}, {"value", "Main Menu"}, {"enabled", true}},
        {{"label", "Bind Slot"}, {"value", "1"}, {"enabled", true}},
        {{"label", ""}, {"value", ""}, {"enabled", false}, {"isSpacer", true}},
        {{"label", "Aim"}, {"value", "ON"}, {"enabled", true}}};
    // The complete source catalog carries typed actions even with remapped captions
    packet["rows"].erase(packet["rows"].begin() + 3, packet["rows"].end());
    for (unsigned id = 0; id < OpeningActionCount; ++id)
        packet["rows"].push_back({{"label", OpeningActionCaption(static_cast<OpeningAction>(id))},
            {"value", "OFF"}, {"enabled", true}, {"openingAction", id}});
    packet["menuPreferenceRefresh"] = {{"phase", "selector"}};
    packet["menuOpening"] = {{"revision", 1u}, {"elapsedMilliseconds", 100}};
    auto truncated = packet; truncated.erase("menuOpening");
    truncated["rows"].erase(truncated["rows"].begin() + 3, truncated["rows"].end());
    Reject([&] { ParsePair(truncated); });
    const auto parsed = ParsePair(packet);
    Check(parsed.snapshot->menuOpening && runtime::InputModeForSnapshot(*parsed.snapshot) == runtime::InputMode::MenuLocked,
          "Selector cooldown discarded opening or admitted controls");
    for (int invalid = 0; invalid < 7; ++invalid) {
        auto bad = packet;
        if (invalid == 0) bad["menuPreferenceRefresh"]["phase"] = "cooldown";
        if (invalid == 1) { bad.erase("openingPolicies"); bad.erase("mainOpeningControls"); }
        if (invalid == 2) bad["selectedIndex"] = 1;
        if (invalid == 3) bad["rows"][0]["label"] = "Preferences";
        if (invalid == 4) bad["rows"][1]["label"] = "Slot";
        if (invalid == 5) bad["rows"][2]["isSpacer"] = false;
        if (invalid == 6) bad["isRootMenu"] = true;
        Reject([&] { ParsePair(bad); });
    }
}
} // namespace iw::overlay::tests
