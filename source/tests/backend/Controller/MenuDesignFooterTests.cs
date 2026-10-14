using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The descriptor separates live placement from existing footer paint without another timeline
internal static class MenuDesignFooterTests
{
    internal static string? FooterDesign(StateDocument state)
    {
        using var json = JsonDocument.Parse(state.Serialize());
        var transition = json.RootElement.GetProperty("menuDesignTransition");
        return transition.ValueKind == JsonValueKind.Object && transition.TryGetProperty("footerDesign", out var design)
            ? design.GetString() : null;
    }

    public static void ChangedDesignPublishesItsCommittedFooterOnlyUntilTheExistingFiftyMillisecondRedraw()
    {
        foreach (string target in MenuDesignTests.Names.Skip(1))
        {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Stage(target);
            var before = rig.Current();
            rig.Send(OverlayInput.Select);

            foreach (int elapsed in new[] { 0, 49 })
            {
                if (elapsed > 0)
                    rig.Clock.Advance(TimeSpan.FromMilliseconds(elapsed));
                var gap = rig.Current();
                TestAssert.Equal(target, FooterDesign(gap));
                TestAssert.Equal("Apparition", gap.Appearance!.Design);
                TestAssert.Equal(before.MenuRevision, gap.MenuRevision);
                TestAssert.Equal(before.NavigationRevision, gap.NavigationRevision);
                TestAssert.Equal(target, gap.Rows[0].Value);
            }

            rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var drawn = rig.Current();
            TestAssert.True(FooterDesign(drawn) is null, "Redraw removes the descriptor without a second presentation event");
            TestAssert.Equal(target, drawn.Appearance!.Design);
            TestAssert.Equal(before.MenuRevision + 1, drawn.MenuRevision);
            TestAssert.Equal(before.NavigationRevision, drawn.NavigationRevision);
            rig.NoGameWork();
        }
        // Returning to the source default is also a real change rather than an omitted descriptor
        var returning = new MenuDesignTests.Rig();
        returning.Open();
        returning.Stage("Classic");
        returning.Send(OverlayInput.Select);
        returning.Clock.Advance(TimeSpan.FromMilliseconds(250));
        returning.Current();
        returning.Stage("Apparition");
        returning.Send(OverlayInput.Select);
        TestAssert.Equal("Apparition", FooterDesign(returning.Current()));
        TestAssert.Equal("Classic", returning.Current().Appearance!.Design);
        returning.NoGameWork();
    }

    public static void DraftSameDesignFocusShutdownAndClockRecoveryPreserveTheExistingTransitionOwnership()
    {
        var draft = new MenuDesignTests.Rig();
        draft.Open();
        draft.Stage("AIO");
        TestAssert.True(FooterDesign(draft.Current()) is null, "A candidate is not a committed footer design");
        draft.Stage("Apparition");
        draft.Send(OverlayInput.Select);
        TestAssert.True(FooterDesign(draft.Current()) is null, "Same-design cooldown has no geometry descriptor");

        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Stage("AIO");
        rig.Send(OverlayInput.Select);
        var gap = rig.Current();
        TestAssert.Equal("AIO", FooterDesign(gap));
        var hidden = StateFocusProjection.Apply(gap, false);
        TestAssert.True(hidden.MenuDesignTransition is null, "Focus hiding clears the entire display transition");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(25));
        rig.Current();
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        var recovered = rig.Current();
        TestAssert.Equal("AIO", FooterDesign(recovered));
        TestAssert.Equal(gap.MenuRevision, recovered.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        var drawn = rig.Current();
        TestAssert.True(FooterDesign(drawn) is null, "Recovery retains the existing rebuild deadline only");
        rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(10));
        TestAssert.True(FooterDesign(rig.Current()) is null, "A completed rebuild cannot reintroduce captured geometry");
        rig.Controller.RequestShutdown();
        var closed = rig.Controller.ClosedState();
        TestAssert.True(closed.MenuDesignTransition is null, "Cleanup cancels the descriptor together with its source transition");
        TestAssert.Equal("AIO", closed.Appearance!.Design);
        rig.NoGameWork();
    }
}
