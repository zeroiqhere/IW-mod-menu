using System;
using System.Collections.Generic;
using System.Text;

namespace syndishanx_iw_tool.Players;

// Read-only hypothesis from Aurora's asserted engine fields, pending retail validation
public static class CandidateRosterReader {
    private const long EntityArrayRva = 0x3D22610;
    private const int EntitySize = 0x3F8;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IReadOnlyList<DetectedPlayer> Read(long moduleBase,
        Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        var players = new List<DetectedPlayer>();
        if (moduleBase < 0x10000 || moduleBase > 0x7FFF00000000) {
            return players;
        }

        // Zombies has four display slots; no client-array stride is assumed
        for (int slot = 0; slot < 4; slot++) {
            try {
                long entity = moduleBase + EntityArrayRva + slot * EntitySize;
                byte[]? number = ReadExact(read, entity, sizeof(short));
                byte[]? index = ReadExact(read, entity + 0x9C, sizeof(int));
                byte[]? link = ReadExact(read, entity + 0x170, sizeof(long));
                if (number is null || index is null || link is null
                    || BitConverter.ToInt16(number) != slot || BitConverter.ToInt32(index) != slot) {
                    continue;
                }
                long client = BitConverter.ToInt64(link);
                // Reject null, low, unaligned, and out-of-range pointers before following them
                if (client < 0x10000 || client > 0x7FFFFFFF0000 || client % 8 != 0) {
                    continue;
                }
                byte[]? connected = ReadExact(read, client + 0x4A90, sizeof(int));
                byte[]? playing = ReadExact(read, client + 0x4A94, sizeof(int));
                byte[]? clientIndex = ReadExact(read, client + 0x4BDC, sizeof(int));
                if (connected is null || playing is null || clientIndex is null
                    || BitConverter.ToInt32(connected) != 2
                    || BitConverter.ToInt32(playing) != 0
                    || BitConverter.ToInt32(clientIndex) != slot) {
                    continue;
                }
                string? sessionName = ReadName(ReadExact(read, client + 0x4BB0, 32));
                string? stateName = ReadName(ReadExact(read, client + 0x4C50, 32));
                if (sessionName is null || stateName != sessionName) {
                    continue;
                }
                // Recheck the entity link to reject a slot changing during these reads
                byte[]? currentLink = ReadExact(read, entity + 0x170, sizeof(long));
                if (currentLink is not null && BitConverter.ToInt64(currentLink) == client) {
                    players.Add(new DetectedPlayer(slot, sessionName));
                }
            } catch (Exception exception) when (exception is System.IO.IOException
                or InvalidOperationException or System.ComponentModel.Win32Exception) {
                // A failed slot read must never become a guessed player
            }
        }
        return players;
    }

    private static byte[]? ReadExact(Func<long, int, byte[]?> read, long address, int count) {
        byte[]? bytes = read(address, count);
        return bytes?.Length == count ? bytes : null;
    }

    private static string? ReadName(byte[]? bytes) {
        if (bytes is null) {
            return null;
        }
        int end = Array.IndexOf(bytes, (byte)0);
        if (end <= 0) {
            return null;
        }
        try {
            string name = StrictUtf8.GetString(bytes, 0, end);
            // Control characters and whitespace-only data are not usable labels
            return string.IsNullOrWhiteSpace(name) || HasControlCharacter(name) ? null : name;
        } catch (DecoderFallbackException) {
            return null;
        }
    }

    private static bool HasControlCharacter(string value) {
        foreach (char character in value) {
            if (char.IsControl(character)) {
                return true;
            }
        }
        return false;
    }
}
