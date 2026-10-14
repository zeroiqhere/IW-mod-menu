using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The existing account fixture remains unchanged and owns every source byte
internal sealed class LocalActorMemoryFixture {
    internal const long CurrentParty = LocalBindingMemoryFixture.ModuleBase + 0x524C4A0;
    internal const long OtherParty = LocalBindingMemoryFixture.ModuleBase + 0x5254DA0;
    internal LocalBindingMemoryFixture Memory { get; }
    internal long HealthAddress => LocalBindingMemoryFixture.Entity(Memory.Member) + 0x1E0;
    internal long SessionStateAddress => LocalBindingMemoryFixture.Client(Memory.Member) + 0x4A94;

    internal LocalActorMemoryFixture(int member = 0, int health = 100, int session = 0, bool host = true) {
        Memory = new LocalBindingMemoryFixture(member);
        Memory.Int32(HealthAddress, health);
        Memory.Int32(SessionStateAddress, session);
        Memory.Int64(CurrentParty, Memory.Session);
        Memory.Int64(OtherParty, 0);
        Memory.Int32(CurrentParty + 0x2D10, 1);
        Memory.Int32(CurrentParty + 0x2C50, host ? member : (member + 1) % 4);
    }

    internal void ReplaceServerRecord(long server) {
        long anchor = server - (long)Memory.Member * LocalBindingMemoryFixture.ServerStride;
        for (int slot = 0; slot < 4; slot++) {
            Memory.Int64(LocalBindingMemoryFixture.Table + slot * 8,
                anchor + (long)slot * LocalBindingMemoryFixture.ServerStride);
        }
        Memory.Int32(server + 8, 5);
        Memory.Int64(server + 136, LocalBindingMemoryFixture.Entity(Memory.Member));
        Memory.Text(server + 1188, LocalBindingMemoryFixture.Name(Memory.Member), 32);
        Memory.Text(server + 0x9F340, LocalBindingMemoryFixture.Guid(Memory.Member), 21);
    }

    internal void ReplaceClient(long client) {
        Memory.Int64(LocalBindingMemoryFixture.Entity(Memory.Member) + 0x170, client);
        Memory.Int32(client + 0x4A90, 2);
        Memory.Int32(client + 0x4BDC, Memory.Member);
        Memory.Text(client + 0x4BB0, LocalBindingMemoryFixture.Name(Memory.Member), 32);
        Memory.Text(client + 0x4C50, LocalBindingMemoryFixture.Name(Memory.Member), 32);
    }

    internal LocalActorSnapshot Read() => LocalActorSnapshotReader.Read(LocalBindingMemoryFixture.ModuleBase, Memory.ReadBytes);
}
