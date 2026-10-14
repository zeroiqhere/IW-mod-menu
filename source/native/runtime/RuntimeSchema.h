#pragma once

#include "../shared/OverlayProtocol.h"
#include "NativeInputRouter.h"
#include "OpeningActionLabels.h"
#include "scroll/SpacerScrollReceiptTracker.h"
#include "../render/RendererDiagnostics.h"
#include <optional>

namespace iw::overlay::runtime {

inline constexpr std::size_t MaximumFileBytes = 32768;

struct ParsedState {
    std::shared_ptr<const MenuSnapshot> snapshot;
    unsigned long long lastSequence;
    unsigned long long generation;
};

// Parsing returns owned data and does not retain JSON buffers or file handles
ParsedState ParseState(const std::string& bytes, DWORD expectedProcessId,
    const std::vector<std::string>* controlHints = nullptr,
    const std::vector<std::string>* quickExitHints = nullptr,
    const std::vector<std::string>* rootQuickExitHints = nullptr,
    const std::vector<std::string>* widthEditorHints = nullptr,
    const std::vector<std::string>* positionHints = nullptr,
    std::optional<std::uint64_t> presentationFocusRevision = std::nullopt,
    const std::vector<std::string>* menuPositionHints = nullptr,
    const std::vector<std::string>* creditsHints = nullptr,
    const OpeningActionLabels* openingActionLabels = nullptr,
    const scroll::SpacerScrollObservation* spacerObservation = nullptr);
// Parsed phase identity owns routing rather than a title or a renderer guess
InputMode InputModeForSnapshot(const MenuSnapshot& snapshot);
Bindings ParseBindings(const std::string& bytes);
// Close and editor commands require their captured epoch; legacy navigation keeps its packet
std::string CommandJson(DWORD processId, unsigned long long sequence, Input input,
    std::optional<std::uint64_t> navigationRevision = std::nullopt,
    std::optional<std::uint64_t> openingControlsRevision = std::nullopt,
    std::optional<std::uint64_t> localAdmissionRevision = std::nullopt,
    std::optional<std::uint64_t> menuRevision = std::nullopt);
std::string ReadyJson(DWORD processId);
std::string RendererStatusJson(DWORD processId, const RendererStatus& status);
std::string RuntimeErrorJson(DWORD processId);

}
