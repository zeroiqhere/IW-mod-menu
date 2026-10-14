namespace Session.Tests;

// The recovery policy reads one controlled monotonic millisecond counter
internal sealed class RecoveryTimeProvider : TimeProvider {
    internal long Milliseconds { get; set; }
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => Milliseconds;
}
