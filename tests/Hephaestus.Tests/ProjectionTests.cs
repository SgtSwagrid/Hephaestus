using System.Collections.Immutable;

namespace Hephaestus.Tests;

/// <summary>The typed layer, exercised with the base class library's time types so that no extra package is needed.</summary>
public sealed class ProjectionTests {
    private static readonly DateTime Origin = new(2026, 9, 19, 8, 0, 0);
    private static readonly Point<DateTime, TimeSpan> Start = Variable.DateTime("start", Origin);
    private static readonly Point<DateTime, TimeSpan> Finish = Variable.DateTime("finish", Origin);
    private static readonly Quantity<TimeSpan> Runtime = Variable.TimeSpan("runtime");
    private static readonly ContinuousVariable StartSeconds = Variable.Continuous("start");
    private static readonly ContinuousVariable FinishSeconds = Variable.Continuous("finish");
    private static readonly ContinuousVariable RuntimeSeconds = Variable.Continuous("runtime");

    private static void AssertSameConstraint(IBooleanExpression expected, IBooleanExpression actual) =>
        Assert.Equal(expected.Normalise(1e-4), actual.Normalise(1e-4));

    [Fact]
    public void TheDifferenceOfTwoPointsIsAQuantity() {
        Quantity<TimeSpan> runTime = Finish - Start;

        AssertSameConstraint(FinishSeconds - StartSeconds >= 300, runTime >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void APointShiftedByAQuantityIsAPoint() {
        Point<DateTime, TimeSpan> earliestFinish = Start + Runtime + TimeSpan.FromMinutes(5);

        AssertSameConstraint(StartSeconds + RuntimeSeconds + 300 <= FinishSeconds, earliestFinish <= Finish);
        AssertSameConstraint(StartSeconds - RuntimeSeconds - 300 <= FinishSeconds, Start - Runtime - TimeSpan.FromMinutes(5) <= Finish);
    }

    [Fact]
    public void APlainValueShiftedByAQuantityIsAPointAnchoredAtThatValue() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1));

