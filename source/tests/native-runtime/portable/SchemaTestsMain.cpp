#include "../chrome/theme/ChromeThemeTests.h"
#include "../instructions/theme/InstructionThemeTests.h"
#include "../instructions/color/InstructionColorTests.h"
#include "../instructions/InstructionHudTests.h"
#include "../chrome/MenuChromeTests.h"
#include "../paint/BooleanPaintTests.h"
#include "../callbacks/CallbackTestModels.h"
#include "../TestSupport.h"
#include "../quick/LocalMenuSchemaTests.h"
#include <iostream>

int main(int argc, char** argv) {
    using namespace iw::overlay::tests;
    // Optional producer exports join the real parser without a game or graphics process
    if (argc > 2) return 2;
    struct Test { const char* name; void (*run)(); };
    const Test tests[] = {
        {"ChromeThemeOwnsSourceCreationAndHistoricalMasks", chrome::ChromeThemeOwnsSourceCreationAndHistoricalMasks},
        {"ChromeThemeRejectsMalformedFieldsAndUnprovedScope", chrome::ChromeThemeRejectsMalformedFieldsAndUnprovedScope},
        {"ChromeThemePreviewMatchesProductionOwnership", chrome::ChromeThemePreviewMatchesProductionOwnership},
        {"InstructionThemePreviewUsesTheSameStrictOwnedJournal", instructions::InstructionThemePreviewUsesTheSameStrictOwnedJournal},
        {"InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority", instructions::InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority},
        {"InstructionThemeRejectsMalformedJournalAndCrossCreationClaims", instructions::InstructionThemeRejectsMalformedJournalAndCrossCreationClaims},
        {"InstructionColorPreviewMatchesLegacyPairsAndStrictValidation", instructions::InstructionColorPreviewMatchesLegacyPairsAndStrictValidation},
        {"InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState", instructions::InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState},
        {"InstructionColorRejectsMalformedPairsAndDecodedDuplicates", instructions::InstructionColorRejectsMalformedPairsAndDecodedDuplicates},
        {"InstructionHudPreviewMatchesStrictLifetimeAndAdmission", instructions::InstructionHudPreviewMatchesStrictLifetimeAndAdmission},
        {"InstructionHudOwnsLegacyExplicitAbsenceAndIndependentCreation", instructions::InstructionHudOwnsLegacyExplicitAbsenceAndIndependentCreation},
        {"InstructionHudRejectsMalformedFieldsAndUnverifiedAdmission", instructions::InstructionHudRejectsMalformedFieldsAndUnverifiedAdmission},
        {"InstructionHudHintsUseExactSavedPoliciesAndDisabledDeadRoute", instructions::InstructionHudHintsUseExactSavedPoliciesAndDisabledDeadRoute},
        {"PreviewMatchesStrictGlobalChromeAndLegacyDefaults", chrome::PreviewMatchesStrictGlobalChromeAndLegacyDefaults},
        {"OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs", chrome::OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs},
        {"RejectsMalformedChromeRevisionsAndDecodedDuplicates", chrome::RejectsMalformedChromeRevisionsAndDecodedDuplicates},
        {"PaintIdentityRetiresPrivateScrollReceipts", paint::PaintIdentityRetiresPrivateScrollReceipts},
        {"PreviewMatchesOwnedPaintAndRejectsMalformedSuppliedMetadata", paint::PreviewMatchesOwnedPaintAndRejectsMalformedSuppliedMetadata},
        {"OwnsBooleanPaintWithoutChangingCurrentRowsOrInputMode", paint::OwnsBooleanPaintWithoutChangingCurrentRowsOrInputMode},
        {"RejectsMalformedPaintFieldsBoundsAndDecodedDuplicates", paint::RejectsMalformedPaintFieldsBoundsAndDecodedDuplicates},
        {"RejectsInvalidPaintTargetsAndCompetingModalContexts", paint::RejectsInvalidPaintTargetsAndCompetingModalContexts},
        {"ResolvesUnknownAndLegacyValuesWithoutGrantingActions", paint::ResolvesUnknownAndLegacyValuesWithoutGrantingActions},

        {"OwnsLegacyAndExactCallbackPhases", callback::OwnsLegacyAndExactCallbackPhases},
        {"RejectsMalformedCallbackFieldsAndDecodedDuplicates", callback::RejectsMalformedCallbackFieldsAndDecodedDuplicates},
        {"RejectsInvalidCallbackContextsAndEveryCompetingWait", callback::RejectsInvalidCallbackContextsAndEveryCompetingWait},
        {"CallbackOpeningCompatibilityDoesNotInventRedrawsOrValues", callback::CallbackOpeningCompatibilityDoesNotInventRedrawsOrValues},
        {"CallbackModesConsumeHeldPendingAndFocusInputsUntilFreshRelease", callback::CallbackModesConsumeHeldPendingAndFocusInputsUntilFreshRelease},
        {"CallbackWaitRetiresPrivateScrollReceipts", callback::CallbackWaitRetiresPrivateScrollReceipts},
        {"PreviewOwnsExactCallbacksAndLegacyWithoutChangingRows", callback::PreviewOwnsExactCallbacksAndLegacyWithoutChangingRows},
        {"PreviewRejectsMalformedCallbacksContextsAndOpeningConflicts", callback::PreviewRejectsMalformedCallbacksContextsAndOpeningConflicts},

        {"LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion", LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion},
        {"ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues", ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues},
        {"JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact", JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact},

        {"LocalEligibilityPreviewMatchesOwnedNullableFactsAndBothSurfaces", LocalEligibilityPreviewMatchesOwnedNullableFactsAndBothSurfaces},
        {"LocalEligibilityPreviewRejectsMalformedFactsSurfaceAndDecodedDuplicates", LocalEligibilityPreviewRejectsMalformedFactsSurfaceAndDecodedDuplicates},
        {"LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts", LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts},
        {"LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds", LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds},
        {"LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication", LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication},
        {"LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext", LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext},
        {"MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference", MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference},
        {"MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates", MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates},

        {"OpeningPoliciesPreviewOwnsBothVerifiedTuplesAndSavedCaptions", OpeningPoliciesPreviewOwnsBothVerifiedTuplesAndSavedCaptions},
        {"SelectorPreviewPreservesOwnedOpeningAndRejectsOrdinaryCooldown", SelectorPreviewPreservesOwnedOpeningAndRejectsOrdinaryCooldown},
        {"SelectorRefreshPreservesOpeningOnlyForExactOpenControlsContext", SelectorRefreshPreservesOpeningOnlyForExactOpenControlsContext},
        {"OpeningPoliciesOwnIndependentEpochsAndPreserveLegacyMain", OpeningPoliciesOwnIndependentEpochsAndPreserveLegacyMain},
        {"OpeningPoliciesRejectExactFieldsTypesBoundsAndDecodedDuplicates", OpeningPoliciesRejectExactFieldsTypesBoundsAndDecodedDuplicates},
        {"OpeningPoliciesRequireExactMainMirrorButAllowCrossFamilyOverlap", OpeningPoliciesRequireExactMainMirrorButAllowCrossFamilyOverlap},
        {"OpeningPoliciesStartupRejectsUnverifiedQuickEvenClosedOrDisabled", OpeningPoliciesStartupRejectsUnverifiedQuickEvenClosedOrDisabled},
        {"OpeningPoliciesCommandsCaptureObservedGlobalEpochWithoutQuickActivation", OpeningPoliciesCommandsCaptureObservedGlobalEpochWithoutQuickActivation},
        {"OpeningPoliciesReceiptsRetireQuickOnlyGlobalAndDisableChanges", OpeningPoliciesReceiptsRetireQuickOnlyGlobalAndDisableChanges},

        {"SpacerSchemaStampsExactPrivateAckBeforeReturningImmutableSnapshot", SpacerSchemaStampsExactPrivateAckBeforeReturningImmutableSnapshot},
        {"SpacerSchemaCannotTrustReceiptJsonOrWrongAckAndFocus", SpacerSchemaCannotTrustReceiptJsonOrWrongAckAndFocus},
        {"SpacerSchemaFailureCannotPublishReceiptAndRetryKeepsDispatchTime", SpacerSchemaFailureCannotPublishReceiptAndRetryKeepsDispatchTime},

        {"SpacerReceiptRequiresExactRawAckAndOneConfirmedNavigation", SpacerReceiptRequiresExactRawAckAndOneConfirmedNavigation},
        {"SpacerReceiptRetainsOldRenderedSourceAndAdvancesReportsWithoutRestart", SpacerReceiptRetainsOldRenderedSourceAndAdvancesReportsWithoutRestart},
        {"SpacerReceiptRejectsChangedPrivateContextPolicyAndTopology", SpacerReceiptRejectsChangedPrivateContextPolicyAndTopology},
        {"SpacerReceiptExpiresFromDispatchWithoutHeartbeatReplay", SpacerReceiptExpiresFromDispatchWithoutHeartbeatReplay},
        {"SpacerReceiptChecksUnsignedBoundsAndRejectsRollback", SpacerReceiptChecksUnsignedBoundsAndRejectsRollback},
        {"SpacerReceiptCopiesIdentityWithoutCachingRowValuesAndResetDropsPending", SpacerReceiptCopiesIdentityWithoutCachingRowValuesAndResetDropsPending},
        {"SpacerReceiptCancelsUnexpectedNavigationWhileAckIsPending", SpacerReceiptCancelsUnexpectedNavigationWhileAckIsPending},

        {"SpacerPlannerRecognizesAllThreeProductionDirectionalPaths", SpacerPlannerRecognizesAllThreeProductionDirectionalPaths},
        {"SpacerPlannerAllowsStableMultipleBlanksButRejectsShiftedAmbiguity", SpacerPlannerAllowsStableMultipleBlanksButRejectsShiftedAmbiguity},
        {"SpacerPlannerNeverSkipsDisabledRowsOrInventsWrapMovement", SpacerPlannerNeverSkipsDisabledRowsOrInventsWrapMovement},
        {"SpacerPlannerRejectsForgedBranchWindowsAndMalformedTopology", SpacerPlannerRejectsForgedBranchWindowsAndMalformedTopology},
        {"SpacerPlannerRejectsLegacyHiddenAndEveryModalRoute", SpacerPlannerRejectsLegacyHiddenAndEveryModalRoute},
        {"SharedScrollWindowKeepsExactSourceBoundariesAndRejectsOverflowInputs", SharedScrollWindowKeepsExactSourceBoundariesAndRejectsOverflowInputs},

        {"MainOpeningDescriptorOwnsClosedRootChildAndPositiveBounds", MainOpeningDescriptorOwnsClosedRootChildAndPositiveBounds},
        {"MainOpeningDescriptorRejectsDecodedDuplicatesAndExactKeys", MainOpeningDescriptorRejectsDecodedDuplicatesAndExactKeys},
        {"OpeningRowsOwnSpacerAndRejectSelectedBlank", OpeningRowsOwnSpacerAndRejectSelectedBlank},
        {"OpeningActionLabelsUsePrivateGroupsWithoutGuessing", OpeningActionLabelsUsePrivateGroupsWithoutGuessing},
        {"OpeningActionBindingsOptionalStrictTypesAndMembership", OpeningActionBindingsOptionalStrictTypesAndMembership},
        {"OpeningMutationCommandsRequireCapturedPolicyAndNavigation", OpeningMutationCommandsRequireCapturedPolicyAndNavigation},

        {"OpeningPreviewMatchesStrictDescriptorSpacerAndPrivateLabels", OpeningPreviewMatchesStrictDescriptorSpacerAndPrivateLabels},
        {"RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData", RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData},
        {"OpeningControlsRejectMalformedDescriptorBeforeRouting", OpeningControlsRejectMalformedDescriptorBeforeRouting},
        {"OpeningRowsRejectMalformedSpacerAndUnknownAction", OpeningRowsRejectMalformedSpacerAndUnknownAction},

        {"StealthAppearancePreservesLegacyAndOwnsBothBooleanValues", StealthAppearancePreservesLegacyAndOwnsBothBooleanValues},
        {"StealthPreviewOwnsLegacyAppearanceAndBothRebuildPhases", StealthPreviewOwnsLegacyAppearanceAndBothRebuildPhases},
        {"StealthPreviewRejectsMalformedFlagsPhasesAndInvalidContexts", StealthPreviewRejectsMalformedFlagsPhasesAndInvalidContexts},
        {"StealthPreviewRejectsDecodedDuplicatesAndBoundsCooldownOpening", StealthPreviewRejectsDecodedDuplicatesAndBoundsCooldownOpening},
        {"PreferencePendingRefreshPreservesOpeningUntilActualRedraw", PreferencePendingRefreshPreservesOpeningUntilActualRedraw},
        {"StealthAppearanceRejectsWrongTypesAndDecodedDuplicates", StealthAppearanceRejectsWrongTypesAndDecodedDuplicates},
        {"StealthAppearanceRejectsBoxesAndCounterWithoutNormalizingFlags", StealthAppearanceRejectsBoxesAndCounterWithoutNormalizingFlags},
        {"StealthCapacityIsFiveAcrossEveryDesignAndInstructionVariant", StealthCapacityIsFiveAcrossEveryDesignAndInstructionVariant},
        {"PreferenceRebuildPreservesLegacyAndOwnsBothLockedPhases", PreferenceRebuildPreservesLegacyAndOwnsBothLockedPhases},
        {"PreferenceRebuildRejectsMalformedPhasesExtrasAndDecodedDuplicates", PreferenceRebuildRejectsMalformedPhasesExtrasAndDecodedDuplicates},
        {"PreferenceRebuildRejectsInvalidRowsAndEveryCompetingModal", PreferenceRebuildRejectsInvalidRowsAndEveryCompetingModal},
        {"PreferenceRebuildAllowsOnlyPostRedrawOpeningWithinStealthCapacity", PreferenceRebuildAllowsOnlyPostRedrawOpeningWithinStealthCapacity},
        {"PreferenceRebuildConsumesHeldPendingAndFocusEdgesUntilFreshRelease", PreferenceRebuildConsumesHeldPendingAndFocusEdgesUntilFreshRelease},
        {"StealthAppearanceRejectsExplicitNullBeforeReachingLayout", StealthAppearanceRejectsExplicitNullBeforeReachingLayout},
        {"MenuDesignFooterPreviewMatchesRuntimeLegacyAndOwnedRebuildTargets", MenuDesignFooterPreviewMatchesRuntimeLegacyAndOwnedRebuildTargets},
        {"MenuDesignFooterPreviewRejectsMalformedPhaseFlagsContextsAndDecodedDuplicates", MenuDesignFooterPreviewRejectsMalformedPhaseFlagsContextsAndDecodedDuplicates},

        {"MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets", MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets},
        {"MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates", MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates},
        {"MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts", MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts},

        {"MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance", MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance},
        {"MenuOpenTransitionPreviewMatchesRuntimeLegacyEmptyRootChildAndFades", MenuOpenTransitionPreviewMatchesRuntimeLegacyEmptyRootChildAndFades},
        {"MenuOpenTransitionPreviewRejectsMalformedConflictingAndUndrawnStates", MenuOpenTransitionPreviewRejectsMalformedConflictingAndUndrawnStates},

        {"MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows", MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows},
        {"MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict", MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict},
        {"MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration", MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration},
        {"MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks", MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks},
        {"MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle", MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle},
        {"MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures", MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures},

        {"MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates", MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates},
        {"MenuOpeningPreviewMatchesRuntimeLegacyBoundsAndAllowedContexts", MenuOpeningPreviewMatchesRuntimeLegacyBoundsAndAllowedContexts},
        {"MenuOpeningPreviewRejectsMalformedProgressContextsAndDecodedDuplicates", MenuOpeningPreviewRejectsMalformedProgressContextsAndDecodedDuplicates},

        {"MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities", MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities},
        {"MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal", MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal},
        {"MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock", MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock},
        {"MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands", MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands},
        {"MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged", MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged},
        {"MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease", MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease},
        {"MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration", MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration},

        {"MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates", MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates},
        {"MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch", MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch},
        {"MenuDesignPreviewMatchesRuntimeLegacyNamesConstraintsAndTransitions", MenuDesignPreviewMatchesRuntimeLegacyNamesConstraintsAndTransitions},
        {"MenuDesignPreviewRejectsMalformedNamesFlagsContextsAndDecodedDuplicates", MenuDesignPreviewRejectsMalformedNamesFlagsContextsAndDecodedDuplicates},

        {"MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames", MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames},
        {"MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState", MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState},
        {"MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields", MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields},
        {"MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases", MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases},
        {"MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates", MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates},
        {"MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict", MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict},
        {"MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls", MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls},

        {"MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates", MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates},
        {"PreferenceRefreshPreviewMatchesRuntimeLegacyAndBothPhases", PreferenceRefreshPreviewMatchesRuntimeLegacyAndBothPhases},
        {"PreferenceRefreshPreviewRejectsMalformedPhasesContextsAndDuplicates", PreferenceRefreshPreviewRejectsMalformedPhasesContextsAndDuplicates},

        {"PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows", PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows},
        {"PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext", PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext},
        {"PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl", PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl},
        {"PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting", PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting},

        {"PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields", PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields},
        {"MenuCreditsPreviewMatchesRuntimeLegacyAndExactElapsedBoundaries", MenuCreditsPreviewMatchesRuntimeLegacyAndExactElapsedBoundaries},
        {"MenuCreditsPreviewRejectsInvalidProgressDuplicatesAndConflictingContexts", MenuCreditsPreviewRejectsInvalidProgressDuplicatesAndConflictingContexts},

        {"MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries", MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries},
        {"MenuCreditsRejectEveryConflictingEditorUnlockAndSelection", MenuCreditsRejectEveryConflictingEditorUnlockAndSelection},
        {"MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions", MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions},
        {"MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch", MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch},
        {"MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds", MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds},

        {"MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts", MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts},
        {"MenuPositionPreviewMatchesRuntimeDefaultsSignedBoundsAndEditorSteps", MenuPositionPreviewMatchesRuntimeDefaultsSignedBoundsAndEditorSteps},
        {"MenuPositionPreviewRejectsMalformedCoordinatesEditorContextsAndDuplicates", MenuPositionPreviewRejectsMalformedCoordinatesEditorContextsAndDuplicates},

        {"MenuPositionDefaultsAndSignedBoundsRemainOwned", MenuPositionDefaultsAndSignedBoundsRemainOwned},
        {"MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately", MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately},
        {"MenuPositionEditorRejectsEveryConflictingModalContext", MenuPositionEditorRejectsEveryConflictingModalContext},
        {"MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias", MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias},
        {"MenuPositionCommandsRequireExactCapturedUnsignedEpochs", MenuPositionCommandsRequireExactCapturedUnsignedEpochs},
        {"MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds", MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds},
        {"MenuPositionRejectsMalformedCoordinatesAndEditorMetadata", MenuPositionRejectsMalformedCoordinatesAndEditorMetadata},
        {"InputStartupAndClosedCombination", InputStartupAndClosedCombination},
        {"InputFocusAndMenuReseeding", InputFocusAndMenuReseeding},
        {"InputPriorityAndHeldKeys", InputPriorityAndHeldKeys},
        {"InputPendingDropsEdges", InputPendingDropsEdges},
        {"BindingsRejectAmbiguityAndInvalidKeys", BindingsRejectAmbiguityAndInvalidKeys},
        {"AckGateKeepsOneInflightAndRejectsRollback", AckGateKeepsOneInflightAndRejectsRollback},
        {"HeartbeatExpiresRecoversAndRejectsRollback", HeartbeatExpiresRecoversAndRejectsRollback},
        {"ScrollPreviewMatchesRuntimeDurationAndMenuRevisionPresence", ScrollPreviewMatchesRuntimeDurationAndMenuRevisionPresence},
        {"ScrollPreviewRejectsMalformedDurationAndMenuRevision", ScrollPreviewRejectsMalformedDurationAndMenuRevision},
        {"ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps", ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps},
        {"ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates", ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates},
        {"MenuRevisionPreservesAbsenceAndUnsignedBoundaries", MenuRevisionPreservesAbsenceAndUnsignedBoundaries},
        {"MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates", MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates},
        {"PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson", PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson},
        {"ObservedFocusTransitionsStampAnOwnedPresentationContext", ObservedFocusTransitionsStampAnOwnedPresentationContext},
        {"FocusEpochExhaustionPermanentlyFailsClosed", FocusEpochExhaustionPermanentlyFailsClosed},
        {"RainbowPreviewPreservesLegacyModeAndMatchesOwnedRuntimeState", RainbowPreviewPreservesLegacyModeAndMatchesOwnedRuntimeState},
        {"RainbowPreviewRejectsMalformedModesDuplicatesAndInvalidAccent", RainbowPreviewRejectsMalformedModesDuplicatesAndInvalidAccent},
        {"RainbowModeDefaultsAndBooleanChoicesPreserveOwnedState", RainbowModeDefaultsAndBooleanChoicesPreserveOwnedState},
        {"RainbowModeRejectsEverySuppliedNonBooleanIncludingClosedMenus", RainbowModeRejectsEverySuppliedNonBooleanIncludingClosedMenus},
        {"RainbowModeRejectsPlainAndEscapedDuplicateProperties", RainbowModeRejectsPlainAndEscapedDuplicateProperties},
        {"RainbowModeStillRequiresAnExactStaticAccent", RainbowModeStillRequiresAnExactStaticAccent},
        {"PositionCoordinatesPreserveNullStyleDefaultsAndSignedBounds", PositionCoordinatesPreserveNullStyleDefaultsAndSignedBounds},
        {"PositionCoordinatesRejectWrongShapesTypesOverflowAndDuplicates", PositionCoordinatesRejectWrongShapesTypesOverflowAndDuplicates},
        {"PositionEditorPhasesAndPrivateHintsAreOwnedWithoutChangingOrdinaryHelp", PositionEditorPhasesAndPrivateHintsAreOwnedWithoutChangingOrdinaryHelp},
        {"PositionEditorRejectsUnsupportedPhasesAndConflictingContexts", PositionEditorRejectsUnsupportedPhasesAndConflictingContexts},
        {"PositionBindingsRemainExactAndRejectAliasesOrIncompleteSubsets", PositionBindingsRemainExactAndRejectAliasesOrIncompleteSubsets},
        {"PositionCommandsRequireTheirCapturedNavigationEpoch", PositionCommandsRequireTheirCapturedNavigationEpoch},
        {"PositionEditorSchemaJoinsRoutingAndConsumesTransitionHolds", PositionEditorSchemaJoinsRoutingAndConsumesTransitionHolds},
        {"InstructionAppearanceDefaultsPreserveLegacyPackets", InstructionAppearanceDefaultsPreserveLegacyPackets},
        {"InstructionAppearanceChoicesAreOwnedWithoutChangingState", InstructionAppearanceChoicesAreOwnedWithoutChangingState},
        {"InstructionAppearanceRejectsAlternateAdaptiveConflict", InstructionAppearanceRejectsAlternateAdaptiveConflict},
        {"InstructionAppearanceRejectsEverySuppliedNonBooleanType", InstructionAppearanceRejectsEverySuppliedNonBooleanType},
        {"InstructionPreviewMatchesRuntimeDefaultsAndChoices", InstructionPreviewMatchesRuntimeDefaultsAndChoices},
        {"InstructionPreviewRejectsMalformedOptionsAndStyleConflict", InstructionPreviewRejectsMalformedOptionsAndStyleConflict},
        {"PositionPreviewMatchesRuntimeCoordinatesAndEditorPhases", PositionPreviewMatchesRuntimeCoordinatesAndEditorPhases},
        {"PositionPreviewRejectsInvalidCoordinatesContextsAndDecodedDuplicates", PositionPreviewRejectsInvalidCoordinatesContextsAndDecodedDuplicates},
        {"StateValidSnapshotAndBoundaries", StateValidSnapshotAndBoundaries},
        {"StateRejectsWrongIdentityVersionsAndTypes", StateRejectsWrongIdentityVersionsAndTypes},
        {"StateRejectsTextUtf8AndRowBounds", StateRejectsTextUtf8AndRowBounds},
        {"StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice", StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice},
        {"StateControlHintsAreCopiedWithoutChangingFeatureRows", StateControlHintsAreCopiedWithoutChangingFeatureRows},
        {"BindingsSchemaRejectsMalformedArrays", BindingsSchemaRejectsMalformedArrays},
        {"BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys", BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys},
        {"NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch", NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch},
        {"StateAppearanceDefaultsPreserveLegacyPackets", StateAppearanceDefaultsPreserveLegacyPackets},
        {"StateAppearanceChoicesAreCopiedAndKeepFeatureState", StateAppearanceChoicesAreCopiedAndKeepFeatureState},
        {"StateAppearanceRejectsUnsupportedValuesAndTypes", StateAppearanceRejectsUnsupportedValuesAndTypes},
        {"StateQuickExitDefaultsAndBooleanValuesAreCopied", StateQuickExitDefaultsAndBooleanValuesAreCopied},
        {"StateQuickExitRejectsNonBooleanValues", StateQuickExitRejectsNonBooleanValues},
        {"StateRootMenuIdentityDefaultsAndValuesAreCopied", StateRootMenuIdentityDefaultsAndValuesAreCopied},
        {"StateRootMenuIdentityRejectsNonBooleanValues", StateRootMenuIdentityRejectsNonBooleanValues},
        {"StateQuickExitControlHintsUseConfirmedRootIdentity", StateQuickExitControlHintsUseConfirmedRootIdentity},
        {"CloseCommandsRequireTheExactNavigationEpoch", CloseCommandsRequireTheExactNavigationEpoch},
        {"CommandSchemaPreservesUnsignedSequence", CommandSchemaPreservesUnsignedSequence},
        {"RendererStatusSchemaPreservesBoundsAndIdentity", RendererStatusSchemaPreservesBoundsAndIdentity},
        {"RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields", RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields},
        {"MenuWidthDefaultsAndSupportedWidthsRemainExact", MenuWidthDefaultsAndSupportedWidthsRemainExact},
        {"MenuWidthRejectsOutOfRangeAndWrongTypes", MenuWidthRejectsOutOfRangeAndWrongTypes},
        {"WidthEditorStateAndPrivateHintsAreCopiedSeparately", WidthEditorStateAndPrivateHintsAreCopiedSeparately},
        {"DuplicatePrivateFieldsCannotOverwriteAConfirmedValue", DuplicatePrivateFieldsCannotOverwriteAConfirmedValue},
        {"PrivateJsonDepthRemainsBoundedWithoutDiscardingData", PrivateJsonDepthRemainsBoundedWithoutDiscardingData},
        {"UnknownBindingPropertiesCannotInventAControlCapability", UnknownBindingPropertiesCannotInventAControlCapability},
        {"WidthEditorRejectsMalformedOrNonEditorContexts", WidthEditorRejectsMalformedOrNonEditorContexts},
        {"ExactAttackBindingsPreserveKeysAndLegacyPackets", ExactAttackBindingsPreserveKeysAndLegacyPackets},
        {"ExactAttackBindingsRejectAmbiguousMembershipAndTypes", ExactAttackBindingsRejectAmbiguousMembershipAndTypes},
        {"WidthEditorCommandsRequireTheExactNavigationEpoch", WidthEditorCommandsRequireTheExactNavigationEpoch},
        {"EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands", EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands},
        {"MissingAndNullUnlockPhasesPreserveLegacyMenus", MissingAndNullUnlockPhasesPreserveLegacyMenus},
        {"ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues", ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues},
        {"UnsupportedUnlockNamesAndTypesCannotLockMenuControls", UnsupportedUnlockNamesAndTypesCannotLockMenuControls},
        {"UnlockPhasesRequireAnOpenNonEditorChildContext", UnlockPhasesRequireAnOpenNonEditorChildContext},
        {"ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume", ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume},
    };
    try {
        for (const auto& test : tests) {
            test.run();
            std::cout << "PASS " << test.name << '\n';
        }
        // Actual controller exports retain the exact row values and navigation checks
        if (argc == 2) {
            BackendPreviewStatesPreserveActualControllerPreferences(argv[1]);
            BackendLocalEligibilityPreviewMatchesActualScopedFamiliesAndNullableFacts(argv[1]);
            std::cout << "PASS BackendPreviewStatesPreserveActualControllerPreferences\n";
        }
        std::cout << "Passed " << std::size(tests) + (argc == 2 ? 1 : 0)
            << " portable native schema regression tests\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "FAIL " << exception.what() << '\n';
        return 1;
    }
}
