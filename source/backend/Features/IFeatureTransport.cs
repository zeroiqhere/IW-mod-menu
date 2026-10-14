using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace syndishanx_iw_tool.Features;

// Keep existing backend operations outside feature state handling
public interface IFeatureTransport {
    bool Connected { get; }
    Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken cancellationToken);
    byte[] ReadAmmoBytes(long candidate, int count);
    bool WriteAmmoBytes(long candidate, byte[] bytes);
    int ReadHealth();
    bool WriteHealth(int value);
}
