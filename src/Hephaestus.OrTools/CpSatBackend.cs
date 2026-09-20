using System.Collections.Immutable;
using System.Globalization;
using Google.OrTools.Sat;

namespace Hephaestus.OrTools;

/// <summary>
/// Solves whole-number problems with CP-SAT through its own interface. A guarded row becomes a
/// linear constraint that is only enforced if its guards hold, which is CP-SAT's native idiom, so
/// there is no big-M at all. CP-SAT reasons over finite whole-number domains: every variable must
/// be an integer or binary, and must be bounded (directly or by implication).
/// </summary>
public sealed record CpSatBackend : IIndicatorBackend {
    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">The problem has a continuous variable, or a coefficient that no power of ten makes whole.</exception>
    /// <exception cref="ModellingException">A variable has no finite bounds, stated or implied.</exception>
    public ISolveResult Solve(IndicatorProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
        var bounded = Bounded(Whole(problem).WithPropagatedBounds());
        var model = new CpModel();
        var variables = bounded.Columns.ToImmutableDictionary(column => column.Variable, column => Declare(model, column));
        bounded.Rows.ToList().ForEach(row => Declare(model, row, variables));
        Declare(model, bounded, variables);
        start.Where(entry => variables.ContainsKey(entry.Key)).ToList().ForEach(entry => model.AddHint(variables[entry.Key], (long)Math.Round(entry.Value)));

        var solver = new CpSolver { StringParameters = Parameters(options) };
        if (options.Log is { } log) {
            solver.SetLogCallback(new StringToVoidDelegate(log));
        }
        using var interruption = cancellationToken.Register(solver.StopSearch);
        return AsResult(solver.Solve(model), solver, model, bounded, variables);
    }

    private static IndicatorProblem Whole(IndicatorProblem problem) =>
        problem.Columns.Where(column => !column.Variable.IsIntegral).Select(column => $"'{column.Variable.Name}'").ToList() is { Count: > 0 } continuous
            ? throw new NotSupportedException(
                $"CP-SAT works in whole numbers, but {string.Join(", ", continuous.Take(5))}{(continuous.Count > 5 ? ", ..." : "")} "
                + "are continuous. Use integer variables (for typed variables, inWholeUnits: true), or a MILP solver.")
            : problem;

    private static IndicatorProblem Bounded(IndicatorProblem problem) =>
        problem.Columns.Where(column => double.IsInfinity(column.LowerBound) || double.IsInfinity(column.UpperBound)).Select(column => $"'{column.Variable.Name}'").ToList() is { Count: > 0 } unbounded
            ? throw new ModellingException(
                $"CP-SAT needs a finite domain for every variable, but {string.Join(", ", unbounded.Take(5))}{(unbounded.Count > 5 ? ", ..." : "")} "
                + "have no finite bounds, stated or implied. State them as ordinary constraints (for example 0 <= n & n <= 100).")
            : problem;

    private static IntVar Declare(CpModel model, Column column) =>
        column.Variable is BinaryVariable
            ? Restricted(model, model.NewBoolVar(column.Variable.Name), column)
            : model.NewIntVar((long)column.LowerBound, (long)column.UpperBound, column.Variable.Name);

    private static BoolVar Restricted(CpModel model, BoolVar variable, Column column) {
        if (column.LowerBound > 0 || column.UpperBound < 1) {
            model.AddLinearConstraint(variable, (long)column.LowerBound, (long)column.UpperBound);
        }
        return variable;
    }

    private static void Declare(CpModel model, GuardedRow row, ImmutableDictionary<IVariable, IntVar> variables) {
        var guards = row.Guards.Select(guard => AsLiteral(guard, variables)).ToArray();
        var scale = Scale(row.Expression);
        var limit = 0 - row.Expression.Constant * scale;
        var constraint = row.IsEquality && !IsWhole(limit)
            // A whole-valued expression can never equal a fraction, so the guards may not all hold.
            ? model.AddBoolOr(guards.Select(guard => guard.Not()))
            : model.AddLinearConstraint(Scaled(row.Expression, scale, variables), row.IsEquality ? Floor(limit) : long.MinValue, Floor(limit));
        if (guards.Length > 0) {
            constraint.OnlyEnforceIf(guards);
        }
    }

    private static void Declare(CpModel model, IndicatorProblem problem, ImmutableDictionary<IVariable, IntVar> variables) {
        var objective = Scaled(problem.Objective, Scale(problem.Objective), variables);
        if (problem.Sense == ObjectiveSense.Maximise) {
            model.Maximize(objective);
        } else {
            model.Minimize(objective);
        }
    }

    private static ILiteral AsLiteral(Literal literal, ImmutableDictionary<IVariable, IntVar> variables) =>
        literal.IsPositive ? (BoolVar)variables[literal.Variable] : ((BoolVar)variables[literal.Variable]).Not();