        AssertSameConstraint(StartSeconds >= 600 + RuntimeSeconds, Start >= Origin.AddMinutes(10) + Runtime);
        AssertSameConstraint(StartSeconds >= 600 + RuntimeSeconds, Start >= Runtime + Origin.AddMinutes(10));
        AssertSameConstraint(StartSeconds <= 600 - RuntimeSeconds, Start <= Origin.AddMinutes(10) - Runtime);
        AssertSameConstraint(StartSeconds >= 600 + 60 * Variable.Continuous("minutes") + RuntimeSeconds, Start >= Origin.AddMinutes(10) + minutes + Runtime);
        Assert.Equal(new DateTimeProjection(Origin.AddMinutes(10), TimeSpan.FromMinutes(1)), (Origin.AddMinutes(10) + minutes).Projection);
        Assert.Equal(Runtime.Expression, (Origin + Runtime).Expression);
    }

    [Fact]
    public void APlainDeltaMayComeFirst() =>
        AssertSameConstraint(StartSeconds + 300 <= FinishSeconds, TimeSpan.FromMinutes(5) + Start <= Finish);

    [Fact]
    public void BoundsMayBePlainValuesOrExpressions() {
        AssertSameConstraint((StartSeconds <= FinishSeconds) & (FinishSeconds <= StartSeconds + 600), Finish.Between(Start, Start + TimeSpan.FromMinutes(10)));
        AssertSameConstraint((0 <= FinishSeconds) & (FinishSeconds <= StartSeconds), Finish.Between(Origin, Start));
        AssertSameConstraint((StartSeconds <= FinishSeconds) & (FinishSeconds <= 3600), Finish.Between(Start, Origin.AddHours(1)));
        AssertSameConstraint((30 <= RuntimeSeconds) & (RuntimeSeconds <= FinishSeconds - StartSeconds), Runtime.Between(TimeSpan.FromSeconds(30), Finish - Start));
        AssertSameConstraint((FinishSeconds - StartSeconds <= RuntimeSeconds) & (RuntimeSeconds <= 300), Runtime.Between(Finish - Start, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void AQuantityCanBeCountedInAPlainUnit() =>
        Assert.Equal((RuntimeSeconds / 60).Normalise(), Runtime.In(TimeSpan.FromMinutes(1)).Normalise());

    [Fact]
    public void PointsCompareWithPlainValues() {
        AssertSameConstraint(StartSeconds >= 600, Start >= Origin.AddMinutes(10));
        AssertSameConstraint(StartSeconds.Between(0, 3600), Start.Between(Origin, Origin.AddHours(1)));
        AssertSameConstraint(StartSeconds.EqualTo(60), Start.EqualTo(Origin.AddMinutes(1)));
        AssertSameConstraint((FinishSeconds - 900).EqualTo(0), (Origin.AddMinutes(15) - Finish).EqualTo(TimeSpan.Zero));
    }

    [Fact]
    public void QuantitiesFormAVectorSpace() {
        AssertSameConstraint(2 * RuntimeSeconds + 30 <= 120, 2 * Runtime + TimeSpan.FromSeconds(30) <= TimeSpan.FromMinutes(2));
        AssertSameConstraint(-RuntimeSeconds / 2 >= -60, -Runtime / 2 >= TimeSpan.FromMinutes(-1));
        AssertSameConstraint(60 - RuntimeSeconds > 0, TimeSpan.FromMinutes(1) - Runtime > TimeSpan.Zero);
    }

    [Fact]
    public void QuantitiesInDifferentUnitsAreReconciled() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1), inWholeUnits: true);

        AssertSameConstraint(RuntimeSeconds + 60 * Variable.Integer("minutes") <= 600, Runtime + minutes <= TimeSpan.FromMinutes(10));
        AssertSameConstraint(Variable.Integer("minutes") + RuntimeSeconds / 60 <= 10, minutes + Runtime <= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void PointsWithDifferentOriginsAreReconciled() {
        var later = Variable.DateTime("later", Origin.AddHours(1));

        AssertSameConstraint(StartSeconds <= Variable.Continuous("later") + 3600, Start <= later);
        AssertSameConstraint((Variable.Continuous("later") + 3600 - StartSeconds).EqualTo(60), (later - Start).EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AQuantityCanBeMeasuredInAnotherUnitForUseInAPlainExpression() =>
        Assert.Equal((RuntimeSeconds / 60).Normalise(), Runtime.In(new TimeSpanProjection(TimeSpan.FromMinutes(1))).Normalise());

    [Fact]
    public void QuantitiesSumUnderTheProjectionOfTheFirst() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1));

        Assert.Equal((RuntimeSeconds + 60 * Variable.Continuous("minutes")).Normalise(), new[] { Runtime, minutes }.Sum().Expression.Normalise());
        Assert.Throws<InvalidOperationException>(() => Array.Empty<Quantity<TimeSpan>>().Sum());
    }

    [Fact]
    public void SolutionsAreReadBackInTheDomainType() {
        var solution = new Solution(
            ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer).Add(StartSeconds, 90).Add(FinishSeconds, 600).Add(RuntimeSeconds, 45),
            0);

        Assert.Equal(Origin.AddSeconds(90), solution.Value(Start));
        Assert.Equal(TimeSpan.FromSeconds(45), solution.Value(Runtime));
        Assert.Equal(TimeSpan.FromSeconds(510), solution.Value(Finish - Start));
        Assert.Equal(Origin.AddSeconds(135), solution.Value(Start + Runtime));
    }

    [Fact]
    public void TypedObjectivesUnwrapToTheirUnderlyingExpressions() {
        Assert.Equal(new SingleObjectiveProblem(Objective.Minimise(Runtime.Expression), BooleanConstant.True), Problem.Minimise(Runtime).SubjectTo(BooleanConstant.True));
        Assert.Equal(new SingleObjectiveProblem(Objective.Maximise(Start.Expression), BooleanConstant.True), Problem.Maximise(Start).SubjectTo(BooleanConstant.True));
    }

    /// <summary>A type of a modeller's own, projected onto the number line, with an algebra of its own and nothing inherited.</summary>
    private sealed record Money(ILinearExpression Expression, IProjection<decimal> Projection) : ILinearlyEncodable<decimal>;

    [Fact]
    public void ATypeOfYourOwnIsComparedReadAndOptimisedLikeTheBuiltInOnes() {
        var cost = new Money(Variable.Continuous("cost"), new RealNumberProjection<decimal>());

        // Comparison, against another of its kind and against a plain value.
        Assert.Equal("cost <= 100", (cost <= 100m).Format());
        Assert.Equal("cost >= budget", (cost >= new Money(Variable.Continuous("budget"), new RealNumberProjection<decimal>())).Format());
        Assert.Equal("cost == 5", cost.EqualTo(5m).Format());
        Assert.Equal("(0 <= cost) & (cost <= 9)", cost.Between(0m, 9m).Format());

        // Reading, and optimising.
        Assert.Equal(12.5m, Solution.Empty.With(cost, 12.5m).Value(cost));
        Assert.Equal(new Optimisation(ObjectiveSense.Minimise, cost.Expression), Objective.Minimise(cost));
    }

    [Fact]
    public void AmountsAndPositionsShareOneComparison() {
        // Both reach the same operators through ILinearlyEncodable, so neither carries its own.
        Assert.Equal("start <= 60", (Start <= Origin.AddMinutes(1)).Format());
        Assert.Equal("runtime <= 60", (Runtime <= TimeSpan.FromMinutes(1)).Format());
    }

    [Fact]
    public void DateTimeOffsetsBehaveLikeDateTimes() {
        var origin = new DateTimeOffset(Origin, TimeSpan.FromHours(10));
        var moment = Variable.DateTimeOffset("moment", origin, unit: TimeSpan.FromMinutes(1));

        AssertSameConstraint(Variable.Continuous("moment") >= 30, moment >= origin.AddMinutes(30));
        Assert.Equal(origin.AddMinutes(2), new DateTimeOffsetProjection(origin, TimeSpan.FromMinutes(1)).Decode(2));
    }
}
