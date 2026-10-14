#pragma once
#include <filesystem>
namespace iw::overlay::tests::monitor {
void LegacyPacketsKeepOrdinaryInputWithoutAWait();
void ChoiceWaitCopiesPositiveBoundsWithoutInventingAModalOrEpoch();
void BackWaitOwnsOpenParentsAndClosedRootsInBothFamilies();
void InvalidMechanicalWaitShapesAreRejected();
void MechanicalWaitCannotBorrowUnknownLocalAdmission();
void MechanicalWaitRejectsEveryCompetingCallbackAndModal();
void DecodedDuplicateWaitFieldsCannotReplaceTheirOwner();
void PreviewAndRuntimeShareStrictWaitOwnership();
void ActualControllerWaitPacketsMatchBothStrictReaders(const std::filesystem::path& directory);
void ActualControllerWaitChronologyJoinsNativeRouting(const std::filesystem::path& directory);
}
