#include "InstructionColorTests.h"
#include "../../TestSupport.h"
#include "../../../native-layout/BackendPreview.h"
#include <fstream>
#include <random>
namespace iw::overlay::tests::instructions {
namespace {
class ColorPreview final {
public:
    ColorPreview() {
        std::random_device random;
        for(unsigned attempt=0;attempt<32;++attempt) {
            auto candidate=std::filesystem::temp_directory_path()/("iw-color-preview-"+std::to_string(random()));
            if(std::filesystem::create_directory(candidate)) {
                directory_=std::move(candidate);std::filesystem::permissions(directory_,std::filesystem::perms::owner_all);return;
            }
        }
        throw std::runtime_error("Could not reserve color preview directory");
    }
    ~ColorPreview() { std::error_code ignored;std::filesystem::remove_all(directory_,ignored); }
    MenuSnapshot Read(const std::string& bytes) {
        const auto path=directory_/"packet.json";
        { std::ofstream file(path,std::ios::binary);file<<bytes;if(!file) throw std::runtime_error("Could not write color preview"); }
        return ReadBackendPreview(path);
    }
private:
    std::filesystem::path directory_;
};
}
void InstructionColorPreviewMatchesLegacyPairsAndStrictValidation() {
    ColorPreview preview;auto packet=StateJson();
    packet["localMenuEligibility"]={{"revision",7},{"life","alive"},{"hasMenu",true},{"controlsDisabled",false},{"blocked",false}};
    packet["instructionHud"]={{"exists",true},{"creationRevision",2},{"admissionRevision",7},
        {"creationDesign","AIO"},{"creationAlternate",false},{"creationBackground",true},
        {"creationAccent","#3998fe"},{"creationRainbow",true}};
    for(bool open:{false,true}) {
        packet["isOpen"]=open;const auto wire=packet.dump();
        const auto parsed=runtime::ParseState(wire,GetCurrentProcessId()).snapshot;
        const auto read=preview.Read(wire);
        Check(read.instructionHud->creationAccent=="#3998FE" && read.instructionHud->creationRainbow==true
            && read.instructionHud->creationAccent==parsed->instructionHud->creationAccent
            && read.rows[0].value==parsed->rows[0].value,"Preview lost paired color or changed current row data");
    }
    for(const Json& color:{Json("#XYZXYZ"),Json(1),Json(false),Json(nullptr)}) {
        auto bad=packet;bad["instructionHud"]["creationAccent"]=color;Reject([&]{preview.Read(bad.dump());});
    }
    for(const Json& mode:{Json("true"),Json(1),Json(nullptr)}) {
        auto bad=packet;bad["instructionHud"]["creationRainbow"]=mode;Reject([&]{preview.Read(bad.dump());});
    }
    for(const auto* key:{"creationAccent","creationRainbow"}) {
        auto bad=packet;bad["instructionHud"].erase(key);Reject([&]{preview.Read(bad.dump());});
    }
    auto raw=packet.dump();const auto at=raw.find("\"creationRainbow\":");
    raw.insert(at,"\"creation\\u0052ainbow\":false,");
    Check(raw.find("\\u0052")!=std::string::npos,"Preview duplicate test lost the JSON escape");
    Reject([&]{preview.Read(raw);});
    packet["instructionHud"]["creationAccent"]=nullptr;packet["instructionHud"]["creationRainbow"]=nullptr;
    Check(!preview.Read(packet.dump()).instructionHud->creationAccent,"Null pair lost legacy compatibility");
    packet["instructionHud"].erase("creationAccent");packet["instructionHud"].erase("creationRainbow");
    Check(!preview.Read(packet.dump()).instructionHud->creationRainbow,"Six-field preview invented creation mode");
}
}