    private static LinearExpr Scaled(AffineForm form, double scale, ImmutableDictionary<IVariable, IntVar> variables) =>
        LinearExpr.WeightedSum(
            form.Coefficients.Keys.Select(variable => variables[variable]),
            form.Coefficients.Values.Select(coefficient => (long)Math.Round(coefficient * scale)));

    /// <summary>The smallest power of ten that makes every coefficient whole: CP-SAT takes whole coefficients only.</summary>
    private static double Scale(AffineForm form) =>
        Enumerable.Range(0, 10).Select(power => Math.Pow(10, power)).Where(scale => form.Coefficients.Values.All(coefficient => IsWhole(coefficient * scale))).Cast<double?>().FirstOrDefault()
        ?? throw new NotSupportedException($"CP-SAT takes whole coefficients only, and no power of ten up to a billion makes those of '{form.Format()}' whole.");

    private static bool IsWhole(double value) => Math.Abs(value - Math.Round(value)) <= Dust(value);

    /// <summary>The largest whole number not above <paramref name="value"/>, forgiving floating-point error a hair's breadth below a whole number.</summary>
    private static long Floor(double value) => (long)Math.Floor(value + Dust(value));

    /// <summary>The floating-point error to forgive in a number of this size: a few ulps, never enough to reach a neighbouring whole number.</summary>
    private static double Dust(double value) => 1e-9 + 1e-13 * Math.Abs(value);

    private static string Parameters(SolverOptions options) =>
        string.Join(' ', ((IEnumerable<string?>)[
            options.TimeLimit is { } timeLimit ? $"max_time_in_seconds:{timeLimit.TotalSeconds.ToString("R", CultureInfo.InvariantCulture)}" : null,
            options.Threads is { } threads ? $"num_workers:{threads}" : null,
            options.RelativeGap is { } gap ? $"relative_gap_limit:{gap.ToString("R", CultureInfo.InvariantCulture)}" : null,
            options.AbsoluteGap is { } absoluteGap ? $"absolute_gap_limit:{absoluteGap.ToString("R", CultureInfo.InvariantCulture)}" : null,
            options.Seed is { } seed ? $"random_seed:{seed}" : null,
            options.Log is null ? null : "log_search_progress:true log_to_stdout:false",
            .. (options.Parameters ?? ImmutableSortedDictionary<string, string>.Empty).Select(parameter => $"{parameter.Key}:{parameter.Value}"),
        ]).OfType<string>());

    private static ISolveResult AsResult(CpSolverStatus status, CpSolver solver, CpModel model, IndicatorProblem problem, ImmutableDictionary<IVariable, IntVar> variables) =>
        status switch {
            CpSolverStatus.Optimal => new Optimal(ReadSolution(solver, problem, variables)) { Statistics = ReadStatistics(solver, problem, hasSolution: true) },
            CpSolverStatus.Feasible => new Feasible(ReadSolution(solver, problem, variables)) { Statistics = ReadStatistics(solver, problem, hasSolution: true) },
            CpSolverStatus.Infeasible => new Infeasible { Statistics = ReadStatistics(solver, problem, hasSolution: false) },
            CpSolverStatus.ModelInvalid => new Unknown($"CP-SAT rejected the model: {model.Validate()}") { Statistics = ReadStatistics(solver, problem, hasSolution: false) },
            _ => new Unknown("CP-SAT stopped without a solution.") { Statistics = ReadStatistics(solver, problem, hasSolution: false) },
        };

    /// <summary>CP-SAT bounds the objective it was given, which was scaled to whole coefficients and shorn of its constant.</summary>
    private static SolveStatistics ReadStatistics(CpSolver solver, IndicatorProblem problem, bool hasSolution) =>
        SolveStatistics.None with {
            BestBound = hasSolution && !problem.Objective.IsConstant ? solver.BestObjectiveBound / Scale(problem.Objective) + problem.Objective.Constant : null,
            Nodes = solver.NumBranches(),
        };

    private static Solution ReadSolution(CpSolver solver, IndicatorProblem problem, ImmutableDictionary<IVariable, IntVar> variables) {
        var values = variables.ToImmutableSortedDictionary(entry => entry.Key, entry => (double)solver.Value(entry.Value), VariableOrder.Comparer);
        return new Solution(values, problem.Objective.Evaluate(variable => values[variable]));
    }
}

/// <summary>Ready-made CP-SAT solvers.</summary>
public static class CpSatSolver {
    /// <summary>A solver for whole-number problems, backed by CP-SAT through its own interface.</summary>
    public static ISolver Create(EncodingOptions? encoding = null, SolverOptions? options = null) =>
        new IndicatorSolver(new CpSatBackend(), encoding, options);
}
