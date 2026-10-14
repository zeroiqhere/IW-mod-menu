#pragma once
#include "../TestSupport.h"

namespace iw::overlay::tests::paint {
inline Json Packet() {
    auto packet = StateJson(); packet["isOpen"] = true; packet["isRootMenu"] = false;
    packet["menuRevision"] = 6;
    packet["rows"] = Json::array({{{"label", "God Mode"}, {"value", "OFF"}, {"enabled", true}},
        {{"label", "Second"}, {"value", "UNKNOWN"}, {"enabled", false}}});
    packet["menuBooleanPaint"] = {{"revision", 7}, {"rows", Json::array({{{"index", 0}, {"value", "ON"}}, {{"index", 1}, {"value", "OFF"}}})}};
    return packet;
}
void OwnsBooleanPaintWithoutChangingCurrentRowsOrInputMode();
void RejectsMalformedPaintFieldsBoundsAndDecodedDuplicates();
void RejectsInvalidPaintTargetsAndCompetingModalContexts();
void ResolvesUnknownAndLegacyValuesWithoutGrantingActions();
void PaintIdentityRetiresPrivateScrollReceipts();
void PreviewMatchesOwnedPaintAndRejectsMalformedSuppliedMetadata();
}
