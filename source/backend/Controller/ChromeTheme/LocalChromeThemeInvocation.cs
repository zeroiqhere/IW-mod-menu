namespace Apparition.Backend.Controller;

// Producer clock ticks never cross the wire or become a native widget identity
internal sealed record LocalChromeThemeInvocation(ulong Revision, int TargetMask, string Accent,
    bool StoppedRainbow, long StartedTimestamp, int? PredecessorGapMilliseconds);
