using System.Collections.Immutable;

namespace Hephaestus.Tests;

/// <summary>The typed layer, exercised with the base class library's time types so that no extra package is needed.</summary>
public sealed class ProjectionTests {
    private static readonly DateTime Origin = new(2026, 9, 19, 8, 0, 0);
    private static readonly Point<DateTime, TimeSpan> Departure = Variable.DateTime("departure", Origin);
    private static readonly Point<DateTime, TimeSpan> Arrival = Variable.DateTime("arrival", Origin);
    private static readonly Quantity<TimeSpan> Dwell = Variable.TimeSpan("dwell");
    private static readonly ContinuousVariable DepartureSeconds = Variable.Continuous("departure");
    private static readonly ContinuousVariable ArrivalSeconds = Variable.Continuous("arrival");
    private static readonly ContinuousVariable DwellSeconds = Variable.Continuous("dwell");

    private static void AssertSameConstraint(IBooleanExpression expected, IBooleanExpression actual) =>
        Assert.Equal(expected.Normalise(1e-4), actual.Normalise(1e-4));

    [Fact]
    public void TheDifferenceOfTwoPointsIsAQuantity() {
        Quantity<TimeSpan> runTime = Arrival - Departure;

        AssertSameConstraint(ArrivalSeconds - DepartureSeconds >= 300, runTime >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void APointShiftedByAQuantityIsAPoint() {
        Point<DateTime, TimeSpan> earliestArrival = Departure + Dwell + TimeSpan.FromMinutes(5);

        AssertSameConstraint(DepartureSeconds + DwellSeconds + 300 <= ArrivalSeconds, earliestArrival <= Arrival);
        AssertSameConstraint(DepartureSeconds - DwellSeconds - 300 <= ArrivalSeconds, Departure - Dwell - TimeSpan.FromMinutes(5) <= Arrival);
    }

    [Fact]
    public void APlainValueShiftedByAQuantityIsAPointAnchoredAtThatValue() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1));

