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
    public ISolveResult Solve(MilpProblem problem, SolverOptions options, CancellationToken cancellationToken) =>
        Solve(problem.AsIndicatorProblem(), options, cancellationToken);

    /// <inheritdoc/>
    public ISolveResult Solve(IndicatorProblem problem, SolverOptions options, CancellationToken cancellationToken) {
        using var environment = Quietly();
        using var model = new GRBModel(environment);
        using var interruption = cancellationToken.Register(model.Terminate);

        // A Gurobi indicator constraint hangs off a single binary, so conjunctions of guards are named first.
        var singlyGuarded = problem.WithSingleGuards();
        var variables = singlyGuarded.Columns.ToImmutableDictionary(column => column.Variable, column => Declare(model, column));
        singlyGuarded.Rows.ToList().ForEach(row => Declare(model, row, variables));
        model.SetObjective(Linear(singlyGuarded.Objective, variables), singlyGuarded.Sense == ObjectiveSense.Maximise ? GRB.MAXIMIZE : GRB.MINIMIZE);
        Configure(model, options);
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
            GRB.Status.OPTIMAL => new Optimal(ReadSolution(model, variables)),
            GRB.Status.INFEASIBLE => new Infeasible(),
            GRB.Status.UNBOUNDED => new Unbounded(),
            _ when model.SolCount > 0 => new Feasible(ReadSolution(model, variables)),
            _ => new Unknown($"Gurobi stopped with status {status} and no solution."),
        };

    private static Solution ReadSolution(GRBModel model, ImmutableDictionary<IVariable, GRBVar> variables) =>
        new(
            variables.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value.X, VariableOrder.Comparer),
            model.ObjVal);
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
