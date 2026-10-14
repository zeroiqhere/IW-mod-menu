#pragma once
#include "../ClosedTransitionChecks.h"

namespace iw::overlay::tests {
void CheckRendererFailureRetirement(HWND window, const ChainFactory& create, const TransitionCheck& check,
                                    std::ostream& log);
}
