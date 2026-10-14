#pragma once
#include "../TestSupport.h"

namespace iw::overlay::tests::callback {
inline Json Packet(const char* kind = "boolean", const char* phase = "pending") {
    auto packet = StateJson();
    packet["isOpen"] = true;
    packet["isRootMenu"] = false;
    packet["menuRevision"] = 6;
    packet["navigationRevision"] = 12;
    packet["rows"].push_back({{"label", "Second"}, {"value", "UNKNOWN"}, {"enabled", false}});
    // UNKNOWN is owned display data, never a guessed callback result
    packet["menuOptionCallback"] = {{"revision", 7}, {"kind", kind}, {"phase", phase}};
    return packet;
}
void OwnsLegacyAndExactCallbackPhases();
void RejectsMalformedCallbackFieldsAndDecodedDuplicates();
void RejectsInvalidCallbackContextsAndEveryCompetingWait();
void CallbackOpeningCompatibilityDoesNotInventRedrawsOrValues();
void CallbackModesConsumeHeldPendingAndFocusInputsUntilFreshRelease();
void CallbackWaitRetiresPrivateScrollReceipts();
void PreviewOwnsExactCallbacksAndLegacyWithoutChangingRows();
void PreviewRejectsMalformedCallbacksContextsAndOpeningConflicts();
}
