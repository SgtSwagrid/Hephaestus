using Hephaestus.OrTools;
using Hephaestus.Z3;
using NodaTime;

namespace Hephaestus.NodaTime.Tests;

/// <summary>The typed layer end to end: constraints written in NodaTime types, solved, and read back in NodaTime types.</summary>
public sealed class TimetablingTests {
    private static readonly LocalDateTime Start = new(2026, 9, 19, 8, 0);
    private static readonly Duration Headway = Duration.FromMinutes(2);
    private static readonly Duration Moment = Duration.FromMilliseconds(1);

    public static TheoryData<string> Solvers => [.. SolversByName.Keys];

    private static readonly Dictionary<string, ISolver> SolversByName = new() {
        ["SCIP"] = OrToolsSolver.Create(OrToolsSolverId.Scip),
        ["HiGHS"] = OrToolsSolver.Create(OrToolsSolverId.Highs),
        ["Z3"] = new Z3Solver(),
    };

    private static void AssertClose(LocalDateTime expected, LocalDateTime actual) =>
        Assert.True(Period.Between(expected, actual, PeriodUnits.Nanoseconds).ToDuration() is var gap && gap < Moment && gap > -Moment, $"Expected {expected}, got {actual}.");

    [Theory]
    [MemberData(nameof(Solvers))]
    public void TwoTrainsOnOneTrackKeepTheirHeadway(string solver) {
        var departureA = Variable.LocalDateTime("departureA", origin: Start);
        var departureB = Variable.LocalDateTime("departureB", origin: Start);
        var separated = (departureA + Headway <= departureB) | (departureB + Headway <= departureA);
        var constraint =
            departureA.Between(Start.PlusMinutes(5), Start.PlusHours(1))
            & departureB.Between(Start.PlusMinutes(4), Start.PlusHours(1))
            & separated;

        var solution = Assert.IsType<Optimal>(SolversByName[solver].Solve(Problem.Minimise((departureA - Start) + (departureB - Start)).SubjectTo(constraint))).Solution;

        AssertClose(Start.PlusMinutes(4), solution.Value(departureB));
        AssertClose(Start.PlusMinutes(6), solution.Value(departureA));
        Assert.True(solution.Value(separated));
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public void QuantisedDurationsComeBackAsWholeUnits(string solver) {
        var dwell = Variable.Duration("dwell", unit: Duration.FromSeconds(30), inWholeUnits: true);
        var arrival = Variable.LocalDateTime("arrival", origin: Start);
        var departure = Variable.LocalDateTime("departure", origin: Start);
        var constraint =
            arrival.EqualTo(Start.PlusMinutes(10))
            & departure.Between(Start, Start.PlusHours(1))
            & (departure - arrival).EqualTo(dwell)
            & (dwell >= Duration.FromSeconds(50));

        var solution = Assert.IsType<Optimal>(SolversByName[solver].Solve(Problem.Minimise(departure).SubjectTo(constraint))).Solution;

        Assert.Equal(Duration.FromSeconds(60), solution.Value(dwell));
        AssertClose(Start.PlusMinutes(11), solution.Value(departure));
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public void DatesAndPeriodsWorkInWholeDays(string solver) {
        var today = new LocalDate(2026, 9, 19);
        var possession = Variable.LocalDate("possession", origin: today);
        var reopening = Variable.LocalDate("reopening", origin: today);
        var constraint =
            possession.Between(today.PlusDays(3), today.PlusDays(30))
            & (reopening - possession >= Period.FromWeeks(1))
            & (possession.NotEqualTo(today.PlusDays(3)));

        var solution = Assert.IsType<Optimal>(SolversByName[solver].Solve(Problem.Minimise(reopening).SubjectTo(constraint))).Solution;

        Assert.Equal(today.PlusDays(4), solution.Value(possession));
        Assert.Equal(today.PlusDays(11), solution.Value(reopening));
        Assert.Equal(Period.FromDays(7), solution.Value(reopening - possession));
    }

    [Fact]
    public void InstantsAndZonedDateTimesShareTheGlobalTimeLine() {
        var origin = Instant.FromUtc(2026, 9, 19, 0, 0);
        var sydney = DateTimeZoneProviders.Tzdb["Australia/Sydney"];
        var handover = Variable.Instant("handover", origin, unit: Duration.FromMinutes(1));
        var briefing = Variable.ZonedDateTime("briefing", origin.InZone(sydney), unit: Duration.FromMinutes(1));
        var constraint =
            handover.Between(origin, origin + Duration.FromHours(12))
            & briefing.Between(origin.InZone(sydney), (origin + Duration.FromHours(12)).InZone(sydney))
            & (handover >= origin + Duration.FromHours(3))
            & ((briefing - origin.InZone(sydney)) >= (handover - origin) + Duration.FromMinutes(30));

        var solution = Assert.IsType<Optimal>(new Z3Solver().Solve(Problem.Minimise(briefing).SubjectTo(constraint))).Solution;

        Assert.Equal(origin + Duration.FromHours(3), solution.Value(handover));
        Assert.Equal((origin + Duration.FromMinutes(210)).InZone(sydney), solution.Value(briefing));
    }

    [Fact]
    public void TimesOfDayStayWithinTheirDayWhenConstrainedTo() {
        var curfew = Variable.LocalTime("curfew", unit: Duration.FromMinutes(1));
        var constraint = curfew.Between(LocalTime.Midnight, LocalTime.MaxValue) & (curfew >= new LocalTime(22, 15)) & (curfew - new LocalTime(22, 0) <= Duration.FromMinutes(45));

        var solution = Assert.IsType<Optimal>(OrToolsSolver.Create().Solve(Problem.Maximise(curfew).SubjectTo(constraint))).Solution;

        Assert.Equal(new LocalTime(22, 45), solution.Value(curfew));
    }

    [Fact]
    public void PlainValuesShiftedByQuantitiesArePoints() {
        var dwell = Variable.Duration("dwell", unit: Duration.FromSeconds(30), inWholeUnits: true);
        var days = Variable.LocalDate("reopening", origin: new LocalDate(2026, 9, 19)) - new LocalDate(2026, 9, 19);
        var sydney = DateTimeZoneProviders.Tzdb["Australia/Sydney"];
        var instant = Instant.FromUtc(2026, 9, 19, 0, 0);
        var solution = new Solution(
            System.Collections.Immutable.ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer).Add(Variable.Integer("dwell"), 3).Add(Variable.Integer("reopening"), 10),
            0);

        Assert.Equal(Start.PlusSeconds(90), solution.Value(Start + dwell));
        Assert.Equal(Start.PlusSeconds(90), solution.Value(dwell + Start));
        Assert.Equal(Start.PlusSeconds(-90), solution.Value(Start - dwell));
        Assert.Equal(instant + Duration.FromSeconds(90), solution.Value(instant + dwell));
        Assert.Equal((instant + Duration.FromSeconds(90)).InZone(sydney), solution.Value(instant.InZone(sydney) + dwell));
        Assert.Equal((instant + Duration.FromSeconds(90)).WithOffset(Offset.FromHours(10)), solution.Value(instant.WithOffset(Offset.FromHours(10)) + dwell));
        Assert.Equal(new LocalTime(8, 1, 30), solution.Value(new LocalTime(8, 0) + dwell));
        Assert.Equal(new LocalDate(2026, 10, 11), solution.Value(new LocalDate(2026, 10, 1) + days));
        Assert.Equal(new LocalDate(2026, 9, 21), solution.Value(new LocalDate(2026, 10, 1) - days));
    }

    [Theory]
    [MemberData(nameof(Solvers))]
    public void ADepartureWaitsForTheDwellAfterAFixedArrival(string solver) {
        var dwell = Variable.Duration("dwell");
        var departure = Variable.LocalDateTime("departure", origin: Start);
        var constraint = dwell.Between(Duration.FromSeconds(45), Duration.FromMinutes(5)) & departure.Between(Start, Start.PlusHours(1)) & (departure >= Start.PlusMinutes(10) + dwell);

        var solution = Assert.IsType<Optimal>(SolversByName[solver].Solve(Problem.Minimise(departure).SubjectTo(constraint))).Solution;

        AssertClose(Start.PlusMinutes(10).PlusSeconds(45), solution.Value(departure));
    }

    [Fact]
    public void WhatDoesNotMakeSenseDoesNotCompile() {
        // departureA + departureB          -- no operator + for two points
        // 2 * departureA                   -- points cannot be scaled
        // departureA <= Headway            -- a point is not comparable with a quantity
        // (departureA - departureB) <= Start -- nor a quantity with a point
        var departure = Variable.LocalDateTime("departure", origin: Start);

        Quantity<Duration> sinceStart = departure - Start;
        Point<LocalDateTime, Duration> shifted = departure + 2 * sinceStart - Headway;

        Assert.Equal((120 - 2 * Variable.Continuous("departure")).Normalise(), (departure - shifted).Expression.Normalise());
    }
}
