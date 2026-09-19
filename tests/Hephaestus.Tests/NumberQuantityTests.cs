using static Hephaestus.Piecewise;

namespace Hephaestus.Tests;

/// <summary>Plain numbers as typed quantities: they read back in their own type, and kinds of number do not mix.</summary>
public sealed class NumberQuantityTests {
    private static readonly Quantity<int> Trains = Variable.Integer<int>("trains");
    private static readonly Quantity<int> Platforms = Variable.Integer<int>("platforms");
    private static readonly Quantity<long> Passengers = Variable.Integer<long>("passengers");
    private static readonly Quantity<decimal> Cost = Variable.Continuous<decimal>("cost");
    private static readonly IntegerVariable TrainsUnderneath = Variable.Integer("trains");
    private static readonly IntegerVariable PlatformsUnderneath = Variable.Integer("platforms");

    private static void AssertSameConstraint(IBooleanExpression expected, IBooleanExpression actual) =>
        Assert.Equal(expected.Format(), actual.Format());

    [Fact]
    public void TheyAreOrdinaryVariablesUnderneath() {
        Assert.Equal(TrainsUnderneath, Trains.Expression);
        Assert.Equal(Variable.Continuous("cost"), Cost.Expression);
        Assert.Equal(new WholeNumberProjection<int>(), Trains.Projection);
        Assert.IsType<IntegerVariable>(Problem.Minimise(Trains).SubjectTo(Trains >= 2).Encode().Columns.Single().Variable);
    }

    [Fact]
    public void TheyCombineWithEachOtherAndWithPlainNumbersOfTheirOwnType() {
        AssertSameConstraint(TrainsUnderneath + 2 <= PlatformsUnderneath, Trains + 2 <= Platforms);
        AssertSameConstraint(3 <= TrainsUnderneath, 3 <= Trains);
        AssertSameConstraint(2 * TrainsUnderneath - PlatformsUnderneath >= 1, 2 * Trains - Platforms >= 1);
        AssertSameConstraint((0 <= TrainsUnderneath) & (TrainsUnderneath <= 12), Trains.Between(0, 12));
        AssertSameConstraint(TrainsUnderneath.EqualTo(4), Trains.EqualTo(4));
        AssertSameConstraint(Variable.Continuous("cost") <= 99.5, Cost <= 99.5m);
        AssertSameConstraint(Variable.Integer("passengers") >= 5000000000, Passengers >= 5_000_000_000L);
    }

    [Fact]
    public void TheyAreReadBackInTheirOwnTypeAndWholeNumbersAreMadeWhole() {
        var solution = new Solution(Solution.Empty.Values.Add(TrainsUnderneath, 2.9999999).Add(PlatformsUnderneath, 4).Add(Variable.Integer("passengers"), 5e9).Add(Variable.Continuous("cost"), 12.35), 0);

        Assert.Equal(3, solution.Value(Trains));
        Assert.Equal(7, solution.Value(Trains + Platforms));
        Assert.Equal(4, solution.Value(Max(Trains, Platforms)));
        Assert.Equal(1, solution.Value(Abs(Trains - Platforms)));
        Assert.Equal(5_000_000_000L, solution.Value(Passengers));
        Assert.Equal(12.35m, solution.Value(Cost));
        Assert.Equal(24.7m, solution.Value(2 * Cost));
    }

    [Fact]
    public void TheyTakeTheirPlaceInStartsObjectivesAndSums() {
        Assert.Equal(6, Solution.Empty.With(Trains, 6).Values[TrainsUnderneath]);
        Assert.Equal(new Prioritised(Objective.Minimise(TrainsUnderneath), 2), Objective.Minimise(Trains, tolerance: 2));
        Assert.Equal((TrainsUnderneath + PlatformsUnderneath).Normalise(), new[] { Trains, Platforms }.Sum().Expression.Normalise());
        Assert.Equal((TrainsUnderneath / 4).Normalise(), Trains.In(4).Normalise());
    }
}
