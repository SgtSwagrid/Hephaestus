
using static Hephaestus.Piecewise;

namespace Hephaestus.Contracts;

/// <summary>
/// What every solver must do, whatever is underneath. The same problems go to MILP backends (by way
/// of the big-M encoding) and to Z3 (which takes the logic as it stands); agreeing answers are the
/// evidence that the solver really is swappable.
/// </summary>
public abstract class SolverContract {
    protected abstract ISolver Solver { get; }

    private static readonly ContinuousVariable DepartureA = Variable.Continuous("departureA");
    private static readonly ContinuousVariable DepartureB = Variable.Continuous("departureB");
    private static readonly BinaryVariable OccupiesA = Variable.Binary("occupiesA");
    private static readonly BinaryVariable OccupiesB = Variable.Binary("occupiesB");
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly BinaryVariable Flag = Variable.Binary("flag");

    private const double Headway = 120;

    /// <summary>Decimal places to which continuous values are compared: solvers work to tolerances of about a millionth.</summary>
    private const int Precision = 4;

    private static readonly IBooleanExpression Separated = (DepartureA + Headway <= DepartureB) | (DepartureB + Headway <= DepartureA);
    private static readonly IBooleanExpression ConflictFree = !(OccupiesA & OccupiesB) | Separated;
    private static readonly IBooleanExpression Horizon = DepartureA.Between(0, 3600) & DepartureB.Between(0, 3600);

    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(1);

    private Solution Optimum(IProblem problem) => Assert.IsType<Optimal>(Solver.Solve(problem)).Solution;

    [Fact]
    public void TrainsSharingATrackAreSeparatedByTheHeadway() {
        var solution = Optimum(Problem.Minimise(DepartureA + DepartureB, subjectTo: Horizon & ConflictFree & OccupiesA & OccupiesB));

        Assert.Equal(Headway, solution.ObjectiveValue, precision: Precision);
        Assert.Equal(Headway, Math.Abs(solution.Value(DepartureA - DepartureB)), precision: Precision);
        Assert.True(solution.Value(Separated));
    }

    [Fact]
    public void TrainsOnDifferentTracksNeedNoSeparation() {
        var solution = Optimum(Problem.Minimise(DepartureA + DepartureB, subjectTo: Horizon & ConflictFree & OccupiesA & !OccupiesB));

        Assert.Equal(0, solution.ObjectiveValue, precision: Precision);
        Assert.False(solution.Value(Separated));
        Assert.True(solution.Value(ConflictFree));
    }

    [Fact]
    public void TheSolverMayChooseToGiveUpTheTrackRatherThanWait() {
        // Departing late costs a little; not running on the shared track at all costs more than waiting.
        var both = Problem.Minimise(
            DepartureA + DepartureB + 1000 * (2 - OccupiesA - OccupiesB),
            subjectTo: Horizon & ConflictFree);
        var solution = Optimum(both);

        Assert.Equal(Headway, solution.ObjectiveValue, precision: Precision);
        Assert.True(solution.Value(OccupiesA & OccupiesB & Separated));
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

        Assert.Equal(13, solution.ObjectiveValue, precision: Precision);
        Assert.Equal((2, 0, 1), (solution.Value(a), solution.Value(b), solution.Value(c)));
    }

    [Fact]
    public void AnEquivalenceBindsInBothDirections() {
        var domain = X.Between(0, 10);

        Assert.True(Optimum(Problem.Minimise(Flag, subjectTo: domain & Flag.Iff(X >= 5) & X.EqualTo(7))).Value(Flag));
        Assert.False(Optimum(Problem.Maximise(Flag, subjectTo: domain & Flag.Iff(X >= 5) & X.EqualTo(3))).Value(Flag));
    }

