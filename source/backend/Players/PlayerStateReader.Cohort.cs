namespace syndishanx_iw_tool.Players;

public static partial class PlayerStateReader {
    // Membership and enable eligibility are distinct copied facts, not an atomic engine snapshot
    public static PlayerCohortObservation ReadCohort(long moduleBase, int processId,
        Func<long, int, byte[]?> read) {
        if (read is null || processId <= 0) return UnknownCohort("A supported process and read callback are required");
        try {
            ValidatePointer(moduleBase, "module base");
            Address(moduleBase, MigrateRva, sizeof(int));
            var globals = ReadGlobals(moduleBase, read);
            var first = ReadCohortPass(moduleBase, globals, read);

            // A changed world withdraws the entire observation instead of retaining a partial old session
            var repeatedGlobals = ReadGlobals(moduleBase, read);
            if (globals != repeatedGlobals) return UnknownCohort("The server context changed during the cohort read");
            var second = ReadCohortPass(moduleBase, repeatedGlobals, read);
            // Final scalars can expose a transition after the second identity checks completed
            if (globals != ReadGlobals(moduleBase, read)) return UnknownCohort("The server context changed after the cohort read");

            var slots = new PlayerSlotObservation[ExpectedServerCount];
            for (int slot = 0; slot < slots.Length; slot++) {
                var before = first[slot].Player;
                var current = second[slot].Player;
                if (before is null) slots[slot] = first[slot];
                else if (current is null) slots[slot] = second[slot];
                else if (before.Identity != current.Identity) {
                    slots[slot] = UnknownSlot(slot, "The player identity changed during the cohort read");
                } else if (current.CommandTime < before.CommandTime) {
                    slots[slot] = UnknownSlot(slot, "The player command time rolled back during the cohort read");
                } else {
                    // Use the latest scalar sample without turning a dead actor into an empty slot
                    slots[slot] = new(slot, PlayerSlotPresence.Present, current);
                }
            }
            return new(new(processId, moduleBase, globals.Clients, globals.Count), slots);
        } catch (Exception error) when (IsCohortReadFailure(error)) {
            return UnknownCohort(error.Message);
        }
    }

    private static PlayerSlotObservation[] ReadCohortPass(long moduleBase,
        (long Clients, int Count, long Table) globals, Func<long, int, byte[]?> read) {
        var slots = new PlayerSlotObservation[ExpectedServerCount];
        for (int slot = 0; slot < slots.Length; slot++) {
            try {
                long entity = Address(moduleBase, EntityArrayRva + slot * EntitySize, EntitySize);
                // Connected active records prove membership without requiring normal living feature play
                var identity = ReadIdentity(entity, slot, globals, read, requireEnableEligible: false);
                slots[slot] = new(slot, PlayerSlotPresence.Present, ReadOwnedScalars(identity, read));
            } catch (Exception error) when (IsCohortReadFailure(error)) {
                // No retail absence predicate is established for null, free, inactive or unreadable slots
                slots[slot] = UnknownSlot(slot, error.Message);
            }
        }
        return slots;
    }

    private static PlayerSlotObservation UnknownSlot(int slot, string reason) =>
        new(slot, PlayerSlotPresence.Unknown, FailureReason: reason);

    private static PlayerCohortObservation UnknownCohort(string reason) => new(null,
        Enumerable.Range(0, ExpectedServerCount).Select(slot => UnknownSlot(slot, reason)).ToArray());

    // Unexpected programming failures are not disguised as ordinary unreadable memory
    private static bool IsCohortReadFailure(Exception error) => error is IOException
        or InvalidOperationException or System.ComponentModel.Win32Exception;
}
