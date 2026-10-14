using System.Collections;
using Apparition.Backend.OpenControls;

namespace Backend.Tests;

// Bounds must be checked before trusting indexes or enumerators from external lists
internal sealed class UnboundedOpeningActions : IReadOnlyList<OpeningAction> {
    public int Count => int.MaxValue;
    public int IndexReads { get; private set; }
    public OpeningAction this[int index] {
        get { IndexReads++; throw new InvalidOperationException("An unbounded tuple was indexed"); }
    }
    public IEnumerator<OpeningAction> GetEnumerator() => throw new InvalidOperationException("An unbounded tuple was enumerated");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
