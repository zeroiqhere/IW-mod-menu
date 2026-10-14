namespace Backend.Tests;

// A failed timestamp is injected at the real controller preflight, never by replacing its action
internal sealed class MonitorWaitFaultClock : TimeProvider, IManualClockFixture {
    public ManualTimeProvider Time { get; } = new();
    public bool FailTimestamp { get; set; }
    public override long TimestampFrequency => Time.TimestampFrequency;
    public override long GetTimestamp() => FailTimestamp ? throw new IOException("The owned test clock failed") : Time.GetTimestamp();
}

// A valid clock shape can still overflow its elapsed conversion
internal sealed class MonitorWaitOverflowClock : TimeProvider {
    public override long TimestampFrequency => 1;
    public override long GetTimestamp() => long.MaxValue;
}
