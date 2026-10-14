using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// An explicit shared store follows account keys, not transient menu or actor owners
internal static class MemoryCommittedMenuPreferencesStoreTests {
    public static void MissingAccountsReturnNoPreferenceOrInventedDefault() {
        ICommittedMenuPreferencesStore store = new MemoryCommittedMenuPreferencesStore();
        TestAssert.True(!store.TryRead(Scope(11), out var value) && value is null,
            "A missing account must not silently acquire a saved preference");
    }

    public static void SameAccountSharesCommittedValuesAcrossDifferentOwners() {
        var store = new MemoryCommittedMenuPreferencesStore();
        var first = Scope(11);
        var later = Scope(11);
        var value = Value("#00FF00");
        store.Write(first, value);
        TestAssert.True(store.TryRead(later, out var restored) && ReferenceEquals(value, restored),
            "A shared process-local store must retain the immutable value across controller owners");
        // Reading storage grants no process or actor authority to the later owner
        TestAssert.True(!ReferenceEquals(first.OwnerToken, later.OwnerToken), "The test must use separate authority owners");
    }

    public static void DifferentAccountsAndStoresRemainIndependent() {
        var shared = new MemoryCommittedMenuPreferencesStore();
        var green = Value("#00FF00");
        var red = Value("#FF0000");
        shared.Write(Scope(11), green);
        shared.Write(Scope(22), red);
        TestAssert.True(shared.TryRead(Scope(11), out var first) && ReferenceEquals(green, first)
            && shared.TryRead(Scope(22), out var second) && ReferenceEquals(red, second),
            "Separate account keys must never share preferences");
        TestAssert.True(!new MemoryCommittedMenuPreferencesStore().TryRead(Scope(11), out _),
            "A new store must not inherit hidden static or disk state");
    }

    public static void InvalidWritesPreserveThePreviouslyCommittedValue() {
        var store = new MemoryCommittedMenuPreferencesStore();
        var scope = Scope(11);
        var value = Value("#00FF00");
        store.Write(scope, value);
        TestAssert.Throws<ArgumentNullException>(() => store.Write(null!, value));
        TestAssert.Throws<ArgumentNullException>(() => store.Write(scope, null!));
        TestAssert.Throws<ArgumentNullException>(() => store.TryRead(null!, out _));
        TestAssert.True(store.TryRead(scope, out var retained) && ReferenceEquals(value, retained),
            "Rejected storage arguments must not erase a prior complete value");
    }

    public static void ConcurrentReadersObserveOnlyCompleteImmutableWrites() {
        var store = new MemoryCommittedMenuPreferencesStore();
        var scope = Scope(11);
        var green = Value("#00FF00");
        var red = new CommittedMenuPreferences(MenuAppearance.Default with { BooleanDisplay = "Text", MenuWidth = 400 },
            "#FF0000", false, quickDisabled: true);
        store.Write(scope, green);
        Parallel.For(0, 200, index => {
            store.Write(scope, index % 2 == 0 ? green : red);
            TestAssert.True(store.TryRead(scope, out var value) && (ReferenceEquals(value, green) || ReferenceEquals(value, red)),
                "Readers must observe one complete immutable preference, never mixed fields");
        });
    }

    private static LocalMenuPreferenceScope Scope(ulong account) => new(account, new object());
    private static CommittedMenuPreferences Value(string accent) => new(MenuAppearance.Default, accent, false);
}
