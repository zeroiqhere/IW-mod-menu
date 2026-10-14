namespace Apparition.Backend.Controller;

// Stored timestamps belong to the producer clock and never cross the wire
internal sealed record LocalInstructionThemeInvocation(ulong Revision, string Target, string Accent,
    bool StoppedRainbow, long StartedTimestamp, int? PredecessorGapMilliseconds);
