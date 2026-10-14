using System;
using System.Collections.Generic;

namespace syndishanx_iw_tool.Diagnostics;

// Keep recent feature results so another successful action cannot hide a failure
public sealed class MessageHistory {
    private readonly Queue<string> entries = new();
    private readonly object entriesLock = new();
    private readonly int maximumEntries;
    private readonly int maximumMessageLength;

    public MessageHistory(int maximumEntries = 50, int maximumMessageLength = 2000) {
        if (maximumEntries <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries), "History limits must be positive");
        }
        if (maximumMessageLength <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumMessageLength), "History limits must be positive");
        }
        this.maximumEntries = maximumEntries;
        this.maximumMessageLength = maximumMessageLength;
    }

    public void Append(string message) {
        ArgumentNullException.ThrowIfNull(message);
        // Bound each entry as well as the number kept during a long game session
        string bounded = Truncate(message);
        lock (entriesLock) {
            // Queue updates and copied frames share the same short critical section
            entries.Enqueue(bounded);
            while (entries.Count > maximumEntries) {
                entries.Dequeue();
            }
        }
    }

    // Return a copy so readers cannot change retained messages
    public IReadOnlyList<string> Entries {
        get {
            lock (entriesLock) {
                return entries.ToArray();
            }
        }
    }

    // Formatting does not hold the queue lock or observe a changing enumeration
    public string Render() => string.Join(Environment.NewLine + Environment.NewLine, Entries);

    private string Truncate(string message) {
        if (message.Length <= maximumMessageLength) return message;
        int length = maximumMessageLength;
        // A cutoff inside a complete surrogate pair keeps neither half
        if (char.IsHighSurrogate(message[length - 1]) && char.IsLowSurrogate(message[length])) {
            length--;
        }
        return message[..length];
    }
}
