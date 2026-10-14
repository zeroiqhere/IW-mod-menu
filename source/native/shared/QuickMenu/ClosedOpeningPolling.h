#pragma once

namespace iw::overlay {
// A local sampling mode cannot grant a menu, actor or opening policy
enum class ClosedOpeningPolling { FreshEdges, SourceLevels };
inline bool IsValidClosedOpeningPolling(ClosedOpeningPolling mode) noexcept {
    return mode == ClosedOpeningPolling::FreshEdges || mode == ClosedOpeningPolling::SourceLevels;
}
} // namespace iw::overlay
