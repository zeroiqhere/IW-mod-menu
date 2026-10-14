#pragma once

#include "RuntimeSchema.h"
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <string>
#include <filesystem>

namespace iw::overlay::tests {
using Json = nlohmann::json;

inline void Check(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

template<class Action> void Reject(Action action) {
    try { action(); } catch (const std::exception&) { return; }
    throw std::runtime_error("Expected rejection");
}

inline Json StateJson() {
    return { {"version", 1}, {"processId", GetCurrentProcessId()}, {"lastSequence", 0}, {"generation", 1},
        {"isOpen", false}, {"title", "Main Menu"}, {"context", "OWN FIXTURE"},
        {"notice", "No game features"}, {"accent", "#3998FE"}, {"selectedIndex", 0},
        {"rows", Json::array({ { {"label", "Row"}, {"value", "OFF"}, {"enabled", true} } })} };
}

inline Json BindingsJson() {
    return { {"version", 1}, {"aim", {2}}, {"melee", {86, 6}},
        {"up", {2, 38}}, {"down", {1, 40}}, {"select", {70}} };
}

// File fixtures remain under the test process's temporary directory
class FixtureDirectory {
public:
    FixtureDirectory();
    ~FixtureDirectory();
    std::wstring path;
    void Write(const wchar_t* name, const std::string& bytes) const;
    void PublishState(const std::string& bytes) const;
    std::string Read(const wchar_t* name) const;
};

void MainOpeningDescriptorOwnsClosedRootChildAndPositiveBounds();
void MainOpeningDescriptorRejectsDecodedDuplicatesAndExactKeys();
void OpeningRowsOwnSpacerAndRejectSelectedBlank();
void OpeningActionLabelsUsePrivateGroupsWithoutGuessing();
void OpeningActionBindingsOptionalStrictTypesAndMembership();
void OpeningMutationCommandsRequireCapturedPolicyAndNavigation();

void OpeningPreviewMatchesStrictDescriptorSpacerAndPrivateLabels();
void RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData();
void OpeningControlsRejectMalformedDescriptorBeforeRouting();
void OpeningRowsRejectMalformedSpacerAndUnknownAction();

void MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates();
void MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows();
void MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict();
void MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration();
void MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks();
void MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle();
void MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures();
void MenuOpenTransitionPreviewMatchesRuntimeLegacyEmptyRootChildAndFades();
void MenuOpenTransitionPreviewRejectsMalformedConflictingAndUndrawnStates();
void BackendMenuOpenTransitionStatesPreserveActualRedrawsAndIndependentInputReadiness(const std::filesystem::path& directory);
void StealthAppearancePreservesLegacyAndOwnsBothBooleanValues();
void StealthPreviewOwnsLegacyAppearanceAndBothRebuildPhases();
void StealthPreviewRejectsMalformedFlagsPhasesAndInvalidContexts();
void StealthPreviewRejectsDecodedDuplicatesAndBoundsCooldownOpening();
void PreferencePendingRefreshPreservesOpeningUntilActualRedraw();
void StealthAppearanceRejectsWrongTypesAndDecodedDuplicates();
void StealthAppearanceRejectsBoxesAndCounterWithoutNormalizingFlags();
void StealthCapacityIsFiveAcrossEveryDesignAndInstructionVariant();
void PreferenceRebuildPreservesLegacyAndOwnsBothLockedPhases();
void PreferenceRebuildRejectsMalformedPhasesExtrasAndDecodedDuplicates();
void PreferenceRebuildRejectsInvalidRowsAndEveryCompetingModal();
void PreferenceRebuildAllowsOnlyPostRedrawOpeningWithinStealthCapacity();
void PreferenceRebuildConsumesHeldPendingAndFocusEdgesUntilFreshRelease();
void StealthAppearanceRejectsExplicitNullBeforeReachingLayout();
void MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance();
void MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets();
void MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates();
void MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts();
void MenuDesignFooterPreviewMatchesRuntimeLegacyAndOwnedRebuildTargets();
void MenuDesignFooterPreviewRejectsMalformedPhaseFlagsContextsAndDecodedDuplicates();
void InputStartupAndClosedCombination();
void InputFocusAndMenuReseeding();
void InputPriorityAndHeldKeys();
void InputPendingDropsEdges();
void BindingsRejectAmbiguityAndInvalidKeys();
void AckGateKeepsOneInflightAndRejectsRollback();
void MenuWidthDefaultsAndSupportedWidthsRemainExact();
void MissingAndNullUnlockPhasesPreserveLegacyMenus();
void ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues();
void UnsupportedUnlockNamesAndTypesCannotLockMenuControls();
void UnlockPhasesRequireAnOpenNonEditorChildContext();
void ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume();
void MenuWidthRejectsOutOfRangeAndWrongTypes();
void WidthEditorStateAndPrivateHintsAreCopiedSeparately();
void WidthEditorRejectsMalformedOrNonEditorContexts();
void ExactAttackBindingsPreserveKeysAndLegacyPackets();
void ExactAttackBindingsRejectAmbiguousMembershipAndTypes();
void WidthEditorCommandsRequireTheExactNavigationEpoch();
void EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands();
void DuplicatePrivateFieldsCannotOverwriteAConfirmedValue();
void PrivateJsonDepthRemainsBoundedWithoutDiscardingData();
void UnknownBindingPropertiesCannotInventAControlCapability();
void StateValidSnapshotAndBoundaries();
void StateRejectsWrongIdentityVersionsAndTypes();
void StateRejectsTextUtf8AndRowBounds();
void StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice();
void StateControlHintsAreCopiedWithoutChangingFeatureRows();
void StateAppearanceDefaultsPreserveLegacyPackets();
void StateAppearanceChoicesAreCopiedAndKeepFeatureState();
void StateAppearanceRejectsUnsupportedValuesAndTypes();
void InstructionAppearanceDefaultsPreserveLegacyPackets();
void PositionCoordinatesPreserveNullStyleDefaultsAndSignedBounds();
void PositionCoordinatesRejectWrongShapesTypesOverflowAndDuplicates();
void PositionEditorPhasesAndPrivateHintsAreOwnedWithoutChangingOrdinaryHelp();
void PositionEditorRejectsUnsupportedPhasesAndConflictingContexts();
void PositionBindingsRemainExactAndRejectAliasesOrIncompleteSubsets();
void PositionCommandsRequireTheirCapturedNavigationEpoch();
void PositionEditorSchemaJoinsRoutingAndConsumesTransitionHolds();
void InstructionAppearanceChoicesAreOwnedWithoutChangingState();
void InstructionAppearanceRejectsAlternateAdaptiveConflict();
void InstructionAppearanceRejectsEverySuppliedNonBooleanType();
void InstructionPreviewMatchesRuntimeDefaultsAndChoices();
void InstructionPreviewRejectsMalformedOptionsAndStyleConflict();
void PositionPreviewMatchesRuntimeCoordinatesAndEditorPhases();
void PositionPreviewRejectsInvalidCoordinatesContextsAndDecodedDuplicates();
void StateQuickExitDefaultsAndBooleanValuesAreCopied();
void StateQuickExitRejectsNonBooleanValues();
void StateRootMenuIdentityDefaultsAndValuesAreCopied();
void StateRootMenuIdentityRejectsNonBooleanValues();
void StateQuickExitControlHintsUseConfirmedRootIdentity();
void CloseCommandsRequireTheExactNavigationEpoch();
void BackendOpeningControlsPreserveActualPolicyRowsAndCapturedEpochs(const std::filesystem::path& directory);
void BackendPreviewStatesPreserveActualControllerPreferences(const std::filesystem::path& directory);
void BindingsSchemaRejectsMalformedArrays();
void BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys();
void NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch();
void CommandSchemaPreservesUnsignedSequence();
void SessionFilesAtomicReplacementAndBounds();
void SessionDirectoryReplacementIsRejected();
void SessionReparseDirectoriesAndFilesAreRejected();
void RuntimeOwnFixtureStartsAndStopsWithoutGameFeatures();
void RuntimeWrongConfigNeverStarts();
void RuntimeFailureClosesCachedSnapshot();
void HeartbeatExpiresRecoversAndRejectsRollback();
void RuntimeStaleHeartbeatClosesAndRecoversCachedSnapshot();
void RendererStatusSchemaPreservesBoundsAndIdentity();
void RuntimePublishesInitialAndChangedRendererStatus();
void RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields();
void RuntimeMalformedStatePublishesOnlySanitizedError();
void RuntimeErrorPublicationFailureStillClosesAndStops();
void SessionReplacementRetriesUntilSharedReaderReleases();
void SessionReplacementRetriesUntilDeleteDenyingReaderReleases();
void SessionReplacementPersistentBlockFailsAndPreservesFrame();
void SessionReplacementReadonlyTargetIsRejected();
void SessionReplacementDirectoryAndReparseTargetsAreRejected();
void SessionReplacementRechecksDirectoryDuringRetry();

// Rainbow protocol checks run without a rendering or game dependency
void RainbowModeDefaultsAndBooleanChoicesPreserveOwnedState();
void RainbowModeRejectsEverySuppliedNonBooleanIncludingClosedMenus();
void RainbowModeRejectsPlainAndEscapedDuplicateProperties();
void RainbowModeStillRequiresAnExactStaticAccent();
void RainbowPreviewPreservesLegacyModeAndMatchesOwnedRuntimeState();
void RainbowPreviewRejectsMalformedModesDuplicatesAndInvalidAccent();
// Scroll timing and private focus identity remain source-pure fixtures
void ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps();
void ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates();
void MenuRevisionPreservesAbsenceAndUnsignedBoundaries();
void MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates();
void PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson();
void ObservedFocusTransitionsStampAnOwnedPresentationContext();
void FocusEpochExhaustionPermanentlyFailsClosed();

void ScrollPreviewMatchesRuntimeDurationAndMenuRevisionPresence();
void ScrollPreviewRejectsMalformedDurationAndMenuRevision();


// Menu position checks share production parsing without platform calls
void MenuPositionRejectsMalformedCoordinatesAndEditorMetadata();

void MenuPositionDefaultsAndSignedBoundsRemainOwned();
void MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately();
void MenuPositionEditorRejectsEveryConflictingModalContext();
void MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias();
void MenuPositionCommandsRequireExactCapturedUnsignedEpochs();
void MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds();

void MenuPositionPreviewMatchesRuntimeDefaultsSignedBoundsAndEditorSteps();
void MenuPositionPreviewRejectsMalformedCoordinatesEditorContextsAndDuplicates();
void PreferenceRefreshPreviewMatchesRuntimeLegacyAndBothPhases();
void PreferenceRefreshPreviewRejectsMalformedPhasesContextsAndDuplicates();

void BackendPreferenceRefreshStatesPreserveActualControllerPhases(const std::filesystem::path& directory);

void MenuDesignPreviewMatchesRuntimeLegacyNamesConstraintsAndTransitions();
void MenuDesignPreviewRejectsMalformedNamesFlagsContextsAndDecodedDuplicates();

void MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch();

void BackendStealthStatesPreserveActualTransitionsCapacityAndFreshInput(const std::filesystem::path& directory);
void BackendMenuDesignStatesPreserveActualControllerChoicesAndTransitions(const std::filesystem::path& directory);

void MenuOpeningPreviewMatchesRuntimeLegacyBoundsAndAllowedContexts();
void MenuOpeningPreviewRejectsMalformedProgressContextsAndDecodedDuplicates();

void BackendMenuOpeningStatesPreserveActualControllerEventsAndInputIndependence(const std::filesystem::path& directory);

// Opening alpha remains independent from locks and saved control routing
void MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates();
void MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities();
void MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal();
void MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock();
void MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands();
void MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged();
void MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease();
void MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration();


// Design state is bounded and independent from game features
void MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates();
void MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames();
void MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState();
void MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields();
void MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases();
void MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates();
void MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict();
void MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls();


// Local refresh metadata reuses the established paused input route
void PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields();
void PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows();
void PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext();
void PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl();
void PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting();


// Credits remain a bounded local presentation with no feature transport
void MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts();

void MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries();
void MenuCreditsRejectEveryConflictingEditorUnlockAndSelection();
void MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions();
void MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch();
void MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds();

void MenuCreditsPreviewMatchesRuntimeLegacyAndExactElapsedBoundaries();
void MenuCreditsPreviewRejectsInvalidProgressDuplicatesAndConflictingContexts();

// Private spacer receipts join native planning, raw acknowledgments and owned snapshots
void SpacerPlannerRecognizesAllThreeProductionDirectionalPaths();
void SpacerPlannerAllowsStableMultipleBlanksButRejectsShiftedAmbiguity();
void SpacerPlannerNeverSkipsDisabledRowsOrInventsWrapMovement();
void SpacerPlannerRejectsForgedBranchWindowsAndMalformedTopology();
void SpacerPlannerRejectsLegacyHiddenAndEveryModalRoute();
void SharedScrollWindowKeepsExactSourceBoundariesAndRejectsOverflowInputs();

void SpacerReceiptRequiresExactRawAckAndOneConfirmedNavigation();
void SpacerReceiptRetainsOldRenderedSourceAndAdvancesReportsWithoutRestart();
void SpacerReceiptRejectsChangedPrivateContextPolicyAndTopology();
void SpacerReceiptExpiresFromDispatchWithoutHeartbeatReplay();
void SpacerReceiptChecksUnsignedBoundsAndRejectsRollback();
void SpacerReceiptCopiesIdentityWithoutCachingRowValuesAndResetDropsPending();
void SpacerReceiptCancelsUnexpectedNavigationWhileAckIsPending();

void SpacerSchemaStampsExactPrivateAckBeforeReturningImmutableSnapshot();
void SpacerSchemaCannotTrustReceiptJsonOrWrongAckAndFocus();
void SpacerSchemaFailureCannotPublishReceiptAndRetryKeepsDispatchTime();

void BackendSpacerScrollPromotesExactControllerAcksAcrossAllProductionBranches(const std::filesystem::path& directory);

// Dual policy ownership uses the same strict packet and command boundaries
void OpeningPoliciesOwnIndependentEpochsAndPreserveLegacyMain();
void OpeningPoliciesRejectExactFieldsTypesBoundsAndDecodedDuplicates();
void OpeningPoliciesRequireExactMainMirrorButAllowCrossFamilyOverlap();
void OpeningPoliciesStartupRejectsUnverifiedQuickEvenClosedOrDisabled();
void OpeningPoliciesCommandsCaptureObservedGlobalEpochWithoutQuickActivation();
void OpeningPoliciesReceiptsRetireQuickOnlyGlobalAndDisableChanges();

void SelectorRefreshPreservesOpeningOnlyForExactOpenControlsContext();
void OpeningPoliciesPreviewOwnsBothVerifiedTuplesAndSavedCaptions();
void SelectorPreviewPreservesOwnedOpeningAndRejectsOrdinaryCooldown();
void BackendOpeningPoliciesPreserveIndependentOwnersAndGlobalCommandScopes(const std::filesystem::path &directory);
}

namespace iw::overlay::tests {
// Authenticated admission facts retain unknown values without granting a new menu route
void LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts();
void LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds();
void LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication();
void LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext();
void MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference();
void MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates();
}
