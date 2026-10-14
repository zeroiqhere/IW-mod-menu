using Apparition.Backend.OpenControls;

namespace Backend.Tests;

internal static class OpeningActionCatalogTests {
    public static void CanonicalIdsAndTokensKeepTheExactThirteenActionSourceOrder() {
        // These literals are the pinned source inventory, independent of the production catalog
        string[] tokens = {
            "+actionslot 1", "+actionslot 2", "+actionslot 3", "+actionslot 4",
            "+melee", "+speed_throw", "+attack", "+breath_sprint", "+activate",
            "+frag", "+smoke", "+stance", "+gostand"
        };
        OpeningAction[] actions = {
            OpeningAction.ActionSlot1, OpeningAction.ActionSlot2, OpeningAction.ActionSlot3, OpeningAction.ActionSlot4,
            OpeningAction.Melee, OpeningAction.Aim, OpeningAction.Attack, OpeningAction.Sprint, OpeningAction.Use,
            OpeningAction.Frag, OpeningAction.SecondaryOffhand, OpeningAction.Stance, OpeningAction.Jump
        };
        TestAssert.Equal(13, OpeningActionCatalog.Items.Count);
        for (int index = 0; index < actions.Length; index++) {
            var entry = OpeningActionCatalog.Items[index];
            TestAssert.Equal(index, (int)entry.Action);
            TestAssert.Equal(actions[index], entry.Action);
            TestAssert.Equal(tokens[index], entry.SourceToken);
            TestAssert.True(OpeningActionCatalog.TryFind(actions[index], out var found), "Every source action must resolve");
            TestAssert.Equal(entry, found);
        }
        TestAssert.True(!OpeningActionCatalog.TryFind((OpeningAction)(-1), out _) &&
            !OpeningActionCatalog.TryFind((OpeningAction)13, out _), "Unknown action IDs must not resolve");
        // A caller cannot rewrite the shared source order
        TestAssert.Throws<NotSupportedException>(() => ((IList<OpeningActionDescriptor>)OpeningActionCatalog.Items)[0] =
            new(OpeningAction.Jump, "changed"));
    }
}
