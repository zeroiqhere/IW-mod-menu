using System.Buffers.Binary;
using Backend.Tests;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

internal static class LocalGodmodeCommandTimeIntegrationTests {
    public static async Task AdvancingCommandTimeKeepsTheRealBoundFeatureAliveAndRestoresOnlyItsBit() {
        const uint originalFlags = 0x80000200;
        const int initialCommandTime = 12345;
        var fixture = new LocalActorFlagsMemoryFixture(commandTime: initialCommandTime, flags: originalFlags);
        int latestCommandTime = initialCommandTime;
        fixture.Memory.BeforeRead = (address, _, count) => {
            if (address == fixture.CommandTimeAddress && count % 2 == 0) {
                latestCommandTime++;
                fixture.Memory.Int32(fixture.CommandTimeAddress, latestCommandTime);
            }
        };
        var writes = new List<(long Address, uint Expected, uint Desired)>();
        BoundLocalActorFlagsTransport? bound = null;
        var service = new LocalGodmodeSession(fixture.Read, initial => {
            bound = Bind(fixture, initial, writes);
            return bound;
        }, TimeSpan.FromHours(1));

        try {
            LocalGodmodeFixture.Check(await service.ToggleAsync(),
                "Normal command-time progress must not retire the local feature read");
            LocalGodmodeFixture.Check(service.ReadView().Value == GodmodeValue.On,
                "The actual bound feature must confirm its owned ON state");
            LocalGodmodeFixture.Check(bound?.ReadFlags() == (originalFlags | 1U),
                "The same original adapter must still read after another normal command-time tick");
            LocalGodmodeFixture.Check(await service.ToggleAsync(),
                "The same bound feature must restore after command-time progress");

            uint observedFlags = BinaryPrimitives.ReadUInt32LittleEndian(
                fixture.Memory.ReadBytes(fixture.FlagsAddress, sizeof(uint))!);
            LocalGodmodeFixture.Check(observedFlags == originalFlags,
                "Restoration must preserve the original flags outside bit zero");
            LocalGodmodeFixture.Check(writes.Count == 2
                && writes[0] == (fixture.FlagsAddress, originalFlags, originalFlags | 1U)
                && writes[1] == (fixture.FlagsAddress, originalFlags | 1U, originalFlags),
                "The feature may write only the captured actor's single owned flag bit");
        } finally {
            await service.CloseAsync();
        }
    }

    public static Task ActualReaderCommandTimeRollbackRetiresTheOriginalBoundWriter() {
        var fixture = new LocalActorFlagsMemoryFixture(commandTime: 12345, flags: 0x80000200);
        LocalActorFlagsSnapshot initial = fixture.Read();
        var writes = new List<(long Address, uint Expected, uint Desired)>();
        BoundLocalActorFlagsTransport transport = Bind(fixture, initial, writes);

        fixture.Memory.Int32(fixture.CommandTimeAddress, initial.CommandTime + 1);
        LocalGodmodeFixture.Check(transport.ReadFlags() == initial.Flags,
            "An advancing command-time watermark must remain usable before a rollback");
        fixture.Memory.Int32(fixture.CommandTimeAddress, initial.CommandTime);
        TestAssert.Throws<IOException>(() => transport.ReadFlags());
        fixture.Memory.Int32(fixture.CommandTimeAddress, initial.CommandTime + 2);
        TestAssert.Throws<IOException>(() => transport.ReadFlags());
        LocalGodmodeFixture.Check(writes.Count == 0,
            "A rolled-back adapter cannot reach the writer or recover into a later time");
        return Task.CompletedTask;
    }

    private static BoundLocalActorFlagsTransport Bind(LocalActorFlagsMemoryFixture fixture,
        LocalActorFlagsSnapshot initial, List<(long Address, uint Expected, uint Desired)> writes) =>
        new(initial, () => true,
            () => LocalOwnedActorFlagsReader.Read(LocalBindingMemoryFixture.ModuleBase, fixture.Memory.ReadBytes),
            (address, expected, desired) => {
                writes.Add((address, expected, desired));
                byte[] currentBytes = fixture.Memory.ReadBytes(address, sizeof(uint))!;
                uint current = BinaryPrimitives.ReadUInt32LittleEndian(currentBytes);
                if (address != fixture.FlagsAddress || current != expected) return false;
                fixture.Memory.Int32(address, unchecked((int)desired));
                return true;
            });
}
