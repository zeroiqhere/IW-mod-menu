#pragma once
#include <filesystem>
namespace iw::overlay::tests::instructionOptions {
// One aggregate joins the actual instruction state, copied checkbox sample and caller wait
void ActualInstructionGraphKeepsCurrentFlagsPaintAndCallerWaitsSeparate(const std::filesystem::path& directory);
}
