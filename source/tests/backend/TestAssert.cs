namespace Backend.Tests;

// Small assertions keep these tests independent of a test runner package
internal static class TestAssert {
    public static void True(bool condition, string reason) {
        if (!condition) throw new InvalidOperationException(reason);
    }
    public static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new InvalidOperationException($"Expected {expected}; observed {actual}");
        }
    }
    public static void Throws<T>(Action action) where T : Exception {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
