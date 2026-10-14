#pragma once
#include "NativeInputRouter.h"
#include "OpeningActionLabels.h"
#include <string>

namespace iw::overlay::runtime {
// Display hints describe the exact saved controls used by the native router
std::vector<std::string> DescribeControls(const Bindings& bindings,
    bool quickExit = false, bool isRootMenu = false);
std::vector<std::string> DescribeWidthEditorControls(const Bindings& bindings);
std::vector<std::string> DescribeInstructionPositionControls(const Bindings& bindings);
std::vector<std::string> DescribeMenuPositionControls(const Bindings& bindings);
OpeningActionLabels DescribeOpeningActions(const Bindings& bindings);
std::vector<std::string> DescribeMenuCreditsControls(const Bindings& bindings);
}
