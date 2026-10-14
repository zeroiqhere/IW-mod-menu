using NativeObservation.Tests;

// An explicit private report works even when Proton does not attach the console streams
using var report = args.Length == 2 && args[0] == "--report" ? new StreamWriter(args[1]) { AutoFlush = true } : null;
if (report is not null) { Console.SetOut(report); Console.SetError(report); }

if (args.SequenceEqual(new[] { "--access-mask-diagnostic" })) return AccessMaskDiagnostic.Run();

Action[] tests = [
    ReadOnlyMemoryTests.CurrentProcessHandleReadsExactCopiedOwnedBytes,
    ReadOnlyMemoryTests.ReadOnlyHandleRejectsAnAttemptedWriteToOwnedBytes,
    ReadOnlyMemoryTests.IncompleteProtectedAndReleasedPagesNeverReturnFabricatedBytes,
    ReadOnlyMemoryTests.InvalidProcessIdsAndReadSpansRejectBeforeNativeReads,
    ReadOnlyMemoryTests.ReleasedHandleCannotReadAfterItsOwnerDisposesIt
];
int passed = 0;
foreach (var test in tests) {
    try { test(); Console.WriteLine("PASS " + test.Method.Name); passed++; }
    catch (Exception exception) { Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + exception); }
}
Console.WriteLine($"{passed}/{tests.Length} native read-only memory checks passed; game access=0");
return passed == tests.Length ? 0 : 1;
