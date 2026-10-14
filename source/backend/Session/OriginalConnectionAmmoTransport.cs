using syndishanx_iw_tool.Features;

namespace Apparition.Backend.Session;

// Only the known replacement bytes require new-write admission; owned originals can restore
internal sealed class OriginalConnectionAmmoTransport : IFeatureTransport {
    private readonly IFeatureTransport inner;
    private readonly Func<bool> admitEnable;
    private readonly byte[] replacement;

    public OriginalConnectionAmmoTransport(IFeatureTransport inner, Func<bool> admitEnable, byte[] replacement) {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.admitEnable = admitEnable ?? throw new ArgumentNullException(nameof(admitEnable));
        this.replacement = (byte[])(replacement ?? throw new ArgumentNullException(nameof(replacement))).Clone();
    }

    // Actual native connectivity continues to govern original cleanup
    public bool Connected => inner.Connected;
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken cancellationToken) =>
        inner.FindAmmoCandidatesAsync(cancellationToken);
    public byte[] ReadAmmoBytes(long candidate, int count) => inner.ReadAmmoBytes(candidate, count);

    public bool WriteAmmoBytes(long candidate, byte[] bytes) {
        // The feature still verifies its exact candidate, original bytes, and trailing context
        if (bytes.AsSpan().SequenceEqual(replacement) && !admitEnable()) return false;
        return inner.WriteAmmoBytes(candidate, bytes);
    }

    // No legacy health feature is created by this connection
    public int ReadHealth() => inner.ReadHealth();
    public bool WriteHealth(int value) => admitEnable() && inner.WriteHealth(value);
}
