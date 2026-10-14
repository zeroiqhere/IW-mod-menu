using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Actor and account fixtures remain unchanged; this wrapper adds only feature fields
internal sealed class LocalActorFlagsMemoryFixture {
    internal LocalActorMemoryFixture Actor { get; }
    internal LocalBindingMemoryFixture Memory => Actor.Memory;
    internal long FlagsAddress => LocalBindingMemoryFixture.Entity(Memory.Member) + 0x1C8;
    internal long CommandTimeAddress => LocalBindingMemoryFixture.Client(Memory.Member);
    internal long MovementAddress => CommandTimeAddress + 4;

    internal LocalActorFlagsMemoryFixture(int member = 0, int health = 100, int session = 0,
        int commandTime = 12345, int movementType = 0, uint flags = 0x200) {
        Actor = new LocalActorMemoryFixture(member, health, session);
        Memory.Int32(CommandTimeAddress, commandTime);
        Memory.Int32(MovementAddress, movementType);
        // Preserve all thirty-two flag bits through the fixture's signed storage helper
        Memory.Int32(FlagsAddress, unchecked((int)flags));
    }

    internal LocalActorFlagsSnapshot Read() => LocalActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, Memory.ReadBytes);
}
