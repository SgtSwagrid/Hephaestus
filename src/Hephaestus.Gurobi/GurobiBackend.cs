using System.Collections.Immutable;
using Gurobi;

namespace Hephaestus.Gurobi;

/// <summary>
/// Solves problems with Gurobi through its own .NET interface. As an <see cref="IIndicatorBackend"/>
/// it turns every guarded row into a Gurobi indicator constraint, so that no big-M appears anywhere;
/// as an <see cref="IMilpBackend"/> it takes the classic formulation with derived big-M values.
/// </summary>
public sealed record GurobiBackend : IIndicatorBackend, IMilpBackend {
    /// <inheritdoc/>
    public ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) =>
        Solve(problem.AsIndicatorProblem(), start, options, cancellationToken).Select(solution => solution with { RowDuals = PerRow(problem, solution.RowDuals) });

    /// <summary>
    /// A row reaches Gurobi as an equation, or as its upper side followed by its lower side negated,
    /// whichever of those it has. So the dual of a row is that of its upper side less that of its lower.
    /// </summary>
    private static ImmutableArray<double> PerRow(MilpProblem problem, ImmutableArray<double> duals) =>
        duals.IsEmpty
            ? duals
            : [.. problem.Rows.Aggregate((Next: 0, Duals: ImmutableList<double>.Empty), (taken, row) => (taken.Next + Signs(row).Length, taken.Duals.Add(Signs(row).Select((sign, part) => sign * duals[taken.Next + part]).Sum()))).Duals];

    private static ImmutableArray<int> Signs(LinearRow row) =>
        row.LowerBound == row.UpperBound ? [1]
        : [.. double.IsPositiveInfinity(row.UpperBound) ? (int[])[] : [1], .. double.IsNegativeInfinity(row.LowerBound) ? (int[])[] : [-1]];

    /// <inheritdoc/>
    public ISolveResult Solve(IndicatorProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken) {
        using var environment = Quietly();
        using var model = new GRBModel(environment);
        using var interruption = cancellationToken.Register(model.Terminate);

        // A Gurobi indicator constraint hangs off a single binary, so conjunctions of guards are named first.
        var singlyGuarded = problem.WithSingleGuards();
        var variables = singlyGuarded.Columns.ToImmutableDictionary(column => column.Variable, column => Declare(model, column));
        singlyGuarded.Rows.ToList().ForEach(row => Declare(model, row, variables));
        // Gurobi completes a start that leaves variables out, so the auxiliaries need no values.
        start.Where(entry => variables.ContainsKey(entry.Key)).ToList().ForEach(entry => variables[entry.Key].Start = entry.Value);
        model.SetObjective(Linear(singlyGuarded.Objective, variables), singlyGuarded.Sense == ObjectiveSense.Maximise ? GRB.MAXIMIZE : GRB.MINIMIZE);
        Configure(model, options);
        using var logging = Logging(model, options.Log);
        model.Optimize();

        return AsResult(Settled(model), model, variables);
    }

    /// <summary>An environment that prints nothing, not even the licence banner, which is why the flag is set before it starts.</summary>
    private static GRBEnv Quietly() {
        var environment = new GRBEnv(empty: true);
        environment.Set(GRB.IntParam.OutputFlag, 0);
        environment.Start();
        return environment;
    }

    private static GRBVar Declare(GRBModel model, Column column) =>
        model.AddVar(
            Math.Max(column.LowerBound, -GRB.INFINITY),
            Math.Min(column.UpperBound, GRB.INFINITY),
            0,
            column.Variable switch {
                BinaryVariable => GRB.BINARY,
                IntegerVariable => GRB.INTEGER,
                _ => GRB.CONTINUOUS,
            },
            column.Variable.Name);

    private static void Declare(GRBModel model, GuardedRow row, ImmutableDictionary<IVariable, GRBVar> variables) {
        var body = Linear(new AffineForm(row.Expression.Coefficients, 0), variables);
        var sense = row.IsEquality ? GRB.EQUAL : GRB.LESS_EQUAL;
        if (row.Guards is [var guard]) {
            model.AddGenConstrIndicator(variables[guard.Variable], guard.IsPositive ? 1 : 0, body, sense, 0 - row.Expression.Constant, null);
        } else {
            model.AddConstr(body, sense, 0 - row.Expression.Constant, null);
        }
    }

    private static GRBLinExpr Linear(AffineForm form, ImmutableDictionary<IVariable, GRBVar> variables) {
        var expression = new GRBLinExpr(form.Constant);
        form.Coefficients.ToList().ForEach(term => expression.AddTerm(term.Value, variables[term.Key]));
        return expression;
    }

    private static void Configure(GRBModel model, SolverOptions options) {
        if (options.TimeLimit is { } timeLimit) {
            model.Parameters.TimeLimit = timeLimit.TotalSeconds;
        }
        if (options.Threads is { } threads) {
            model.Parameters.Threads = threads;
        }
        if (options.RelativeGap is { } gap) {
            model.Parameters.MIPGap = gap;
        }
        if (options.AbsoluteGap is { } absoluteGap) {
            model.Parameters.MIPGapAbs = absoluteGap;
        }
        if (options.Seed is { } seed) {
            model.Parameters.Seed = seed;
        }
        (options.Parameters ?? ImmutableSortedDictionary<string, string>.Empty).ToList().ForEach(parameter => model.Set(parameter.Key, parameter.Value));
    }

    /// <summary>Gurobi only produces its log while output is on, so it is turned on, but kept off the console.</summary>
    private static LogCallback? Logging(GRBModel model, Action<string>? log) {
        if (log is null) {
            return null;
        }
        model.Parameters.OutputFlag = 1;
        model.Parameters.LogToConsole = 0;
        var callback = new LogCallback(log);
        model.SetCallback(callback);
        return callback;
    }

    /// <summary>Gurobi's callbacks are by inheritance, hence a class. Disposing is only there for the <c>using</c> that keeps it alive.</summary>
    private sealed class LogCallback(Action<string> log) : GRBCallback, IDisposable {
        protected override void Callback() {
            if (where == GRB.Callback.MESSAGE) {
                log(GetStringInfo(GRB.Callback.MSG_STRING));
            }
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Presolve sometimes cannot tell infeasible from unbounded. Solving again without dual
    /// reductions settles which, and costs nothing in the usual case.
    /// </summary>
    private static int Settled(GRBModel model) {
        if (model.Status == GRB.Status.INF_OR_UNBD) {
            model.Parameters.DualReductions = 0;
            model.Optimize();
        }
        return model.Status;
    }

    private static ISolveResult AsResult(int status, GRBModel model, ImmutableDictionary<IVariable, GRBVar> variables) =>
        status switch {
            GRB.Status.OPTIMAL => new Optimal(ReadSolution(model, variables)) { Statistics = ReadStatistics(model) },
            GRB.Status.INFEASIBLE => new Infeasible { Statistics = ReadStatistics(model) },
            GRB.Status.UNBOUNDED => new Unbounded { Statistics = ReadStatistics(model) },
            _ when model.SolCount > 0 => new Feasible(ReadSolution(model, variables)) { Statistics = ReadStatistics(model) },
            _ => new Unknown($"Gurobi stopped with status {status} and no solution.") { Statistics = ReadStatistics(model) },
        };

    private static SolveStatistics ReadStatistics(GRBModel model) =>
        SolveStatistics.None with {
            BestBound = Attribute(() => model.ObjBound) is { } bound && Math.Abs(bound) < GRB.INFINITY ? bound : null,
            Nodes = (long?)Attribute(() => model.NodeCount),
            Iterations = (long?)Attribute(() => model.IterCount),
        };

    /// <summary>Which attributes exist depends on the kind of model and how far the solve got; one that is not there is simply not reported.</summary>
    private static double? Attribute(Func<double> read) {
        try {
            return read();
        } catch (GRBException) {
            return null;
        }
    }

    private static Solution ReadSolution(GRBModel model, ImmutableDictionary<IVariable, GRBVar> variables) =>
        new(
            variables.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value.X, VariableOrder.Comparer),
            model.ObjVal) {
            // A linear programme has one Gurobi constraint to a row, in order, because a row with two finite sides is never passed on as one.
            RowDuals = model.IsMIP == 0 && model.Status == GRB.Status.OPTIMAL ? [.. model.GetConstrs().Select(constraint => constraint.Pi)] : [],
        };
}

/// <summary>Ready-made solvers backed by Gurobi.</summary>
public static class GurobiSolver {
    /// <summary>
    /// A solver for full problems. By default conditional constraints become indicator constraints,
    /// which need neither a big-M nor bounds; set <paramref name="useIndicators"/> to false for the
    /// classic formulation with a big-M derived for each row.
    /// </summary>
    public static ISolver Create(EncodingOptions? encoding = null, SolverOptions? options = null, bool useIndicators = true) =>
        useIndicators
            ? new IndicatorSolver(new GurobiBackend(), encoding, options)
            : new MilpSolver(new GurobiBackend(), encoding, options);
}
