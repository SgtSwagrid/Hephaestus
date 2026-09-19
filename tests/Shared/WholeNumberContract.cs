
namespace Hephaestus.Contracts;

/// <summary>
/// The contract for problems posed entirely in whole numbers, such as a timetable in whole seconds.
/// CP-SAT can only take part here; every other solver runs it too, so that they are seen to agree.
/// </summary>
public abstract class WholeNumberContract {
    protected abstract ISolver Solver { get; }

    private static readonly IntegerVariable DepartureA = Variable.Integer("departureA");
    private static readonly IntegerVariable DepartureB = Variable.Integer("departureB");
    private static readonly IntegerVariable DepartureC = Variable.Integer("departureC");
    private static readonly BinaryVariable OccupiesA = Variable.Binary("occupiesA");
    private static readonly BinaryVariable OccupiesB = Variable.Binary("occupiesB");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly BinaryVariable Flag = Variable.Binary("flag");

    private const double Headway = 120;

    private static IBooleanExpression Separated(ILinearExpression first, ILinearExpression second) =>
        (first + Headway <= second) | (second + Headway <= first);

    private Solution Optimum(IProblem problem) => Assert.IsType<Optimal>(Solver.Solve(problem)).Solution;

    [Fact]
    public void ThreeTrainsQueueForOneTrack() {
        var departures = new[] { DepartureA, DepartureB, DepartureC };
        var constraint =
            departures.AllOf(departure => departure.Between(0, 3600))
            & (DepartureA >= 60) & (DepartureC >= 30)
            & Separated(DepartureA, DepartureB) & Separated(DepartureA, DepartureC) & Separated(DepartureB, DepartureC);

        var solution = Optimum(Problem.Minimise(departures.Sum(), subjectTo: constraint));

        Assert.Equal(0 + 120 + 240, solution.ObjectiveValue);
        Assert.True(solution.Value(constraint));
    }

    [Fact]
    public void SeparationAppliesOnlyToTrainsThatShareTheTrack() {
        var constraint = DepartureA.Between(0, 3600) & DepartureB.Between(0, 3600) & (!(OccupiesA & OccupiesB) | Separated(DepartureA, DepartureB));

        Assert.Equal(120, Optimum(Problem.Minimise(DepartureA + DepartureB, subjectTo: constraint & OccupiesA & OccupiesB)).ObjectiveValue);
        Assert.Equal(0, Optimum(Problem.Minimise(DepartureA + DepartureB, subjectTo: constraint & OccupiesA & !OccupiesB)).ObjectiveValue);
        Assert.Equal(120, Optimum(Problem.Minimise(DepartureA + DepartureB + 1000 * (2 - OccupiesA - OccupiesB), subjectTo: constraint)).ObjectiveValue);
    }

    [Fact]
    public void AWholeNumberProblemIsSolvedToItsWholeNumberOptimum() {
        var (a, b, c) = (Variable.Integer("a"), Variable.Integer("b"), Variable.Integer("c"));
        var constraint =
            (a >= 0) & (b >= 0) & (c >= 0)
            & (2 * a + 3 * b + c <= 5)
            & (4 * a + b + 2 * c <= 11)
            & (3 * a + 4 * b + 2 * c <= 8);

        var solution = Optimum(Problem.Maximise(5 * a + 4 * b + 3 * c, subjectTo: constraint));

        Assert.Equal(13, solution.ObjectiveValue);
        Assert.Equal((2, 0, 1), (solution.Value(a), solution.Value(b), solution.Value(c)));
    }

    [Fact]
    public void LogicBindsAsItShould() {
        var domain = N.Between(0, 10);

        Assert.True(Optimum(Problem.Minimise(Flag, subjectTo: domain & Flag.Iff(N >= 5) & N.EqualTo(7))).Value(Flag));
        Assert.False(Optimum(Problem.Maximise(Flag, subjectTo: domain & Flag.Iff(N >= 5) & N.EqualTo(3))).Value(Flag));
        Assert.Equal(6, Optimum(Problem.Minimise(N, subjectTo: domain & Flag & Flag.Implies(N >= 6))).Value(N));
        Assert.Equal(4, Optimum(Problem.Minimise(N, subjectTo: domain & (N >= 3) & N.NotEqualTo(3))).Value(N));
        Assert.Equal(9, Optimum(Problem.Maximise(N, subjectTo: domain & (N < 10) & ((N <= 2) ^ (N >= 8)))).Value(N));
    }

    [Fact]
    public void FractionalCoefficientsAndLimitsAreHandled() {
        var domain = N.Between(0, 100);

        Assert.Equal(5, Optimum(Problem.Maximise(N, subjectTo: domain & (0.5 * N < 3))).Value(N));
        Assert.Equal(7, Optimum(Problem.Maximise(N, subjectTo: domain & (0.25 * N + 0.125 * Flag <= 1.9))).Value(N));
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(domain & (2 * N).EqualTo(5))));
        Assert.Equal(1, Optimum(Problem.Minimise(Flag, subjectTo: domain & (Flag | (2 * N).EqualTo(5)))).ObjectiveValue);
    }

    [Fact]
    public void BoundsThatAreOnlyImpliedAreEnough() {
        var arrival = Variable.Integer("arrival");
        var constraint = DepartureA.Between(0, 100) & N.Between(10, 20) & arrival.EqualTo(DepartureA + N) & Flag.Iff(arrival >= 90);

        var solution = Optimum(Problem.Maximise(arrival - 50 * Flag, subjectTo: constraint));

        Assert.Equal(89, solution.Value(arrival));
        Assert.False(solution.Value(Flag));
    }

    [Fact]
    public void ExactlyOneOfSeveralOptionsCanBeChosen() {
        var options = Enumerable.Range(0, 5).Select(index => Variable.Binary($"option{index}")).ToList();
        var weights = new double[] { 3, 9, 4, 7, 1 };

        var solution = Optimum(Problem.Maximise(options.Zip(weights, (option, weight) => weight * option).Sum(), subjectTo: options.Sum().EqualTo(1)));

        Assert.Equal([false, true, false, false, false], options.Select(option => solution.Value(option)));
    }

    [Fact]
    public void InfeasibilityIsReported() {
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(N.Between(0, 10) & ((N <= 2) | (N >= 8)) & N.Between(3, 7))));
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(Flag & !Flag & N.Between(0, 1))));
    }

    [Fact]
    public void QuantisedTypedVariablesAreWholeNumbersUnderneath() {
        var start = new DateTime(2026, 9, 19, 8, 0, 0);
        var departure = Variable.DateTime("departure", origin: start, inWholeUnits: true);
        var dwell = Variable.TimeSpan("dwell", unit: TimeSpan.FromSeconds(30), inWholeUnits: true);
        var constraint = departure.Between(start, start.AddHours(1)) & dwell.Between(TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(5)) & (departure >= start.AddMinutes(10) + dwell);

        var solution = Optimum(Problem.Minimise(departure, subjectTo: constraint));

        Assert.Equal(TimeSpan.FromSeconds(60), solution.Value(dwell));
        Assert.Equal(start.AddMinutes(11), solution.Value(departure));
    }
}

