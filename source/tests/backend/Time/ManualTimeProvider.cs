namespace Backend.Tests;

// Deterministic timestamps let timing tests run without waiting on a real clock
internal sealed class ManualTimeProvider : TimeProvider {
    private long timestamp;
    private DateTimeOffset wallClock = DateTimeOffset.UnixEpoch;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public override DateTimeOffset GetUtcNow() => wallClock;
    public void Advance(TimeSpan elapsed) {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        timestamp = checked(timestamp + elapsed.Ticks);
    }
    public void MoveWallClock(TimeSpan delta) => wallClock += delta;
    // A broken provider is simulated only for the conservative rollback regression
    public void RollBackTimestamp(TimeSpan delta) => timestamp = checked(timestamp - delta.Ticks);
}

internal interface IManualClockFixture { ManualTimeProvider Time { get; } }
