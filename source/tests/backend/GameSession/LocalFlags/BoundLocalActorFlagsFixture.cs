using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// All actor fields and flag writes belong to ordinary managed fixture storage
internal sealed class BoundLocalActorFlagsFixture {
    internal LocalActorFlagsSnapshot Current { get; set; }
    internal LocalOwnedActorFlagsSnapshot? OwnedOverride { get; set; }
    internal bool Connected { get; set; } = true;
    internal Action? BeforeRead { get; set; }
    internal Action? AfterRead { get; set; }
    internal bool ThrowOnRead { get; set; }
    internal int Reads { get; private set; }
    internal List<(long Address, uint Expected, uint Desired)> Writes { get; } = new();

    internal BoundLocalActorFlagsFixture(int slot = 0) {
        var identity = new syndishanx_iw_tool.Players.PlayerIdentity(slot,
            0x140000000 + 0x3D22610 + slot * 0x3F8, 0x400000000 + slot * 0x10000,
            0x300000000, 4, "Local player", "0123456789ABCDEF") {
            ServerClient = 0x300000000 + slot * 715416L
        };
        var binding = new LocalAccountSessionBinding(0, 1, 0x210000000, 0,
            0x140000000 + 0x6B4E080, slot, identity);
        Current = new(new LocalActorSnapshot(binding, 100, 0, true), 1000, 0, 0x20);
    }

    internal BoundLocalActorFlagsTransport Bind() => new(Current, () => Connected, Read, Write);

    internal LocalOwnedActorFlagsSnapshot Read() {
        Reads++;
        BeforeRead?.Invoke();
        if (ThrowOnRead) throw new IOException("The owned fixture read failed");
        var snapshot = OwnedOverride ?? Owned(Current);
        AfterRead?.Invoke();
        return snapshot;
    }

    internal static LocalOwnedActorFlagsSnapshot Owned(LocalActorFlagsSnapshot snapshot) =>
        new(snapshot.Actor, snapshot.CommandTime, snapshot.MovementType, snapshot.Flags);

    internal bool Write(long address, uint expected, uint desired) {
        Writes.Add((address, expected, desired));
        var current = OwnedOverride ?? Owned(Current);
        if (address != current.Binding.Identity.Entity + 0x1C8 || current.Flags != expected) return false;
        if (OwnedOverride is not null) OwnedOverride = new(current.Actor, current.CommandTime, current.MovementType, desired);
        else Current = new(Current.Actor, Current.CommandTime, Current.MovementType, desired);
        return true;
    }

    internal void ChangeBinding(LocalAccountSessionBinding binding) =>
        Current = new(new LocalActorSnapshot(binding, Current.Health, 0, true),
            Current.CommandTime, 0, Current.Flags);
}
