using System.Collections.Immutable;
using System.Globalization;
using Google.OrTools.LinearSolver;

namespace Hephaestus.OrTools;

/// <summary>The identifiers of solvers that OR-Tools can drive. Which are available depends on the OR-Tools build and on installed licences.</summary>
public static class OrToolsSolverId {
    /// <summary>SCIP, a general-purpose MILP solver bundled with OR-Tools.</summary>
    public const string Scip = "SCIP";

    /// <summary>COIN-OR CBC, a MILP solver bundled with OR-Tools.</summary>
    public const string Cbc = "CBC";

    /// <summary>HiGHS, a MILP solver bundled with OR-Tools.</summary>
    public const string Highs = "HIGHS";

    /// <summary>CP-SAT driven as a MILP solver through a big-M encoding. Prefer <see cref="CpSatSolver"/>, which needs no big-M.</summary>
    public const string CpSat = "CP_SAT";

    /// <summary>GLOP, a simplex solver for purely continuous, purely conjunctive problems. Anything needing a binary variable is refused.</summary>
    public const string Glop = "GLOP";

    /// <summary>CPLEX; needs a licence and a build of OR-Tools from source with CPLEX enabled, which the NuGet package is not.</summary>
    public const string Cplex = "CPLEX";

    /// <summary>FICO Xpress; needs a separate installation and licence, which OR-Tools finds at run time.</summary>
    public const string Xpress = "XPRESS";
}

/// <summary>Solves mixed-integer linear programmes with a solver driven through Google OR-Tools.</summary>
public sealed record OrToolsBackend(string SolverId = OrToolsSolverId.Scip) : IMilpBackend {
    /// <inheritdoc/>
    public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
        using var solver = SupportingWholeNumbers(
            Solver.CreateSolver(SolverId) ?? throw new NotSupportedException($"OR-Tools cannot provide the solver '{SolverId}'. It may need a separate installation or licence."),
            problem);
        using var parameters = new MPSolverParameters();
        using var interruption = cancellationToken.Register(() => solver.InterruptSolve());

        var variables = problem.Columns.ToImmutableDictionary(column => column.Variable, column => Declare(solver, column));
        problem.Rows.ToList().ForEach(row => Declare(solver, row, variables));
        Declare(solver.Objective(), problem, variables);
        Hint(solver, [.. start.Where(entry => variables.ContainsKey(entry.Key))], variables);
        Configure(solver, parameters, options);

