using System.Collections;
using syndishanx_iw_tool.Features.PlayerAmmo;

namespace Session.Tests;

// These tests use semantic clip facts only and never link a native transport
public static class PlayerAmmoRefillPlanTests {
    public static Task ModesKeepTheExactContinuousReloadDisableSourceOrder() {
        Equal(0, (int)PlayerAmmoMode.Continuous);
        Equal(1, (int)PlayerAmmoMode.Reload);
        Equal(2, (int)PlayerAmmoMode.Disable);
        Equal("Continuous,Reload,Disable", string.Join(",", Enum.GetNames<PlayerAmmoMode>()));
        return Task.CompletedTask;
    }

    public static Task ContinuousRefillsReserveBeforeTheSourceDefaultClip() {
        var plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new[] { new VerifiedClipCapacity(PlayerAmmoHand.SourceDefault, 30) });

        Equal(PlayerAmmoMode.Continuous, plan.Mode);
        Equal(2, plan.Operations.Count);
        Reserve(plan.Operations[0]);
        Clip(plan.Operations[1], PlayerAmmoHand.SourceDefault, 30);
        return Task.CompletedTask;
    }

    public static Task ContinuousKeepsLeftAndRightCapacitiesSeparateInSuppliedOrder() {
        // Unequal capacities expose accidental hand merging or hardcoded clip sizes
        var plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new[] { new VerifiedClipCapacity(PlayerAmmoHand.Right, 12),
                new VerifiedClipCapacity(PlayerAmmoHand.Left, 7) });

        Equal(3, plan.Operations.Count);
        Reserve(plan.Operations[0]);
        Clip(plan.Operations[1], PlayerAmmoHand.Right, 12);
        Clip(plan.Operations[2], PlayerAmmoHand.Left, 7);
        return Task.CompletedTask;
    }

    public static Task ReloadRefillsOnlyReserveWithoutReadingClipFacts() {
        var plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Reload, true, new UnreadableClipFacts());
        var withoutFacts = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Reload, true);

        Equal(1, plan.Operations.Count);
        Equal(1, withoutFacts.Operations.Count);
        Reserve(plan.Operations[0]);
        Reserve(withoutFacts.Operations[0]);
        return Task.CompletedTask;
    }

    public static Task DisableAndMissingWeaponProduceNoOperationsWithoutReadingClipFacts() {
        // Disable stops intent without restoring any earlier ammo counter
        var disabled = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Disable, true, new UnreadableClipFacts());
        Equal(0, disabled.Operations.Count);
        foreach (var mode in Enum.GetValues<PlayerAmmoMode>()) {
            var missingWeapon = PlayerAmmoRefillPlan.Create(mode, false, new UnreadableClipFacts());
            Equal(0, missingWeapon.Operations.Count);
        }
        return Task.CompletedTask;
    }

    public static Task ZeroCapacityAndAbsentClipFactsKeepContinuousReserveIntent() {
        var noFacts = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true);
        var zero = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new[] { new VerifiedClipCapacity(PlayerAmmoHand.SourceDefault, 0) });
        var oneUsableHand = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new[] { new VerifiedClipCapacity(PlayerAmmoHand.Left, 0),
                new VerifiedClipCapacity(PlayerAmmoHand.Right, 9) });

        Equal(1, noFacts.Operations.Count);
        Equal(1, zero.Operations.Count);
        Reserve(noFacts.Operations[0]);
        Reserve(zero.Operations[0]);
        Equal(2, oneUsableHand.Operations.Count);
        Clip(oneUsableHand.Operations[1], PlayerAmmoHand.Right, 9);
        return Task.CompletedTask;
    }

    public static Task SourceDefaultCannotBeMixedWithEitherExplicitHand() {
        foreach (var hand in new[] { PlayerAmmoHand.Left, PlayerAmmoHand.Right }) {
            Throws<ArgumentException>(() => PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
                new[] { new VerifiedClipCapacity(PlayerAmmoHand.SourceDefault, 0),
                    new VerifiedClipCapacity(hand, 10) }));
            Throws<ArgumentException>(() => PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
                new[] { new VerifiedClipCapacity(hand, 0),
                    new VerifiedClipCapacity(PlayerAmmoHand.SourceDefault, 10) }));
        }
        return Task.CompletedTask;
    }

    public static Task DuplicateHandsAreRejectedEvenWhenTheirCapacitiesAreZero() {
        foreach (var hand in Enum.GetValues<PlayerAmmoHand>()) {
            Throws<ArgumentException>(() => PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
                new[] { new VerifiedClipCapacity(hand, 0), new VerifiedClipCapacity(hand, 10) }));
            Throws<ArgumentException>(() => PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
                new[] { new VerifiedClipCapacity(hand, 0), new VerifiedClipCapacity(hand, 0) }));
        }
        return Task.CompletedTask;
    }

    public static Task InvalidModesHandsAndNegativeCapacitiesRejectWithoutInventingAnUpperLimit() {
        foreach (int value in new[] { -1, 3, int.MaxValue }) {
            Throws<ArgumentOutOfRangeException>(() => PlayerAmmoRefillPlan.Create((PlayerAmmoMode)value, false));
            Throws<ArgumentOutOfRangeException>(() => new VerifiedClipCapacity((PlayerAmmoHand)value, 1));
        }
        Throws<ArgumentOutOfRangeException>(() => new VerifiedClipCapacity(PlayerAmmoHand.Left, -1));

        // Int32 capacity is preserved directly, without guessed game maxima or arithmetic
        var maximum = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new[] { new VerifiedClipCapacity(PlayerAmmoHand.Right, int.MaxValue) });
        Clip(maximum.Operations[1], PlayerAmmoHand.Right, int.MaxValue);
        return Task.CompletedTask;
    }

    public static Task PlansOwnReadOnlyCopiesThatCannotChangeWithInputStorage() {
        var input = new List<VerifiedClipCapacity> { new(PlayerAmmoHand.Left, 7), new(PlayerAmmoHand.Right, 12) };
        var plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true, input);

        input.Clear();
        input.Add(new(PlayerAmmoHand.SourceDefault, 90));

        Equal(3, plan.Operations.Count);
        Clip(plan.Operations[1], PlayerAmmoHand.Left, 7);
        Clip(plan.Operations[2], PlayerAmmoHand.Right, 12);
        var storage = (IList<PlayerAmmoRefillOperation>)plan.Operations;
        Throws<NotSupportedException>(() => storage.Clear());
        return Task.CompletedTask;
    }

    public static Task NullFactsAndEnumerationFailureCannotReturnAPartialPlan() {
        PlayerAmmoRefillPlan? plan = null;
        Throws<ArgumentException>(() => plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true,
            new VerifiedClipCapacity[] { new(PlayerAmmoHand.Left, 7), null! }));
        Equal<PlayerAmmoRefillPlan?>(null, plan);

        Throws<IOException>(() => plan = PlayerAmmoRefillPlan.Create(PlayerAmmoMode.Continuous, true, FactsThatFail()));
        Equal<PlayerAmmoRefillPlan?>(null, plan);
        return Task.CompletedTask;
    }

    // A failure after one hand must not expose a half-validated plan
    private static IEnumerable<VerifiedClipCapacity> FactsThatFail() {
        yield return new(PlayerAmmoHand.Left, 7);
        throw new IOException("Owned clip fact source failed");
    }

    private static void Reserve(PlayerAmmoRefillOperation operation) {
        Equal(PlayerAmmoRefillOperationKind.RefillReserve, operation.Kind);
        Equal<PlayerAmmoHand?>(null, operation.Hand);
        Equal<int?>(null, operation.Capacity);
    }

    private static void Clip(PlayerAmmoRefillOperation operation, PlayerAmmoHand hand, int capacity) {
        Equal(PlayerAmmoRefillOperationKind.FillClip, operation.Kind);
        Equal<PlayerAmmoHand?>(hand, operation.Hand);
        Equal<int?>(capacity, operation.Capacity);
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; received {actual}");
    }

    private static void Throws<T>(Action action) where T : Exception {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }

    // Reload and stopped modes must not consult clip facts that they do not need
    private sealed class UnreadableClipFacts : IEnumerable<VerifiedClipCapacity> {
        public IEnumerator<VerifiedClipCapacity> GetEnumerator() => throw new InvalidOperationException("Clip facts were read");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
