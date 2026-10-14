using syndishanx_iw_tool.Players.LocalBinding;

namespace Backend.Tests;

public static class ObservedBindingReadsTests {
    public static void CommandTimeVerificationRejectsAnUnobservedFieldBeforeReading() {
        int reads = 0;
        var fields = new ObservedBindingReads((_, width) => {
            reads++;
            return new byte[width];
        });

        TestAssert.Throws<InvalidOperationException>(() => fields.VerifyUnchangedCommandTime(0x10000));

        TestAssert.Equal(0, reads);
    }
}