        return AsResult(solver.Solve(parameters), solver, variables);
    }

    /// <summary>
    /// OR-Tools lets a pure LP solver accept whole-number variables and then quietly treats them as
    /// continuous, which would dissolve every disjunction in the problem and still report "optimal".
    /// </summary>
    private Solver SupportingWholeNumbers(Solver solver, MilpProblem problem) =>
        solver.IsMip() || !problem.Columns.Any(column => column.Variable.IsIntegral)
            ? solver
            : throw new NotSupportedException(
                $"'{SolverId}' solves linear programmes only, but this problem needs whole-number variables "
                + $"({string.Join(", ", problem.Columns.Where(column => column.Variable.IsIntegral).Take(5).Select(column => column.Variable.Name))}"
                + $"{(problem.Columns.Count(column => column.Variable.IsIntegral) > 5 ? ", ..." : "")}), "
                + "whether its own or the auxiliaries that encode its logic. OR-Tools would relax them without warning; use a MILP solver such as SCIP or HiGHS.");

    /// <summary>
    /// Only SCIP and CBC are given the hint. OR-Tools offers it to any solver, but its HiGHS interface
    /// crashes the process on receiving one, so the list is of those known to be safe.
    /// </summary>
    private void Hint(Solver solver, ImmutableArray<KeyValuePair<IVariable, double>> start, ImmutableDictionary<IVariable, Google.OrTools.LinearSolver.Variable> variables) {
        if (!start.IsEmpty && SolverId is OrToolsSolverId.Scip or OrToolsSolverId.Cbc) {
            solver.SetHint([.. start.Select(entry => variables[entry.Key])], [.. start.Select(entry => entry.Value)]);
        }
    }

    private static Google.OrTools.LinearSolver.Variable Declare(Solver solver, Column column) =>
        solver.MakeVar(column.LowerBound, column.UpperBound, column.Variable.IsIntegral, column.Variable.Name);

    private static void Declare(Solver solver, LinearRow row, ImmutableDictionary<IVariable, Google.OrTools.LinearSolver.Variable> variables) {
        var constraint = solver.MakeConstraint(row.LowerBound, row.UpperBound);
        row.Coefficients.ToList().ForEach(term => constraint.SetCoefficient(variables[term.Key], term.Value));
    }

    private static void Declare(Objective objective, MilpProblem problem, ImmutableDictionary<IVariable, Google.OrTools.LinearSolver.Variable> variables) {
        problem.Objective.Coefficients.ToList().ForEach(term => objective.SetCoefficient(variables[term.Key], term.Value));
        objective.SetOffset(problem.Objective.Constant);
        objective.SetOptimizationDirection(maximize: problem.Sense == ObjectiveSense.Maximise);
    }

    private void Configure(Solver solver, MPSolverParameters parameters, SolverOptions options) {
        if (options.TimeLimit is { } timeLimit) {
            solver.SetTimeLimit((long)timeLimit.TotalMilliseconds);
        }
        if (options.Threads is { } threads) {
            solver.SetNumThreads(threads);
        }
        if (options.RelativeGap is { } gap) {
            parameters.SetDoubleParam(MPSolverParameters.DoubleParam.RELATIVE_MIP_GAP, gap);
        }
        if (options.Log is not null) {
            solver.EnableOutput();
        }
        if (SpecificParameters(options) is { Length: > 0 } specific && !solver.SetSolverSpecificParametersAsString(specific)) {
            throw new ArgumentException($"The solver rejected the parameters '{specific.ReplaceLineEndings("; ")}'.");
        }
    }

    /// <summary>
    /// OR-Tools has no absolute gap or seed of its own, so for SCIP they go by SCIP's names; the other
    /// solvers it drives go without. The caller's parameters follow, one <c>name = value</c> to a line,
    /// which is the form SCIP reads.
    /// </summary>
    private string SpecificParameters(SolverOptions options) =>
        string.Join('\n', ((IEnumerable<string?>)[
            options.AbsoluteGap is { } gap && SolverId == OrToolsSolverId.Scip ? $"limits/absgap = {gap.ToString("R", CultureInfo.InvariantCulture)}" : null,
            options.Seed is { } seed && SolverId == OrToolsSolverId.Scip ? $"randomization/randomseedshift = {seed}" : null,
            .. (options.Parameters ?? ImmutableSortedDictionary<string, string>.Empty).Select(parameter => $"{parameter.Key} = {parameter.Value}"),
        ]).OfType<string>());

    private static ISolveResult AsResult(Solver.ResultStatus status, Solver solver, ImmutableDictionary<IVariable, Google.OrTools.LinearSolver.Variable> variables) =>
        status switch {
            Solver.ResultStatus.OPTIMAL => new Optimal(ReadSolution(solver, variables)) { Statistics = ReadStatistics(solver, hasSolution: true) },
            Solver.ResultStatus.FEASIBLE => new Feasible(ReadSolution(solver, variables)) { Statistics = ReadStatistics(solver, hasSolution: true) },
            Solver.ResultStatus.INFEASIBLE => new Infeasible { Statistics = ReadStatistics(solver, hasSolution: false) },
            Solver.ResultStatus.UNBOUNDED => new Unbounded { Statistics = ReadStatistics(solver, hasSolution: false) },
            _ => new Unknown($"OR-Tools reported {status}.") { Statistics = ReadStatistics(solver, hasSolution: false) },
        };

    /// <summary>OR-Tools answers -1 for a count the underlying solver does not keep, and only has a bound once it has a solution.</summary>
    private static SolveStatistics ReadStatistics(Solver solver, bool hasSolution) =>
        SolveStatistics.None with {
            BestBound = hasSolution && solver.IsMip() && double.IsFinite(solver.Objective().BestBound()) ? solver.Objective().BestBound() : null,
            Nodes = solver.Nodes() >= 0 ? solver.Nodes() : null,
            Iterations = solver.Iterations() >= 0 ? solver.Iterations() : null,
        };

    private static Solution ReadSolution(Solver solver, ImmutableDictionary<IVariable, Google.OrTools.LinearSolver.Variable> variables) =>
        new(
            variables.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value.SolutionValue(), VariableOrder.Comparer),
            solver.Objective().Value());
}

/// <summary>Ready-made solvers backed by OR-Tools.</summary>
public static class OrToolsSolver {
    /// <summary>A solver for full problems, backed by the given OR-Tools solver (SCIP unless stated).</summary>
    public static ISolver Create(string solverId = OrToolsSolverId.Scip, EncodingOptions? encoding = null, SolverOptions? options = null) =>
        new MilpSolver(new OrToolsBackend(solverId), encoding, options);
}
