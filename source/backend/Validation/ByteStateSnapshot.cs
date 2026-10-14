using System;
using System.Diagnostics.CodeAnalysis;

namespace syndishanx_iw_tool.Validation;

// Keep validated bytes private so later input changes cannot alter restoration
public sealed class ByteStateSnapshot {
    private readonly byte[] originalBytes;
    private readonly byte[] replacementBytes;

    private ByteStateSnapshot(ReadOnlySpan<byte> original,
                              ReadOnlySpan<byte> replacement) {
        originalBytes = original.ToArray();
        replacementBytes = replacement.ToArray();
    }

    // Both states always cover the same original span
    public int Width => originalBytes.Length;

    public static bool TryCapture(ReadOnlySpan<byte> expectedOriginal,
                                   ReadOnlySpan<byte> current,
                                   ReadOnlySpan<byte> replacement,
                                   [NotNullWhen(true)] out ByteStateSnapshot? snapshot,
                                   out ValidationFailure failure) {
        snapshot = null;

        // Empty bytes cannot establish an original state
        if (expectedOriginal.IsEmpty || current.IsEmpty || replacement.IsEmpty) {
            failure = ValidationFailure.EmptyBytes;
            return false;
        }

        // Restoration must not leave part of a replacement behind
        if (current.Length != expectedOriginal.Length ||
            replacement.Length != expectedOriginal.Length) {
            failure = ValidationFailure.WidthMismatch;
            return false;
        }

        // Copy the observed bytes before checking and retaining their value
        byte[] observed = current.ToArray();
        if (!observed.AsSpan().SequenceEqual(expectedOriginal)) {
            failure = ValidationFailure.UnexpectedBytes;
            return false;
        }

        snapshot = new ByteStateSnapshot(observed, replacement);
        failure = ValidationFailure.None;
        return true;
    }

    // Exact comparisons also reject incomplete or oversized reads
    public bool MatchesOriginal(ReadOnlySpan<byte> current) =>
        current.SequenceEqual(originalBytes);

    public bool MatchesReplacement(ReadOnlySpan<byte> current) =>
        current.SequenceEqual(replacementBytes);

    // Return copies so a consumer cannot modify either retained state
    public byte[] CopyOriginalBytes() => (byte[])originalBytes.Clone();

    public byte[] CopyReplacementBytes() => (byte[])replacementBytes.Clone();
}