        AssertSameConstraint(DepartureSeconds >= 600 + DwellSeconds, Departure >= Origin.AddMinutes(10) + Dwell);
        AssertSameConstraint(DepartureSeconds >= 600 + DwellSeconds, Departure >= Dwell + Origin.AddMinutes(10));
        AssertSameConstraint(DepartureSeconds <= 600 - DwellSeconds, Departure <= Origin.AddMinutes(10) - Dwell);
        AssertSameConstraint(DepartureSeconds >= 600 + 60 * Variable.Continuous("minutes") + DwellSeconds, Departure >= Origin.AddMinutes(10) + minutes + Dwell);
        Assert.Equal(new DateTimeProjection(Origin.AddMinutes(10), TimeSpan.FromMinutes(1)), (Origin.AddMinutes(10) + minutes).Projection);
        Assert.Equal(Dwell.Expression, (Origin + Dwell).Expression);
    }

    [Fact]
    public void APlainDeltaMayComeFirst() =>
        AssertSameConstraint(DepartureSeconds + 300 <= ArrivalSeconds, TimeSpan.FromMinutes(5) + Departure <= Arrival);

    [Fact]
    public void BoundsMayBePlainValuesOrExpressions() {
        AssertSameConstraint((DepartureSeconds <= ArrivalSeconds) & (ArrivalSeconds <= DepartureSeconds + 600), Arrival.Between(Departure, Departure + TimeSpan.FromMinutes(10)));
        AssertSameConstraint((0 <= ArrivalSeconds) & (ArrivalSeconds <= DepartureSeconds), Arrival.Between(Origin, Departure));
        AssertSameConstraint((DepartureSeconds <= ArrivalSeconds) & (ArrivalSeconds <= 3600), Arrival.Between(Departure, Origin.AddHours(1)));
        AssertSameConstraint((30 <= DwellSeconds) & (DwellSeconds <= ArrivalSeconds - DepartureSeconds), Dwell.Between(TimeSpan.FromSeconds(30), Arrival - Departure));
        AssertSameConstraint((ArrivalSeconds - DepartureSeconds <= DwellSeconds) & (DwellSeconds <= 300), Dwell.Between(Arrival - Departure, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void AQuantityCanBeCountedInAPlainUnit() =>
        Assert.Equal((DwellSeconds / 60).Normalise(), Dwell.In(TimeSpan.FromMinutes(1)).Normalise());

    [Fact]
    public void PointsCompareWithPlainValues() {
        AssertSameConstraint(DepartureSeconds >= 600, Departure >= Origin.AddMinutes(10));
        AssertSameConstraint(DepartureSeconds.Between(0, 3600), Departure.Between(Origin, Origin.AddHours(1)));
        AssertSameConstraint(DepartureSeconds.EqualTo(60), Departure.EqualTo(Origin.AddMinutes(1)));
        AssertSameConstraint((ArrivalSeconds - 900).EqualTo(0), (Origin.AddMinutes(15) - Arrival).EqualTo(TimeSpan.Zero));
    }

    [Fact]
    public void QuantitiesFormAVectorSpace() {
        AssertSameConstraint(2 * DwellSeconds + 30 <= 120, 2 * Dwell + TimeSpan.FromSeconds(30) <= TimeSpan.FromMinutes(2));
        AssertSameConstraint(-DwellSeconds / 2 >= -60, -Dwell / 2 >= TimeSpan.FromMinutes(-1));
        AssertSameConstraint(60 - DwellSeconds > 0, TimeSpan.FromMinutes(1) - Dwell > TimeSpan.Zero);
    }

    [Fact]
    public void QuantitiesInDifferentUnitsAreReconciled() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1), inWholeUnits: true);

        AssertSameConstraint(DwellSeconds + 60 * Variable.Integer("minutes") <= 600, Dwell + minutes <= TimeSpan.FromMinutes(10));
        AssertSameConstraint(Variable.Integer("minutes") + DwellSeconds / 60 <= 10, minutes + Dwell <= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void PointsWithDifferentOriginsAreReconciled() {
        var later = Variable.DateTime("later", Origin.AddHours(1));

        AssertSameConstraint(DepartureSeconds <= Variable.Continuous("later") + 3600, Departure <= later);
        AssertSameConstraint((Variable.Continuous("later") + 3600 - DepartureSeconds).EqualTo(60), (later - Departure).EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AQuantityCanBeMeasuredInAnotherUnitForUseInAPlainExpression() =>
        Assert.Equal((DwellSeconds / 60).Normalise(), Dwell.In(new TimeSpanProjection(TimeSpan.FromMinutes(1))).Normalise());

    [Fact]
    public void QuantitiesSumUnderTheProjectionOfTheFirst() {
        var minutes = Variable.TimeSpan("minutes", unit: TimeSpan.FromMinutes(1));

        Assert.Equal((DwellSeconds + 60 * Variable.Continuous("minutes")).Normalise(), new[] { Dwell, minutes }.Sum().Expression.Normalise());
        Assert.Throws<InvalidOperationException>(() => Array.Empty<Quantity<TimeSpan>>().Sum());
    }

    [Fact]
    public void SolutionsAreReadBackInTheDomainType() {
        var solution = new Solution(
            ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer).Add(DepartureSeconds, 90).Add(ArrivalSeconds, 600).Add(DwellSeconds, 45),
            0);

        Assert.Equal(Origin.AddSeconds(90), solution.Value(Departure));
        Assert.Equal(TimeSpan.FromSeconds(45), solution.Value(Dwell));
        Assert.Equal(TimeSpan.FromSeconds(510), solution.Value(Arrival - Departure));
        Assert.Equal(Origin.AddSeconds(135), solution.Value(Departure + Dwell));
    }

    [Fact]
    public void TypedObjectivesUnwrapToTheirUnderlyingExpressions() {
        Assert.Equal(new SingleObjectiveProblem(Objective.Minimise(Dwell.Expression), BooleanConstant.True), Problem.Minimise(Dwell).SubjectTo(BooleanConstant.True));
        Assert.Equal(new SingleObjectiveProblem(Objective.Maximise(Departure.Expression), BooleanConstant.True), Problem.Maximise(Departure).SubjectTo(BooleanConstant.True));
    }

    [Fact]
    public void DateTimeOffsetsBehaveLikeDateTimes() {
        var origin = new DateTimeOffset(Origin, TimeSpan.FromHours(10));
        var start = Variable.DateTimeOffset("start", origin, unit: TimeSpan.FromMinutes(1));

        AssertSameConstraint(Variable.Continuous("start") >= 30, start >= origin.AddMinutes(30));
        Assert.Equal(origin.AddMinutes(2), new DateTimeOffsetProjection(origin, TimeSpan.FromMinutes(1)).Decode(2));
    }
}
