using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The real owner and inert session expose source instance facts without native drawing
internal static class ChromeThemeControllerTests {
    private static JsonElement Theme(StateDocument frame) {
        using var document = JsonDocument.Parse(frame.Serialize());
        TestAssert.True(document.RootElement.TryGetProperty("chromeTheme", out var theme),
            "An owned actual chrome instance must publish its source theme descriptor");
        return theme.Clone();
    }

    private static MenuChromeFixture Palette() {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        return rig;
    }

    public static void ExistingChromeCapturesActualCreationIdentityStyleAndEmptyJournal() {
        var rig = Palette();
        var frame = rig.Current();
        var theme = Theme(frame);

        TestAssert.True(theme.GetProperty("exists").GetBoolean(), "A completed owned draw must declare existing chrome");
        TestAssert.Equal(frame.MenuChromeRevision!.Value, theme.GetProperty("creationRevision").GetUInt64());
        TestAssert.Equal("Apparition", theme.GetProperty("creationDesign").GetString());
        TestAssert.Equal("#3998FE", theme.GetProperty("creationAccent").GetString());
        TestAssert.True(!theme.GetProperty("creationRainbow").GetBoolean(), "Creation must capture the actual fixed mode");
        TestAssert.Equal(31, theme.GetProperty("currentRoleMask").GetInt32());
        TestAssert.True(theme.GetProperty("prefixComplete").GetBoolean(), "A fresh creation starts with complete callback history");
        TestAssert.Equal(0, theme.GetProperty("events").GetArrayLength());
        rig.NoGameWrites();
    }

    public static void FixedPaletteSelectionsCreateMaskedRequestsIncludingSameColorRainbowStop() {
        var rig = Palette();
        rig.Select("Red");
        var first = Theme(rig.Current()).GetProperty("events")[0];

        TestAssert.Equal(1UL, first.GetProperty("revision").GetUInt64());
        TestAssert.Equal(28, first.GetProperty("targetMask").GetInt32());
        TestAssert.Equal("#FF0000", first.GetProperty("accent").GetString());
        TestAssert.Equal(0UL, first.GetProperty("elapsedMilliseconds").GetUInt64());
        TestAssert.Equal(JsonValueKind.Null, first.GetProperty("predecessorGapMilliseconds").ValueKind);

        rig.Wait(430);
        rig.Send(OverlayInput.Select);
        var repeated = Theme(rig.Current()).GetProperty("events")[1];
        TestAssert.Equal(2UL, repeated.GetProperty("revision").GetUInt64());
        TestAssert.Equal(430, repeated.GetProperty("predecessorGapMilliseconds").GetInt32());

        rig.Wait(430);
        rig.Select("Smooth Rainbow");
        rig.Wait(430);
        rig.Select("Red");
        var stopped = Theme(rig.Current()).GetProperty("events")[2];
        TestAssert.True(stopped.GetProperty("stoppedRainbow").GetBoolean(),
            "A same-color fixed reselect must capture the actual Rainbow stop");
        TestAssert.Equal("#3998FE", Theme(rig.Current()).GetProperty("creationAccent").GetString());
        rig.NoGameWrites();
    }

