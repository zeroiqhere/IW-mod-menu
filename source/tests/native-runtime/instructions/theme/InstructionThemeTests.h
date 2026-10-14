#pragma once
namespace iw::overlay::tests::instructions {
void InstructionThemeCopiesExplicitOwnedJournalWithoutChangingAuthority();
void InstructionThemeRejectsMalformedJournalAndCrossCreationClaims();
void InstructionThemePreviewUsesTheSameStrictOwnedJournal();
}
