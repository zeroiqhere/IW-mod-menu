#include "InstructionColorTests.h"
#include "../../TestSupport.h"
namespace iw::overlay::tests::instructions {
namespace {
Json Packet() {
    auto packet = StateJson();
    packet["localMenuEligibility"] = {{"revision",7},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    packet["instructionHud"] = {{"exists",true},{"creationRevision",2},{"admissionRevision",7},
        {"creationDesign","AIO"},{"creationAlternate",false},{"creationBackground",true},
        {"creationAccent","#3998fe"},{"creationRainbow",false}};
    return packet;
}
auto Read(const Json& packet) { return runtime::ParseState(packet.dump(),GetCurrentProcessId()).snapshot; }
}
void InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState() {
    for (bool open : {false,true}) {
        auto packet = Packet(); packet["isOpen"] = open;
        const auto state = Read(packet);
        Check(state->instructionHud->creationAccent == "#3998FE" && state->instructionHud->creationRainbow == false,
            "Owned fixed creation facts were not copied and normalized");
        Check(state->accent == "#3998FE" && state->rows[0].value == "OFF" && state->isOpen == open,
            "Captured creation facts changed current authority");
        packet["instructionHud"]["creationAccent"] = "#FF0000";
        Check(state->instructionHud->creationAccent == "#3998FE","Published creation facts borrowed mutable JSON");
        packet["instructionHud"]["creationRainbow"] = true;
        Check(Read(packet)->instructionHud->creationRainbow == true,"Rainbow creation mode was lost");
        packet["instructionHud"]["creationAccent"] = nullptr; packet["instructionHud"]["creationRainbow"] = nullptr;
        Check(!Read(packet)->instructionHud->creationAccent && !Read(packet)->instructionHud->creationRainbow,
            "Null legacy pair invented captured colors");
        packet["instructionHud"].erase("creationAccent"); packet["instructionHud"].erase("creationRainbow");
        Check(!Read(packet)->instructionHud->creationAccent,"Six-field legacy HUD lost compatibility");
    }
}
void InstructionColorRejectsMalformedPairsAndDecodedDuplicates() {
    // A valid complete packet must pass before any negative mutation
    Check(Read(Packet())->instructionHud->exists,"Color test baseline is invalid");
    for (const Json& color : {Json("3998FE"),Json("#3998FG"),Json("#123"),Json("#1234567"),Json(1),Json(true),Json(nullptr)}) {
        auto packet=Packet(); packet["instructionHud"]["creationAccent"]=color;
        Reject([&]{Read(packet);});
    }
    for (const Json& mode : {Json(0),Json("false"),Json(nullptr)}) {
        auto packet=Packet(); packet["instructionHud"]["creationRainbow"]=mode;
        Reject([&]{Read(packet);});
    }
    for (const auto* missing : {"creationAccent","creationRainbow"}) {
        auto packet=Packet();packet["instructionHud"].erase(missing);Reject([&]{Read(packet);});
    }
    auto packet=Packet();packet["instructionHud"]["exists"]=false;
    for (const auto* key : {"creationRevision","admissionRevision","creationDesign","creationAlternate","creationBackground"})
        packet["instructionHud"][key]=nullptr;
    Reject([&]{Read(packet);});
    packet["instructionHud"]["creationAccent"]=nullptr;packet["instructionHud"]["creationRainbow"]=nullptr;
    Check(!Read(packet)->instructionHud->exists,"Explicit absent null pair was rejected");
    auto raw=Packet().dump();const auto at=raw.find("\"creationAccent\":");
    raw.insert(at,"\"creation\\u0041ccent\":\"#000000\",");
    Check(raw.find("\\u0041")!=std::string::npos,"Duplicate test did not contain a decoded JSON escape");
    Reject([&]{runtime::ParseState(raw,GetCurrentProcessId());});
}
}