    [Fact]
    public void AnImplicationBindsOnlyWhenItsAntecedentHolds() {
        var domain = X.Between(0, 100);

        Assert.Equal(10, Optimum(Problem.Minimise(X, subjectTo: domain & Flag & Flag.Implies(X >= 10))).Value(X), precision: Precision);
        Assert.Equal(0, Optimum(Problem.Minimise(X, subjectTo: domain & Flag.Implies(X >= 10))).Value(X), precision: Precision);
    }

    [Fact]
    public void DisequalityAndStrictnessOverWholeNumbersAreExact() {
        var domain = N.Between(3, 6);

        Assert.Equal(4, Optimum(Problem.Minimise(N, subjectTo: domain & N.NotEqualTo(3))).Value(N));
        Assert.Equal(5, Optimum(Problem.Maximise(N, subjectTo: domain & (N < 6))).Value(N));
        Assert.Equal(5, Optimum(Problem.Minimise(N, subjectTo: domain & !(N <= 4))).Value(N));
    }

    [Fact]
    public void StrictnessOverRealsIsRespected() {
        var solution = Assert.IsAssignableFrom<ISolveResult>(Solver.Solve(Problem.Minimise(X, subjectTo: X.Between(0, 10) & (X > 5)))).SolutionOrNull;

        Assert.NotNull(solution);
        Assert.True(solution.Value(X) > 5);
    }

    [Fact]
    public void ExactlyOneOfSeveralOptionsCanBeChosen() {
        var options = Enumerable.Range(0, 5).Select(index => Variable.Binary($"option{index}")).ToList();
        var weights = new double[] { 3, 9, 4, 7, 1 };

        var solution = Optimum(Problem.Maximise(options.Zip(weights, (option, weight) => weight * option).Sum(), subjectTo: options.Sum().EqualTo(1)));

        Assert.Equal(9, solution.ObjectiveValue, precision: Precision);
        Assert.Equal([false, true, false, false, false], options.Select(option => solution.Value(option)));
    }

    [Fact]
    public void ASatisfactionProblemYieldsSomeSolutionThatSatisfiesIt() {
        var constraint = X.Between(0, 10) & Y.Between(0, 10) & ((X + Y).EqualTo(12) | (X - Y >= 8)) & (X <= 7) & (Y >= 3);

        var solution = Solver.Solve(Problem.Satisfy(constraint)).SolutionOrNull;

        Assert.NotNull(solution);
        Assert.True(solution.Value(constraint));
    }

