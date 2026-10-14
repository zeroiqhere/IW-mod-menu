namespace syndishanx_iw_tool.Validation;

// Explain why a candidate or byte snapshot was rejected
public enum ValidationFailure {
    None,
    MissingCandidates,
    AmbiguousCandidates,
    InvalidCandidate,
    EmptyBytes,
    WidthMismatch,
    UnexpectedBytes
}
