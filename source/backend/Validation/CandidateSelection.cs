using System;
using System.Collections.Generic;

namespace syndishanx_iw_tool.Validation;

// Select an existing candidate without locating or changing anything
public static class CandidateSelection {
    public static bool TrySelectUnique<T>(IReadOnlyList<T>? candidates,
                                          Predicate<T> isValid,
                                          out T selected,
                                          out ValidationFailure failure) {
        ArgumentNullException.ThrowIfNull(isValid);
        selected = default!;

        // An absent result cannot identify a usable candidate
        if (candidates is null || candidates.Count == 0) {
            failure = ValidationFailure.MissingCandidates;
            return false;
        }

        // Duplicate entries remain ambiguous even when their values match
        if (candidates.Count != 1) {
            failure = ValidationFailure.AmbiguousCandidates;
            return false;
        }

        // The caller supplies the validity rule for its own candidate type
        T candidate = candidates[0];
        if (candidate is null || !isValid(candidate)) {
            failure = ValidationFailure.InvalidCandidate;
            return false;
        }

        selected = candidate;
        failure = ValidationFailure.None;
        return true;
    }
}
