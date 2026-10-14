using syndishanx_iw_tool.Features;

namespace Features.Tests;

// Synthetic storage records requested operations without opening a process
internal sealed class RecordingTransport : IFeatureTransport {
    public bool Connected { get; set; } = true;
    public IReadOnlyList<long> Candidates { get; set; } = new long[] { 1 };
    public byte[] Bytes { get; set; } = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    public List<(long Candidate, byte[] Bytes)> AmmoWrites { get; } = new();
    public Func<int, byte[]?>? ReadOverride { get; set; }
    public bool AcceptAmmoWrite { get; set; } = true;
    public bool ApplyAmmoWrite { get; set; } = true;
    public Func<int, bool>? OnHealthWrite { get; set; }
    public Func<int, int>? OnHealthRead { get; set; }
    public int Health { get; set; } = 100;
    public int Scans { get; private set; }
    public int AmmoReads { get; private set; }
    public Exception? ScanFailure { get; set; }
    private int healthWrites;
    private int healthReads;
    public int HealthWrites => Volatile.Read(ref healthWrites);
    public int HealthReads => Volatile.Read(ref healthReads);

    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Scans++;
        if (ScanFailure is not null) {
            return Task.FromException<IReadOnlyList<long>>(ScanFailure);
        }
        return Task.FromResult(Candidates);
    }

    public byte[] ReadAmmoBytes(long candidate, int count) {
        AmmoReads++;
        byte[]? overridden = ReadOverride?.Invoke(AmmoReads);
        return overridden is null ? Bytes.Take(count).ToArray() : (byte[])overridden.Clone();
    }

    public bool WriteAmmoBytes(long candidate, byte[] bytes) {
        AmmoWrites.Add((candidate, (byte[])bytes.Clone()));
        if (AcceptAmmoWrite && ApplyAmmoWrite) {
            bytes.CopyTo(Bytes, 0);
        }
        return AcceptAmmoWrite;
    }

    public int ReadHealth() {
        int count = Interlocked.Increment(ref healthReads);
        return OnHealthRead?.Invoke(count) ?? Health;
    }

    public bool WriteHealth(int value) {
        int count = Interlocked.Increment(ref healthWrites);
        if (OnHealthWrite?.Invoke(count) == false) {
            return false;
        }
        Health = value;
        return true;
    }
}