    [Fact]
    public void InfeasibilityIsReported() {
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(X.Between(0, 1) & Y.Between(0, 1) & (X + Y >= 3))));
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(X.Between(0, 10) & ((X <= 2) | (X >= 8)) & X.Between(3, 7))));
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy((X <= 1) & (X >= 2))));
    }

    [Fact]
    public void UnboundednessIsReported() =>
        Assert.IsType<Unbounded>(Solver.Solve(Problem.Maximise(X + Y, subjectTo: (X >= 0) & (Y >= 0) & (X - Y <= 1))));

    [Fact]
    public void TypedExpressionsAreSolvedAndReadBackInTheirOwnTypes() {
        var start = new DateTime(2026, 9, 19, 8, 0, 0);
        var departure = Variable.DateTime("departure", origin: start);
        var arrival = Variable.DateTime("arrival", origin: start);
        var dwell = Variable.TimeSpan("dwell", unit: TimeSpan.FromSeconds(30), inWholeUnits: true);
        var constraint =
            departure.Between(start, start.AddHours(1))
            & arrival.Between(start, start.AddHours(2))
            & dwell.Between(TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(5))
            & (departure >= start.AddMinutes(10) + dwell)
            & (arrival - departure >= TimeSpan.FromMinutes(25));

        var solution = Optimum(Problem.Minimise(arrival, subjectTo: constraint));

        Assert.Equal(TimeSpan.FromSeconds(60), solution.Value(dwell));
        Assert.Equal(start.AddMinutes(11), solution.Value(departure), Moment);
        Assert.Equal(start.AddMinutes(36), solution.Value(arrival), Moment);
        Assert.Equal(start + TimeSpan.FromMinutes(25), start + solution.Value(arrival - departure), Moment);
    }

    [Fact]
    public void TheLargestOfSeveralIsMadeAsSmallAsPossible() {
        var constraint = X.Between(0, 10) & Y.Between(0, 10) & (X + Y >= 7) & (X - Y <= 1);

        var solution = Optimum(Problem.Minimise(Max(X, Y), subjectTo: constraint));

        Assert.Equal(3.5, solution.ObjectiveValue, precision: Precision);
        Assert.Equal(3.5, solution.Value(Max(X, Y)), precision: Precision);
    }

    [Fact]
    public void TheLargestOfSeveralIsMadeAsLargeAsPossible() {
        var constraint = X.Between(0, 4) & Y.Between(0, 7) & (X + Y <= 8);

        Assert.Equal(7, Optimum(Problem.Maximise(Max(X, Y), subjectTo: constraint)).ObjectiveValue, precision: Precision);
        Assert.Equal(4, Optimum(Problem.Maximise(Min(X, Y), subjectTo: constraint)).ObjectiveValue, precision: Precision);
        Assert.Equal(0, Optimum(Problem.Minimise(Min(X, Y), subjectTo: constraint)).ObjectiveValue, precision: Precision);
    }

    [Fact]
    public void AnAbsoluteValueBindsFromBothSides() {
        var constraint = X.Between(0, 10) & (Abs(X - 5) >= 2);

        Assert.Equal(2, Optimum(Problem.Minimise(Abs(X - 5), subjectTo: constraint)).ObjectiveValue, precision: Precision);
        Assert.Equal(5, Optimum(Problem.Maximise(Abs(X - 5), subjectTo: constraint)).ObjectiveValue, precision: Precision);
        Assert.Equal(7, Optimum(Problem.Minimise(X, subjectTo: constraint & (X >= 4))).ObjectiveValue, precision: Precision);
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(constraint & (Abs(X - 5) <= 1))));
    }

    [Fact]
    public void DeviationsFromSeveralTargetsAreSummed() {
        var targets = new double[] { 2, 4, 9 };

        var solution = Optimum(Problem.Minimise(targets.Sum(target => Abs(X - target)), subjectTo: X.Between(0, 10)));

        Assert.Equal(7, solution.ObjectiveValue, precision: Precision);
        Assert.Equal(4, solution.Value(X), precision: Precision);
    }

    [Fact]
    public void PiecewiseFunctionsNestAndMixWithLogic() {
        var inner = Max(X, Min(Y, 6));
        var constraint = X.Between(0, 3) & Y.Between(0, 10) & Flag.Iff(inner >= 5);

        Assert.Equal(6, Optimum(Problem.Maximise(inner, subjectTo: constraint)).ObjectiveValue, precision: Precision);
        Assert.True(Optimum(Problem.Maximise(Y, subjectTo: constraint)).Value(Flag));
        Assert.Equal(10 - 20, Optimum(Problem.Maximise(Y - 20 * Flag, subjectTo: constraint & (Y >= 5))).ObjectiveValue, precision: Precision);
    }

    [Fact]
    public void AStartingSolutionChangesNothingButTheRoute() {
        var problem = Problem.Minimise(DepartureA + DepartureB, subjectTo: Horizon & ConflictFree & OccupiesA & OccupiesB);
        var optimum = Optimum(problem);
        var starts = new[] {
            optimum,
            Solution.Empty.With(DepartureA, 500).With(DepartureB, 1000).With(OccupiesA, true).With(OccupiesB, true),
            Solution.Empty.With(DepartureB, 1000),
            Solution.Empty.With(DepartureA, 10).With(DepartureB, 20).With(X, 3),
        };

        Assert.All(starts, start => Assert.Equal(Headway, Assert.IsType<Optimal>(Solver.Solve(problem, startingFrom: start)).Solution.ObjectiveValue, precision: Precision));
    }
}
