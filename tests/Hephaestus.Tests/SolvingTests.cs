using System.Collections.Immutable;

namespace Hephaestus.Tests;

/// <summary>
/// The solving pipeline, driven by a backend that simply tries every whole-number assignment:
/// slow, but obviously correct, and proof that a backend needs to know nothing beyond columns and rows.
/// </summary>
public sealed class SolvingTests {
    private sealed record ExhaustiveBackend : IMilpBackend {
        public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) =>
            Assignments(problem.Columns)
                .Where(assignment => problem.Rows.All(row => Holds(row, assignment)))
                .Select(assignment => new Solution(assignment, problem.Objective.Evaluate(variable => assignment[variable])))
                .OrderBy(solution => problem.Sense == ObjectiveSense.Minimise ? solution.ObjectiveValue : -solution.ObjectiveValue)
                .FirstOrDefault() is { } best
                    ? new Optimal(best)
                    : new Infeasible();

        private static IEnumerable<ImmutableSortedDictionary<IVariable, double>> Assignments(IEnumerable<Column> columns) =>
            columns.Aggregate(
                (IEnumerable<ImmutableSortedDictionary<IVariable, double>>)[ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer)],
                (assignments, column) =>
                    from assignment in assignments
                    from value in Enumerable.Range((int)column.LowerBound, (int)(column.UpperBound - column.LowerBound) + 1)
                    select assignment.Add(column.Variable, value));

        private static bool Holds(LinearRow row, ImmutableSortedDictionary<IVariable, double> assignment) =>
            new AffineForm(row.Coefficients, 0).Evaluate(variable => assignment[variable]) is var value
            && row.LowerBound - 1e-9 <= value && value <= row.UpperBound + 1e-9;
    }

    private static readonly ISolver Solver = new MilpSolver(new ExhaustiveBackend());
    private static readonly IntegerVariable M = Variable.Integer("m");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly BinaryVariable A = Variable.Binary("a");
    private static readonly IBooleanExpression Domain = M.Between(0, 5) & N.Between(0, 5);

    [Fact]
    public void ADisjunctiveProblemIsSolvedThroughTheEncoding() {
        var separated = (M + 2 <= N) | (N + 2 <= M);

        var solution = Assert.IsType<Optimal>(Solver.Solve(Problem.Minimise(M + 2 * N, subjectTo: Domain & separated))).Solution;

        Assert.Equal(2, solution.ObjectiveValue);
        Assert.Equal(2, solution.Value(M));
        Assert.Equal(0, solution.Value(N));
        Assert.True(solution.Value(separated));
    }

    [Fact]
    public void AuxiliaryVariablesNeverLeakIntoTheSolution() {
        var solution = Assert.IsType<Optimal>(Solver.Solve(Problem.Maximise(M, subjectTo: Domain & ((M <= 1) | (N >= 4))))).Solution;

        Assert.Equal([M, N], solution.Values.Keys);
    }

    [Fact]
    public void AnyExpressionCanBeReadOffASolution() {
        var solution = Assert.IsType<Optimal>(Solver.Solve(Problem.Maximise(M - N, subjectTo: Domain & A.Iff(M >= 3)))).Solution;

        Assert.Equal(5, solution.Value(M - N));
        Assert.Equal(11, solution.Value(2 * M + 1));
        Assert.True(solution.Value(A));
        Assert.Equal(1, solution.Value((ILinearExpression)A));
        Assert.True(solution.Value(A & (M > N)));
        Assert.False(solution.Value(A.Implies(N >= 1)));
    }

    [Fact]
    public void ReadingAVariableTheProblemNeverMentionedIsAnError() {
        var solution = Assert.IsType<Optimal>(Solver.Solve(Problem.Satisfy(Domain))).Solution;

        Assert.Throws<KeyNotFoundException>(() => solution.Value(Variable.Integer("stranger")));
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    public void KnownConditionsSwitchConstraintsOnAndOff(bool isFreight, double expected) =>
        Assert.Equal(expected, Assert.IsType<Optimal>(Solver.Solve(Problem.Minimise(M, subjectTo: Domain & isFreight.Implies(M >= 3) & (!isFreight | (N <= 1))))).Solution.Value(M));

    [Fact]
    public void InfeasibilityIsReported() {
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(Domain & (M + N >= 11))));
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Satisfy(Domain & (M >= 6))));
    }

    [Fact]
    public void ResultsAreMatchedExhaustively() {
        static string Describe(ISolveResult result) =>
            result.Match(
                optimal: solution => $"optimal {solution.ObjectiveValue}",
                feasible: solution => $"feasible {solution.ObjectiveValue}",
                infeasible: () => "infeasible",
                unbounded: () => "unbounded",
                unknown: reason => $"unknown: {reason}");

        Assert.Equal("optimal 5", Describe(Solver.Solve(Problem.Maximise(M, subjectTo: Domain))));
        Assert.Equal("infeasible", Describe(new Infeasible()));
        Assert.Equal("unbounded", Describe(new Unbounded()));
        Assert.Equal("unknown: timed out", Describe(new Unknown("timed out")));
        Assert.Null(new Infeasible().SolutionOrNull);
    }

    [Fact]
    public async Task SolvingCanBeAwaited() =>
        Assert.IsType<Optimal>(await Solver.SolveAsync(Problem.Maximise(M, subjectTo: Domain), cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public void ProblemsAreValuesSoExtendingOneLeavesTheOriginalIntact() {
        var original = new Minimisation(M, Domain);
        var tightened = original with { Constraint = original.Constraint & (M >= 3) };

        Assert.Equal(0, Assert.IsType<Optimal>(Solver.Solve(original)).Solution.ObjectiveValue);
        Assert.Equal(3, Assert.IsType<Optimal>(Solver.Solve(tightened)).Solution.ObjectiveValue);
    }

    /// <summary>Solves nothing, but remembers what it was asked to start from.</summary>
    private sealed record RecordingBackend(List<IReadOnlyDictionary<IVariable, double>> Starts) : IMilpBackend {
        public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
            Starts.Add(start);
            return new Unknown("Nothing was solved.");
        }
    }

    [Fact]
    public void AStartingSolutionReachesTheBackendAsValuesForItsOwnColumns() {
        var backend = new RecordingBackend([]);
        var start = Solution.Empty.With(M, 2.2).With(A, true).With(Variable.Integer("stranger"), 9);

        new MilpSolver(backend).Solve(Problem.Minimise(M, subjectTo: Domain & ((M >= 2) | A)), startingFrom: start);

        // The stranger is dropped, the whole-number variable is rounded, and n and the auxiliary binary are left to the solver.
        Assert.Equal([KeyValuePair.Create<IVariable, double>(A, 1), KeyValuePair.Create<IVariable, double>(M, 2)], backend.Starts.Single().OrderBy(entry => entry.Key.Name));
    }

    [Fact]
    public void WithoutAStartingSolutionTheBackendStartsFromNothing() {
        var backend = new RecordingBackend([]);

        new MilpSolver(backend).Solve(Problem.Minimise(M, subjectTo: Domain));

        Assert.Empty(backend.Starts.Single());
    }

    [Fact]
    public void AStartingSolutionCanBeBuiltByHandInTheTypesOfTheModel() {
        var origin = new DateTime(2026, 9, 19, 8, 0, 0);
        var departure = Variable.DateTime("departure", origin);
        var dwell = Variable.TimeSpan("dwell", unit: TimeSpan.FromMinutes(1));

        var start = Solution.Empty.With(departure, origin.AddMinutes(5)).With(dwell, TimeSpan.FromSeconds(90)).With(A, false);

        Assert.Equal(300, start.Values[Variable.Continuous("departure")]);
        Assert.Equal(1.5, start.Values[Variable.Continuous("dwell")]);
        Assert.Equal(0, start.Values[A]);
        Assert.Throws<ArgumentException>(() => Solution.Empty.With(departure + dwell, origin));
    }

    /// <summary>Solves with the exhaustive backend, and remembers what each solve was asked and where it started from.</summary>
    private sealed record WatchedBackend(List<(MilpProblem Problem, IReadOnlyDictionary<IVariable, double> Start)> Solves) : IMilpBackend {
        public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
            Solves.Add((problem, start));
            return new ExhaustiveBackend().Solve(problem, start, options, cancellationToken);
        }
    }

    private static readonly IBooleanExpression Linked = Domain & (N <= M + 1);

    [Fact]
    public void ALaterObjectiveOnlyChoosesAmongTheBestForTheEarlierOnes() {
        var solution = Assert.IsType<Optimal>(Solver.Solve(Problem.Minimise(M, subjectTo: Linked).Then(Objective.Maximise(N)))).Solution;

        Assert.Equal((0, 1), (solution.Value(M), solution.Value(N)));
        Assert.Equal(0, solution.ObjectiveValue);
    }

    [Fact]
    public void ATolerancedObjectiveGivesGroundToThoseAfterIt() {
        var byAmount = Problem.Lexicographic([Objective.Minimise(M, absoluteTolerance: 2), Objective.Maximise(N)], subjectTo: Linked);
        var byFraction = Problem.Lexicographic([Objective.Maximise(M + 5, relativeTolerance: 0.2), Objective.Minimise(N)], subjectTo: Linked & (N >= M - 1));

        Assert.Equal((2, 3), Read(Assert.IsType<Optimal>(Solver.Solve(byAmount)).Solution));
        Assert.Equal((3, 2), Read(Assert.IsType<Optimal>(Solver.Solve(byFraction)).Solution));
    }

    private static (double, double) Read(Solution solution) => (solution.Value(M), solution.Value(N));

    [Fact]
    public void ObjectivesCanBeChainedAtLength() {
        var problem = Problem.Satisfy(Linked).Then(Objective.Maximise(M + N)).Then(Objective.Minimise(M)).Then(Objective.Maximise(A)).Then(Objective.Minimise(N));

        var solution = Assert.IsType<Optimal>(Solver.Solve(problem)).Solution;

        Assert.Equal(4, problem.Objectives.Length);
        Assert.Equal((5, 5), Read(solution));
        Assert.Equal(10, solution.ObjectiveValue);
    }

    [Fact]
    public void EachStageStartsFromTheSolutionBeforeAndIsHeldToItsValue() {
        var backend = new WatchedBackend([]);

        new MilpSolver(backend).Solve(Problem.Minimise(M, subjectTo: Linked).Then(Objective.Maximise(N)), startingFrom: Solution.Empty.With(M, 4));

        Assert.Equal([4.0], backend.Solves[0].Start.Values);
        Assert.Equal([0.0, 0.0], backend.Solves[1].Start.OrderBy(entry => entry.Key.Name).Select(entry => entry.Value));
        Assert.Equal(0, backend.Solves[1].Problem.Columns.Single(column => column.Variable.Equals(M)).UpperBound, precision: 6);
    }

    [Fact]
    public void WithoutASolutionTheFirstStagesOutcomeStands() {
        Assert.IsType<Infeasible>(Solver.Solve(Problem.Minimise(M, subjectTo: Linked & (M >= 9)).Then(Objective.Maximise(N))));
        Assert.IsType<Optimal>(Solver.Solve(Problem.Lexicographic([], subjectTo: Linked)));
    }

    /// <summary>Gives up on every solve after its first.</summary>
    private sealed record FlaggingBackend(List<int> Count) : IMilpBackend {
        public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
            Count.Add(0);
            return Count.Count == 1 ? new ExhaustiveBackend().Solve(problem, start, options, cancellationToken) : new Unknown("Out of time.");
        }
    }

    [Fact]
    public void AStageThatGivesUpLeavesTheSolutionBeforeItUnproven() {
        var result = new MilpSolver(new FlaggingBackend([])).Solve(Problem.Minimise(M, subjectTo: Linked).Then(Objective.Maximise(N)).Then(Objective.Maximise(A)));

        Assert.Equal(0, Assert.IsType<Feasible>(result).Solution.Value(M));
    }

    [Fact]
    public void TypedObjectivesTakeTypedTolerances() {
        var origin = new DateTime(2026, 9, 19, 8, 0, 0);
        var delay = Variable.TimeSpan("delay", unit: TimeSpan.FromMinutes(1));
        var departure = Variable.DateTime("departure", origin);

        Assert.Equal(new Objective(ObjectiveSense.Minimise, delay.Expression, 1.5), Objective.Minimise(delay, tolerance: TimeSpan.FromSeconds(90)));
        Assert.Equal(new Objective(ObjectiveSense.Maximise, delay.Expression, 0, 0.1), Objective.Maximise(delay, relativeTolerance: 0.1));
        Assert.Equal(new Objective(ObjectiveSense.Minimise, departure.Expression, 30), Objective.Minimise(departure, tolerance: TimeSpan.FromSeconds(30)));
        Assert.Equal(new Objective(ObjectiveSense.Maximise, departure.Expression), Objective.Maximise(departure));
    }
}
