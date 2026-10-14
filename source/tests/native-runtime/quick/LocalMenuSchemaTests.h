#pragma once
#include <filesystem>
namespace iw::overlay::tests {
// Reader parity checks are additional portable cases, not copies of runtime registrations
void LocalEligibilityPreviewMatchesOwnedNullableFactsAndBothSurfaces();
void LocalEligibilityPreviewRejectsMalformedFactsSurfaceAndDecodedDuplicates();
void ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues();
void JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact();
void LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion();
void BackendLocalEligibilityPreservesActualScopedFamiliesEpochsWaitsAndInertRows(const std::filesystem::path& directory);
void BackendLocalEligibilityPreviewMatchesActualScopedFamiliesAndNullableFacts(const std::filesystem::path& directory);
}