    public static void ActualCloseRetiresChromeButFocusWithdrawalPreservesPrivateCreation() {
        var rig = Palette();
        rig.Select("Red");
        rig.Wait(430);
        var active = rig.Current();
        ulong revision = Theme(active).GetProperty("creationRevision").GetUInt64();

        using (var focused = JsonDocument.Parse(StateFocusProjection.Apply(active, false).Serialize())) {
            TestAssert.True(!focused.RootElement.TryGetProperty("chromeTheme", out _),
                "Focus withdrawal must omit chrome authority without declaring source destruction");
        }
        TestAssert.Equal(revision, Theme(rig.Current()).GetProperty("creationRevision").GetUInt64());

        rig.Send(OverlayInput.Toggle);
        var closed = Theme(rig.Current());
        TestAssert.True(!closed.GetProperty("exists").GetBoolean(), "Actual close must retire source chrome explicitly");
        TestAssert.Equal(JsonValueKind.Null, closed.GetProperty("creationRevision").ValueKind);
        TestAssert.Equal(0, closed.GetProperty("events").GetArrayLength());

        rig.Open();
        rig.Wait(50);
        TestAssert.True(Theme(rig.Current()).GetProperty("creationRevision").GetUInt64() > revision,
            "Actual reopen must create a distinct source instance even when the closed frame was missed");
        rig.NoGameWrites();
    }
    internal static MenuChromeFixture DesignedPalette(int design) {
        var rig = new MenuChromeFixture();
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        for (int choice = 0; choice < design; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(1000);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        return rig;
    }

    public static void AllFiveDesignsCaptureTheirSourceMembershipAndFixedInvocationMasks() {
        string[] designs = { "Apparition", "Classic", "Native", "AIO", "Basic" };
        int[] members = { 31, 7, 15, 29, 7 };
        int[] fixedMasks = { 28, 3, 3, 25, 4 };
        for (int index = 0; index < designs.Length; index++) {
            var rig = DesignedPalette(index);
            var created = Theme(rig.Current());
            TestAssert.Equal(designs[index], created.GetProperty("creationDesign").GetString());
            TestAssert.Equal(members[index], created.GetProperty("currentRoleMask").GetInt32());

            rig.Select("Black");
            var request = Theme(rig.Current()).GetProperty("events")[0];
            TestAssert.Equal(fixedMasks[index], request.GetProperty("targetMask").GetInt32());
            TestAssert.Equal("#000000", request.GetProperty("accent").GetString());
            rig.Wait(180);
            TestAssert.Equal(created.GetProperty("creationRevision").GetUInt64(),
                Theme(rig.Current()).GetProperty("creationRevision").GetUInt64());
            rig.NoGameWrites();
        }
    }

    public static void DesignLockPreventsOldCreationCallbacksAndAdmitsOnlyTheRecreatedMask() {
        var rig = Palette();
        rig.Send(OverlayInput.Back);
        rig.Select("Menu Preferences");
        rig.Highlight("Design");
        rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        ulong before = rig.Controller.ChromeThemeCounter;
        TestAssert.True(!Theme(rig.Current()).GetProperty("exists").GetBoolean(), "Changed Design destroys the old chrome immediately");
        rig.Send(OverlayInput.Select);
        rig.Wait(49);
        TestAssert.True(!Theme(rig.Current()).GetProperty("exists").GetBoolean(), "No chrome exists before the actual 50 ms redraw");
        rig.Send(OverlayInput.Select);
        rig.Wait(1);
        var recreated = Theme(rig.Current());
        TestAssert.Equal("Classic", recreated.GetProperty("creationDesign").GetString());
        rig.Send(OverlayInput.Select);
        TestAssert.Equal(before, rig.Controller.ChromeThemeCounter);
        rig.Wait(200);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        rig.Select("Red");
        TestAssert.Equal(3, Theme(rig.Current()).GetProperty("events")[0].GetProperty("targetMask").GetInt32());
        rig.NoGameWrites();
    }

    public static void WidthRetiresOnlyTitleAndUnlockRollbackKeepsTheActualNewCreation() {
        var rig = Palette();
        rig.Select("Red");
        rig.Wait(430);
        ulong original = Theme(rig.Current()).GetProperty("creationRevision").GetUInt64();
        rig.Send(OverlayInput.Back);
        rig.Select("Width Editor");
        var width = Theme(rig.Current());
        TestAssert.Equal(original, width.GetProperty("creationRevision").GetUInt64());
        TestAssert.Equal(27, width.GetProperty("currentRoleMask").GetInt32());
        TestAssert.Equal(28, width.GetProperty("events")[0].GetProperty("targetMask").GetInt32());

        rig.Send(OverlayInput.FinishWidthEditor);
        TestAssert.True(!Theme(rig.Current()).GetProperty("exists").GetBoolean(), "SoftUnlock destroys all old chrome before recreation");
        rig.Wait(50);
        var created = Theme(rig.Current());
        TestAssert.Equal(original + 1, created.GetProperty("creationRevision").GetUInt64());
        TestAssert.Equal(31, created.GetProperty("currentRoleMask").GetInt32());
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(49));
        var recovered = rig.Current();
        TestAssert.Equal("rebuild", recovered.MenuUnlock);
        TestAssert.Equal(original + 1, Theme(recovered).GetProperty("creationRevision").GetUInt64());
        rig.Wait(50);
        TestAssert.Equal(original + 1, Theme(rig.Current()).GetProperty("creationRevision").GetUInt64());
        rig.NoGameWrites();
    }

