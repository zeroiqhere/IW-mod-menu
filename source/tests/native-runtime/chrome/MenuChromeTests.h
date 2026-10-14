#pragma once
namespace iw::overlay::tests::chrome {
void OwnsGlobalChromeCreationWithoutChangingRowsInputOrPublicEpochs();
void RejectsMalformedChromeRevisionsAndDecodedDuplicates();
void PreviewMatchesStrictGlobalChromeAndLegacyDefaults();
}
