#include "chrome/theme/ChromeThemeTests.h"
#include "instructions/theme/InstructionThemeTests.h"
#include "instructions/color/InstructionColorTests.h"
#include "instructions/InstructionHudTests.h"
#include "chrome/MenuChromeTests.h"
#include "paint/BooleanPaintTests.h"
#include "callbacks/CallbackTestModels.h"
#include "TestSupport.h"
#include "quick/LocalMenuSchemaTests.h"
#include <fstream>
#include <iostream>

int main(int argc, char** argv) {
    using namespace iw::overlay::tests;
    // An optional report path keeps test results available when Proton hides console output
    std::ofstream report;
    if (argc > 3) return 2;
    if (argc >= 2) {
        report.open(argv[1], std::ios::binary | std::ios::trunc);
        if (!report) return 2;
    }
    std::ostream& output = argc >= 2 ? report : std::cout;
    struct Test { const char* name; void (*run)(); };
    const Test tests[] = {
        {"ChromeThemeOwnsSourceCreationAndHistoricalMasks", chrome::ChromeThemeOwnsSourceCreationAndHistoricalMasks},
        {"ChromeThemeRejectsMalformedFieldsAndUnprovedScope", chrome::ChromeThemeRejectsMalformedFieldsAndUnprovedScope},
        {"InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority", instructions::InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority},
        {"InstructionThemeRejectsMalformedJournalAndCrossCreationClaims", instructions::InstructionThemeRejectsMalformedJournalAndCrossCreationClaims},
        {"InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState", instructions::InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState},
        {"InstructionColorRejectsMalformedPairsAndDecodedDuplicates", instructions::InstructionColorRejectsMalformedPairsAndDecodedDuplicates},
        {"InstructionHudOwnsLegacyExplicitAbsenceAndIndependentCreation", instructions::InstructionHudOwnsLegacyExplicitAbsenceAndIndependentCreation},
        {"InstructionHudRejectsMalformedFieldsAndUnverifiedAdmission", instructions::InstructionHudRejectsMalformedFieldsAndUnverifiedAdmission},
        {"InstructionHudHintsUseExactSavedPoliciesAndDisabledDeadRoute", instructions::InstructionHudHintsUseExactSavedPoliciesAndDisabledDeadRoute},
        {"OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs", chrome::OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs},
        {"RejectsMalformedChromeRevisionsAndDecodedDuplicates", chrome::RejectsMalformedChromeRevisionsAndDecodedDuplicates},
        {"PaintIdentityRetiresPrivateScrollReceipts", paint::PaintIdentityRetiresPrivateScrollReceipts},
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

        {"LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion", LocalAdmissionReceiptsRetireEveryChangedFactAndFamilyBeforePromotion},
        {"ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues", ScopedMenuOpensRequireAllCapturedEpochsWithoutInventingValues},
        {"JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact", JumpBackRequiresCapturedLocalAndNavigationWhileLegacyPacketsStayExact},

        {"LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts", LocalEligibilityPreservesLegacyAndOwnsUnknownNullableFacts},
        {"LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds", LocalEligibilityOwnsAllLifeFactsAcrossOpenClosedAndUnsignedBounds},
        {"LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication", LocalEligibilityRejectsMalformedExactFieldsBeforeClosedPublication},
        {"LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext", LocalEligibilityRejectsDecodedDuplicatesAndOverflowWithoutBorrowingContext},
        {"MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference", MenuSurfacePreservesLegacyAndOwnsBothFamiliesWithoutTitleInference},
        {"MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates", MenuSurfaceRejectsWrongTypesNamesAndDecodedDuplicates},

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

        {"RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData", RuntimeStartupRejectsUnavailableActionsBeforeReturningPublishableData},
        {"OpeningControlsRejectMalformedDescriptorBeforeRouting", OpeningControlsRejectMalformedDescriptorBeforeRouting},
        {"OpeningRowsRejectMalformedSpacerAndUnknownAction", OpeningRowsRejectMalformedSpacerAndUnknownAction},

        {"StealthAppearancePreservesLegacyAndOwnsBothBooleanValues", StealthAppearancePreservesLegacyAndOwnsBothBooleanValues},
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
        {"MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets", MenuDesignFooterPreservesMissingNullLegacyAndOwnsAllFiveExactTargets},
        {"MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates", MenuDesignFooterRejectsMalformedTargetsUnknownKeysAndDecodedDuplicates},
        {"MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts", MenuDesignFooterRejectsCooldownForbiddenFlagsAndConflictingContexts},

        {"MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance", MenuDesignFooterAcceptsCommittedTargetWithoutReplacingCapturedAppearance},
        {"MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows", MenuOpenTransitionPreservesLegacyAndOwnsRootChildAndEmptyRows},
        {"MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict", MenuOpenTransitionRejectsClosedInvalidSelectionsAndEveryModalConflict},
        {"MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration", MenuOpenTransitionAllowsFadesOnlyAfterRedrawWithoutOwningTheirDuration},
        {"MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks", MenuOpenTransitionConsumesAllHeldControlsThroughHealthyAndLateUnlocks},
        {"MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle", MenuOpenTransitionConsumesFocusPendingAndHiddenPacketsWithoutReplayingToggle},
        {"MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures", MenuOpenTransitionResumesRootChildPrioritiesAndRepeatWithoutNewGestures},

        {"MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates", MenuOpenTransitionRejectsMalformedPhasesAndDecodedDuplicates},
        {"MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities", MenuOpeningPreservesLegacyDefaultsAndOwnsPositiveUnsignedIdentities},
        {"MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal", MenuOpeningRejectsShortRowsInvalidSelectionsAndEveryForbiddenModal},
        {"MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock", MenuOpeningAllowsOrdinaryRootChildAndDesignCooldownWithoutOwningItsLock},
        {"MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands", MenuOpeningRoutesEveryOrdinaryFreshControlWithoutNewKeysOrCommands},
        {"MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged", MenuOpeningExpirationLeavesEpochsAndAnEstablishedScrollRepeatUnchanged},
        {"MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease", MenuOpeningContinuesAfterDesignUnlockWhileHeldKeysRequireFreshRelease},
        {"MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration", MenuRowCapacityMatchesSourceAndBoundsEveryOpeningDuration},

        {"MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates", MenuOpeningRejectsMalformedIdentitiesElapsedValuesAndDecodedDuplicates},
        {"MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch", MenuDesignSameChoiceCooldownConsumesHoldsWithoutChangingEitherEpoch},
        {"MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames", MenuDesignPreservesLegacyAppearanceAndMapsAllFiveExactNames},
        {"MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState", MenuDesignRejectsForbiddenFlagsWithoutNormalizingProducerState},
        {"MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields", MenuDesignIsOwnedAndAppendedWithoutChangingOlderAppearanceFields},
        {"MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases", MenuDesignTransitionPreservesLegacyAndOwnsBothLockedPhases},
        {"MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates", MenuDesignTransitionRejectsMalformedPhasesAndDecodedDuplicates},
        {"MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict", MenuDesignTransitionRejectsInvalidOrdinaryContextsAndEveryModalConflict},
        {"MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls", MenuDesignTransitionJoinsBoundariesAndConsumesPendingFocusAndHeldControls},

        {"MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates", MenuDesignRejectsNonStringsUnknownNamesAndDecodedDuplicates},
        {"PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows", PreferenceRefreshPreservesLegacyAndOwnsBothPhasesWithoutChangingRows},
        {"PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext", PreferenceRefreshRejectsEveryModalAndInvalidOrdinaryContext},
        {"PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl", PreferenceRefreshJoinsPhaseBoundariesAndConsumesEveryHeldControl},
        {"PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting", PreferenceRefreshConsumesPendingFocusLossAndRestoresNormalRouting},

        {"PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields", PreferenceRefreshRejectsMalformedPhasesAndDuplicateFields},
        {"MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries", MenuCreditsPreserveLegacyDefaultsAndOwnedElapsedBoundaries},
        {"MenuCreditsRejectEveryConflictingEditorUnlockAndSelection", MenuCreditsRejectEveryConflictingEditorUnlockAndSelection},
        {"MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions", MenuCreditsOwnPrivateMeleeHelpWithoutReplacingOrdinaryInstructions},
        {"MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch", MenuCreditsFinishCommandsRequireTheirExactCapturedUnsignedEpoch},
        {"MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds", MenuCreditsSchemaJoinsFreshExitAndConsumesReturnHolds},

        {"MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts", MenuCreditsRejectsInvalidElapsedTimeAndClosedContexts},
        {"MenuPositionDefaultsAndSignedBoundsRemainOwned", MenuPositionDefaultsAndSignedBoundsRemainOwned},
        {"MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately", MenuPositionEditorOwnsStepsHintsAndOrdinaryFooterSeparately},
        {"MenuPositionEditorRejectsEveryConflictingModalContext", MenuPositionEditorRejectsEveryConflictingModalContext},
        {"MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias", MenuPositionGrenadeBindingsRejectMalformedKeysAndEveryAlias},
        {"MenuPositionCommandsRequireExactCapturedUnsignedEpochs", MenuPositionCommandsRequireExactCapturedUnsignedEpochs},
        {"MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds", MenuPositionSchemaRoutesImmediateEntryAndConsumesUnlockHolds},
        {"MenuPositionRejectsMalformedCoordinatesAndEditorMetadata", MenuPositionRejectsMalformedCoordinatesAndEditorMetadata},
        {"ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps", ScrollDurationPreservesLegacyDefaultsAndExactSupportedSteps},
        {"ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates", ScrollDurationRejectsWrongTypesRangesStepsAndDuplicates},
        {"MenuRevisionPreservesAbsenceAndUnsignedBoundaries", MenuRevisionPreservesAbsenceAndUnsignedBoundaries},
        {"MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates", MenuRevisionRejectsNullWrongTypesNegativeAndDuplicates},
        {"PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson", PresentationFocusEpochIsPrivateOwnedAndNeverReadFromJson},
        {"ObservedFocusTransitionsStampAnOwnedPresentationContext", ObservedFocusTransitionsStampAnOwnedPresentationContext},
        {"FocusEpochExhaustionPermanentlyFailsClosed", FocusEpochExhaustionPermanentlyFailsClosed},
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
        {"MissingAndNullUnlockPhasesPreserveLegacyMenus", MissingAndNullUnlockPhasesPreserveLegacyMenus},
        {"ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues", ConfirmedUnlockPhasesAreOwnedWithoutChangingFeatureValues},
        {"UnsupportedUnlockNamesAndTypesCannotLockMenuControls", UnsupportedUnlockNamesAndTypesCannotLockMenuControls},
        {"UnlockPhasesRequireAnOpenNonEditorChildContext", UnlockPhasesRequireAnOpenNonEditorChildContext},
        {"ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume", ParsedUnlockPhasesJoinInputRoutingAndReseedOnResume},
        {"DuplicatePrivateFieldsCannotOverwriteAConfirmedValue", DuplicatePrivateFieldsCannotOverwriteAConfirmedValue},
        {"PrivateJsonDepthRemainsBoundedWithoutDiscardingData", PrivateJsonDepthRemainsBoundedWithoutDiscardingData},
        {"UnknownBindingPropertiesCannotInventAControlCapability", UnknownBindingPropertiesCannotInventAControlCapability},

        {"MenuWidthDefaultsAndSupportedWidthsRemainExact", MenuWidthDefaultsAndSupportedWidthsRemainExact},
        {"MenuWidthRejectsOutOfRangeAndWrongTypes", MenuWidthRejectsOutOfRangeAndWrongTypes},
        {"WidthEditorStateAndPrivateHintsAreCopiedSeparately", WidthEditorStateAndPrivateHintsAreCopiedSeparately},
        {"WidthEditorRejectsMalformedOrNonEditorContexts", WidthEditorRejectsMalformedOrNonEditorContexts},
        {"ExactAttackBindingsPreserveKeysAndLegacyPackets", ExactAttackBindingsPreserveKeysAndLegacyPackets},
        {"ExactAttackBindingsRejectAmbiguousMembershipAndTypes", ExactAttackBindingsRejectAmbiguousMembershipAndTypes},
        {"WidthEditorCommandsRequireTheExactNavigationEpoch", WidthEditorCommandsRequireTheExactNavigationEpoch},
        {"EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands", EditorMetadataJoinsTheRealRouterWithoutNormalMenuCommands},

        {"InputStartupAndClosedCombination", InputStartupAndClosedCombination},
        {"InputFocusAndMenuReseeding", InputFocusAndMenuReseeding},
        {"InputPriorityAndHeldKeys", InputPriorityAndHeldKeys},
        {"InputPendingDropsEdges", InputPendingDropsEdges},
        {"BindingsRejectAmbiguityAndInvalidKeys", BindingsRejectAmbiguityAndInvalidKeys},
        {"AckGateKeepsOneInflightAndRejectsRollback", AckGateKeepsOneInflightAndRejectsRollback},
        {"HeartbeatExpiresRecoversAndRejectsRollback", HeartbeatExpiresRecoversAndRejectsRollback},
        {"StateValidSnapshotAndBoundaries", StateValidSnapshotAndBoundaries},
        {"StateRejectsWrongIdentityVersionsAndTypes", StateRejectsWrongIdentityVersionsAndTypes},
        {"StateRejectsTextUtf8AndRowBounds", StateRejectsTextUtf8AndRowBounds},
        {"StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice", StateAcceptsProducerUnicodeNoticeAndRejectsOversizedNotice},
        {"StateControlHintsAreCopiedWithoutChangingFeatureRows", StateControlHintsAreCopiedWithoutChangingFeatureRows},
        {"StateAppearanceDefaultsPreserveLegacyPackets", StateAppearanceDefaultsPreserveLegacyPackets},
        {"StateAppearanceChoicesAreCopiedAndKeepFeatureState", StateAppearanceChoicesAreCopiedAndKeepFeatureState},
        {"StateAppearanceRejectsUnsupportedValuesAndTypes", StateAppearanceRejectsUnsupportedValuesAndTypes},
        {"StateQuickExitDefaultsAndBooleanValuesAreCopied", StateQuickExitDefaultsAndBooleanValuesAreCopied},
        {"StateQuickExitRejectsNonBooleanValues", StateQuickExitRejectsNonBooleanValues},
        {"StateRootMenuIdentityDefaultsAndValuesAreCopied", StateRootMenuIdentityDefaultsAndValuesAreCopied},
        {"StateRootMenuIdentityRejectsNonBooleanValues", StateRootMenuIdentityRejectsNonBooleanValues},
        {"StateQuickExitControlHintsUseConfirmedRootIdentity", StateQuickExitControlHintsUseConfirmedRootIdentity},
        {"CloseCommandsRequireTheExactNavigationEpoch", CloseCommandsRequireTheExactNavigationEpoch},
        {"BindingsSchemaRejectsMalformedArrays", BindingsSchemaRejectsMalformedArrays},
        {"BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys", BindingsOptionalChoiceGroupsRemainExactAndRejectMalformedKeys},
        {"NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch", NavigationRevisionIsUnsignedAndPreservesTheControllerEpoch},
        {"CommandSchemaPreservesUnsignedSequence", CommandSchemaPreservesUnsignedSequence},
        {"RendererStatusSchemaPreservesBoundsAndIdentity", RendererStatusSchemaPreservesBoundsAndIdentity},
        {"RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields", RuntimeErrorSchemaIsBoundedAndHasOnlyPublicFields},
        {"SessionFilesAtomicReplacementAndBounds", SessionFilesAtomicReplacementAndBounds},
        {"SessionDirectoryReplacementIsRejected", SessionDirectoryReplacementIsRejected},
        {"SessionReparseDirectoriesAndFilesAreRejected", SessionReparseDirectoriesAndFilesAreRejected},
        {"SessionReplacementRetriesUntilSharedReaderReleases", SessionReplacementRetriesUntilSharedReaderReleases},
        {"SessionReplacementRetriesUntilDeleteDenyingReaderReleases", SessionReplacementRetriesUntilDeleteDenyingReaderReleases},
        {"SessionReplacementPersistentBlockFailsAndPreservesFrame", SessionReplacementPersistentBlockFailsAndPreservesFrame},
        {"SessionReplacementReadonlyTargetIsRejected", SessionReplacementReadonlyTargetIsRejected},
        {"SessionReplacementDirectoryAndReparseTargetsAreRejected", SessionReplacementDirectoryAndReparseTargetsAreRejected},
        {"SessionReplacementRechecksDirectoryDuringRetry", SessionReplacementRechecksDirectoryDuringRetry},
        {"RuntimeOwnFixtureStartsAndStopsWithoutGameFeatures", RuntimeOwnFixtureStartsAndStopsWithoutGameFeatures},
        {"RuntimeWrongConfigNeverStarts", RuntimeWrongConfigNeverStarts},
        {"RuntimeFailureClosesCachedSnapshot", RuntimeFailureClosesCachedSnapshot},
        {"RuntimeMalformedStatePublishesOnlySanitizedError", RuntimeMalformedStatePublishesOnlySanitizedError},
        {"RuntimeErrorPublicationFailureStillClosesAndStops", RuntimeErrorPublicationFailureStillClosesAndStops},
        {"RuntimeStaleHeartbeatClosesAndRecoversCachedSnapshot", RuntimeStaleHeartbeatClosesAndRecoversCachedSnapshot},
        {"RuntimePublishesInitialAndChangedRendererStatus", RuntimePublishesInitialAndChangedRendererStatus}
    };
    try {
        for (const auto& test : tests) {
            test.run();
            output << "PASS " << test.name << '\n';
        }
        // A third argument checks fresh production-controller exports through the real parser
        if (argc == 3) {
            BackendPreviewStatesPreserveActualControllerPreferences(argv[2]);
            output << "PASS BackendPreviewStatesPreserveActualControllerPreferences\n";
        }
        output << "Passed " << std::size(tests) + (argc == 3 ? 1 : 0) << " native runtime regression tests\n";
        return 0;
    } catch (const std::exception& exception) {
        iw::overlay::RuntimeStop();
        output << "FAIL " << exception.what() << '\n';
        return 1;
    }
}
