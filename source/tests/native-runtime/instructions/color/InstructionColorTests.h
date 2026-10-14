#pragma once
namespace iw::overlay::tests::instructions {
void InstructionColorOwnsPairedCreationFactsWithoutChangingCurrentState();
void InstructionColorRejectsMalformedPairsAndDecodedDuplicates();
void InstructionColorPreviewMatchesLegacyPairsAndStrictValidation();
}
