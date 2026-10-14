using System.Text.Json;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// The oracle combines captured native getter cases with the pinned source wrapper
internal static class NativeLocalPlayerLifeTests {
    public static void PositiveHealthMatchesTheFourObservedSessionStrings() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-life-source-oracle.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var states = document.RootElement.GetProperty("definedSessionCases").EnumerateArray().ToArray();
        TestAssert.Equal(4, states.Length);

        // These strings came from saved native getter handles, not a guessed playing-only rule
        string[] expectedStrings = { "playing", "dead", "spectator", "intermission" };
        for (int index = 0; index < states.Length; index++) {
            var state = states[index];
            TestAssert.Equal(index, state.GetProperty("value").GetInt32());
            TestAssert.Equal(expectedStrings[index], state.GetProperty("decodedString").GetString());
            var expected = Enum.Parse<LocalPlayerLife>(state.GetProperty("positiveHealthLife").GetString()!);
            TestAssert.Equal(expected, NativeLocalPlayerLife.Evaluate(100, index));
        }
    }

    public static void NonpositiveSignedHealthIsDeadForEveryDefinedSession() {
        foreach (int health in new[] { int.MinValue, -1, 0 }) {
            // Signed bounds exercise the exact engine comparison without subtraction overflow
            for (int state = 0; state <= 3; state++) {
                TestAssert.Equal(LocalPlayerLife.Dead, NativeLocalPlayerLife.Evaluate(health, state));
            }
        }
    }

    public static void DownedOneAndMaximumHealthKeepEveryNonspectatorSessionAlive() {
        foreach (int health in new[] { 1, int.MaxValue }) {
            // A dead or intermission label alone does not reject the source wrapper
            foreach (int state in new[] { 0, 1, 3 }) {
                TestAssert.Equal(LocalPlayerLife.Alive, NativeLocalPlayerLife.Evaluate(health, state));
            }
            TestAssert.Equal(LocalPlayerLife.Dead, NativeLocalPlayerLife.Evaluate(health, 2));
        }
    }

    public static void UndefinedSessionValuesStayUnknownEvenWithNonpositiveHealth() {
        foreach (int state in new[] { int.MinValue, -1, 4, int.MaxValue }) {
            // A failed or unsupported field observation cannot become authenticated death
            foreach (int health in new[] { int.MinValue, 0, 1, int.MaxValue }) {
                TestAssert.Equal(LocalPlayerLife.Unknown, NativeLocalPlayerLife.Evaluate(health, state));
            }
        }
    }
}
