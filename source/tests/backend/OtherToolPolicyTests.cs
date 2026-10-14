using Apparition.Backend.Runtime;

namespace Backend.Tests;

internal static class OtherToolPolicyTests {
    public static void OlderToolsAndDuplicateBackendsAreRejectedWhileSelfAndUnrelatedProcessesAreAllowed() {
        foreach (string name in new[] { "syndishanx_iw_tool", "iw_apparition_overlay", "iw_apparition_backend" }) {
            TestAssert.True(OtherToolPolicy.IsConflict(name, 51, 42), "Each other memory-owning tool must block connection");
            TestAssert.True(OtherToolPolicy.IsConflict(name.ToUpperInvariant(), 51, 42), "Windows process names ignore case");
            TestAssert.True(!OtherToolPolicy.IsConflict(name, 42, 42), "The current backend must not reject itself");
        }
        TestAssert.True(!OtherToolPolicy.IsConflict("iw7_ship", 51, 42), "The actual game must not count as another tool");
        TestAssert.True(!OtherToolPolicy.IsConflict("unrelated", 51, 42), "Unrelated processes must remain allowed");
    }
}
