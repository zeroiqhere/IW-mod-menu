#include "../TestSupport.h"
#include "ScopedOpeningExpected.h"
#include "../../native-layout/BackendPreview.h"
#include <fstream>

namespace iw::overlay::tests {
void BackendLocalEligibilityPreviewMatchesActualScopedFamiliesAndNullableFacts(const std::filesystem::path& directory) {
    if (!std::filesystem::exists(directory / "opening-closed.json")) return;
    for (const auto& expected : opening::Profiles) {
        const auto path = directory / expected.file;
        if (!expected.hasActorEligibility && !std::filesystem::exists(path)) continue;
        std::ifstream file(path, std::ios::binary);
        const std::string bytes{std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>()};
        const auto parsed = runtime::ParseState(bytes, 42).snapshot;
        const auto preview = ReadBackendPreview(path);
        Check(preview.localMenuEligibility.has_value() == expected.hasActorEligibility
            && preview.menuSurface == expected.surface && preview.localMenuEligibility == parsed->localMenuEligibility
            && preview.menuOpenTransition == expected.transition && preview.menuOpening == expected.opening
            && preview.navigationRevision == expected.navigation && preview.menuRevision == expected.menu
            && preview.isOpen == expected.open && preview.selectedIndex == expected.selected && preview.rows.size() == expected.rows,
            "Preview disagrees with the actual scoped opening producer or native parser");
    }
}
}