    public static void HiddenModalRowsAndOwnerReplacementKeepTheirSeparateChromeLifetimes() {
        foreach (string page in new[] { "Menu Credits", "Reposition Menu", "Menu Instructions" }) {
            var rig = Palette();
            rig.Select("Red");
            rig.Wait(430);
            ulong parent = Theme(rig.Current()).GetProperty("creationRevision").GetUInt64();
            rig.Send(OverlayInput.Back);
            rig.Select(page);
            if (page == "Menu Instructions") rig.Select("Reposition");
            TestAssert.Equal(parent, Theme(rig.Current()).GetProperty("creationRevision").GetUInt64());
            TestAssert.Equal(31, Theme(rig.Current()).GetProperty("currentRoleMask").GetInt32());
            TestAssert.Equal(1, Theme(rig.Current()).GetProperty("events").GetArrayLength());
            rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive, replaceOwner: true);
            TestAssert.True(!Theme(rig.Current()).GetProperty("exists").GetBoolean(), "Another actor cannot adopt old source chrome history");
            rig.NoGameWrites();
        }
    }

    public static void RepeatedOverlapsAndClockRecoveryNeverInventACompleteCreationHistory() {
        var rig = Palette();
        for (int invocation = 0; invocation < 9; invocation++) {
            rig.Select("Red");
            if (invocation < 8) rig.Wait(430);
        }
        var tail = Theme(rig.Current());
        TestAssert.Equal(8, tail.GetProperty("events").GetArrayLength());
        TestAssert.True(!tail.GetProperty("prefixComplete").GetBoolean(), "Pruning must disclose the missing callback prefix");
        TestAssert.Equal(2UL, tail.GetProperty("events")[0].GetProperty("revision").GetUInt64());
        TestAssert.Equal(3010UL, tail.GetProperty("events")[0].GetProperty("elapsedMilliseconds").GetUInt64());
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
        var recovered = Theme(rig.Current());
        TestAssert.True(!recovered.GetProperty("prefixComplete").GetBoolean(), "Clock recovery cannot reset the same creation to complete");
        TestAssert.Equal(0, recovered.GetProperty("events").GetArrayLength());
        rig.Wait(430);
        rig.Select("Red");
        TestAssert.True(!Theme(rig.Current()).GetProperty("prefixComplete").GetBoolean(), "A later accepted callback cannot restore a lost prefix");
        rig.NoGameWrites();
    }

    public static void ExhaustedInvocationCounterRejectsBeforePaletteModeOrCallbackMutation() {
        var rig = new MenuChromeFixture(themeRevision: ulong.MaxValue);
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Highlight("Red");
        var before = rig.Current();
        TestAssert.Throws<OverflowException>(() => rig.Send(OverlayInput.Select));
        var after = rig.Current();
        TestAssert.Equal(before.Accent, after.Accent);
        TestAssert.Equal(before.SmoothRainbowTheme, after.SmoothRainbowTheme);
        TestAssert.True(after.MenuOptionCallback is null, "A failed invocation cannot arm a caller wait");
        TestAssert.Equal(0, Theme(after).GetProperty("events").GetArrayLength());
        TestAssert.Equal(0UL, rig.Controller.InstructionThemeCounter);
        TestAssert.Equal(0, after.InstructionTheme!.Events.Count);
        rig.NoGameWrites();
    }

    public static void FailedClockAndFreshSnapshotCannotSpendPaletteOrReservedChromeCreation() {
        var clock = new FailingClock();
        var rig = new MenuChromeFixture(clock: clock);
        rig.SettleMain();
        rig.Select("Menu Customization");
        rig.Select("Main Design Color");
        rig.Highlight("Red");
        var before = rig.Current();
        clock.Fail = true;
        TestAssert.Throws<InvalidOperationException>(() => rig.Send(OverlayInput.Select));
        clock.Fail = false;
        TestAssert.Equal(before.Accent, rig.Current().Accent);
        TestAssert.Equal(0UL, rig.Controller.ChromeThemeCounter);
        TestAssert.Equal(0, rig.Current().InstructionTheme!.Events.Count);

        // A fresh actor roster failure at actual draw cannot commit the prepared header
        var navigation = new OverlayNavigation(0, ulong.MaxValue - 3);
        navigation.OpenPage(MenuPage.Players);
        var rows = new MenuChromeFixture(navigation: navigation);
        rows.Open();
        var original = rows.Session.State;
        rows.Session.State = original with { Players = new[] { new OverlayPlayer(1, "Renamed friend") } };
        rows.Clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.Throws<OverflowException>(() => rows.Current());
        TestAssert.Equal(0UL, rows.Controller.CommittedChromeRevision);
        rows.Session.State = original;
        TestAssert.Equal<ulong?>(1, rows.Current().ChromeTheme!.CreationRevision);
        TestAssert.Equal(0, rows.Current().ChromeTheme!.Events.Count);
        // Inject only malformed captured schema data, without adding a production-only test hook
        var rejected = new MenuChromeFixture();
        rejected.Open();
        rejected.Clock.Advance(TimeSpan.FromMilliseconds(50));
        var accentField = typeof(Apparition.Backend.Controller.BackendController).GetField("accent",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        accentField.SetValue(rejected.Controller, "#GG0000");
        TestAssert.Throws<InvalidOperationException>(() => rejected.Current());
        TestAssert.Equal(0UL, rejected.Controller.CommittedChromeRevision);
        accentField.SetValue(rejected.Controller, "#3998FE");
        var retry = rejected.Current();
        TestAssert.True(retry.ChromeTheme!.Exists, "Schema failure cannot consume the only reserved actual draw");
        TestAssert.Equal<ulong?>(1, retry.ChromeTheme.CreationRevision);
        TestAssert.Equal(0, retry.ChromeTheme.Events.Count);
        TestAssert.Equal<ulong?>(1, retry.InstructionHud!.CreationRevision);
        rejected.NoGameWrites();
        rows.NoGameWrites();
        rig.NoGameWrites();
    }

    private sealed class FailingClock : TimeProvider, IManualClockFixture {
        public ManualTimeProvider Time { get; } = new();
        public bool Fail { get; set; }
        public override long TimestampFrequency => Time.TimestampFrequency;
        public override long GetTimestamp() => Fail ? throw new InvalidOperationException("Owned clock unavailable") : Time.GetTimestamp();
    }

    public static void TemporaryUnknownLifeWithdrawsAuthorityWithoutRetiringOwnedCreationHistory() {
        var rig = Palette();
        rig.Select("Red");
        rig.Wait(180);
        var before = rig.Current().ChromeTheme!;
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Unknown);
        var withdrawn = rig.Current();
        TestAssert.True(withdrawn.IsOpen && withdrawn.ChromeTheme is null,
            "Same-owner unknown life withdraws chrome authority without declaring source close");
        TestAssert.Equal(before.CreationRevision, withdrawn.MenuChromeRevision);
        rig.Wait(250);
        rig.Session.Change(syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive);
        var restored = rig.Current();
        TestAssert.Equal(before.CreationRevision, restored.ChromeTheme!.CreationRevision);
        TestAssert.Equal(before.CreationAccent, restored.ChromeTheme.CreationAccent);
        TestAssert.Equal(before.Events[0].Revision, restored.ChromeTheme.Events[0].Revision);
        TestAssert.Equal(430UL, restored.ChromeTheme.Events[0].ElapsedMilliseconds);
        TestAssert.Equal(restored.LocalMenuEligibility!.Revision, restored.ChromeTheme.AdmissionRevision!.Value);
        rig.NoGameWrites();
    }

}
