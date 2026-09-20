using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Anything that can solve a problem. This is the seam along which solvers are swapped: a backend
/// that understands boolean structure natively (such as an SMT solver) implements it directly,
/// while MILP backends implement the much smaller <see cref="IMilpBackend"/> and are wrapped in a
/// <see cref="MilpSolver"/>.
/// </summary>
public interface ISolver {
    /// <summary>Solves the problem. Solving is a function of its arguments: nothing is mutated and nothing is remembered.</summary>
    /// <param name="problem">The problem to solve.</param>
    /// <param name="startingFrom">
    /// A solution to start the search from: that of an earlier, similar problem, or one built by hand
    /// from <see cref="Solution.Empty"/>. It is a hint and nothing more. It may cover only some of the
    /// variables, or mention others, or not be feasible at all; the answer is the same, only perhaps sooner.
    /// </param>
    /// <param name="cancellationToken">Stops the solve early.</param>
    ISolveResult Solve(ISingleObjectiveProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default);
}

/// <summary>A solver for plain mixed-integer linear programmes.</summary>
public interface IMilpBackend {
    /// <summary>Solves the programme, reporting values for every column.</summary>
    /// <param name="problem">The programme to solve.</param>
    /// <param name="start">Values to start the search from, for some of the columns, all of them or none.</param>
    /// <param name="options">Limits and tuning.</param>
    /// <param name="cancellationToken">Stops the solve early.</param>
    ISolveResult Solve(MilpProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// A solver with indicator or half-reified constraints: it takes rows guarded by literals as they
/// stand, so nothing is relaxed with a big-M and no bounds are needed for the encoding.
/// </summary>
public interface IIndicatorBackend {
    /// <summary>Solves the problem, reporting values for every column.</summary>
    /// <param name="problem">The problem to solve.</param>
    /// <param name="start">Values to start the search from, for some of the columns, all of them or none.</param>
    /// <param name="options">Limits and tuning.</param>
    /// <param name="cancellationToken">Stops the solve early.</param>
    ISolveResult Solve(IndicatorProblem problem, IReadOnlyDictionary<IVariable, double> start, SolverOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Turns any MILP backend into a solver for full problems: encode, relax the guards with derived
/// big-M values, solve, then hide the auxiliary variables and snap whole-number variables to whole numbers.
/// </summary>
public sealed record MilpSolver(
    IMilpBackend Backend,
    EncodingOptions? Encoding = null,
    SolverOptions? Options = null
) : IConflictSolver {
    /// <inheritdoc/>
    public ISolveResult Solve(ISingleObjectiveProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
        BackendSolving.Solve(
            problem,
            () => problem.Encode(Encoding),
            (encoded, start) => Backend.Solve(encoded, start, Options ?? SolverOptions.Default, cancellationToken),
            startingFrom);

    /// <inheritdoc/>
    /// <remarks>The logic is encoded without big-M for this, whatever the solver is otherwise given: a conflict is a fact about the problem, not about a formulation of it.</remarks>
    public ImmutableArray<IBooleanExpression> NarrowConflict(ISingleObjectiveProblem problem, CancellationToken cancellationToken = default) =>
        BackendSolving.NarrowConflict(problem, Backend as IConflictBackend, Encoding, Options, cancellationToken);
}

/// <summary>
/// Turns any indicator backend into a solver for full problems: encode the logic, solve, then hide
/// the auxiliary variables and snap whole-number variables to whole numbers. No big-M is involved.
/// </summary>
public sealed record IndicatorSolver(
    IIndicatorBackend Backend,
    EncodingOptions? Encoding = null,
    SolverOptions? Options = null
) : IConflictSolver {
    /// <inheritdoc/>
    public ISolveResult Solve(ISingleObjectiveProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
        BackendSolving.Solve(
            problem,
            () => problem.EncodeLogic(Encoding),
            (encoded, start) => Backend.Solve(encoded, start, Options ?? SolverOptions.Default, cancellationToken),
            startingFrom);

    /// <inheritdoc/>
    /// <inheritdoc cref="MilpSolver.NarrowConflict" path="/remarks"/>
    public ImmutableArray<IBooleanExpression> NarrowConflict(ISingleObjectiveProblem problem, CancellationToken cancellationToken = default) =>
        BackendSolving.NarrowConflict(problem, Backend as IConflictBackend, Encoding, Options, cancellationToken);
}

/// <summary>
/// What every solver wrapper does either side of its backend, whichever form of problem that backend
/// takes. Only the encoding and the call itself differ between them.
/// </summary>
internal static class BackendSolving {
    /// <summary>Encodes, solves, and presents the answer in the modeller's terms; an infeasibility the encoding already settles is not put to the backend at all.</summary>
    public static ISolveResult Solve<TProblem>(
        ISingleObjectiveProblem problem,
        Func<TProblem> encode,
        Func<TProblem, IReadOnlyDictionary<IVariable, double>, ISolveResult> solve,
        Solution? startingFrom
    ) where TProblem : ILoweredProblem =>
        Solved(problem, Timed.Run(encode), solve, startingFrom);

    private static ISolveResult Solved<TProblem>(
        ISingleObjectiveProblem problem,
        (TProblem Encoded, TimeSpan Elapsed) encoding,
        Func<TProblem, IReadOnlyDictionary<IVariable, double>, ISolveResult> solve,
        Solution? startingFrom
    ) where TProblem : ILoweredProblem =>
        encoding.Encoded.IsTriviallyInfeasible
            ? new Infeasible { Statistics = new SolveStatistics(encoding.Elapsed, TimeSpan.Zero) }
            : Timed.Run(() => solve(encoding.Encoded, startingFrom.Over(encoding.Encoded.Columns)))
                .Presentable(encoding.Encoded.Columns, problem.Objective.Expression, encoding.Elapsed);

    /// <summary>A conflict as the backend narrows it, where the backend can; empty where it cannot.</summary>
    public static ImmutableArray<IBooleanExpression> NarrowConflict(ISingleObjectiveProblem problem, IConflictBackend? backend, EncodingOptions? encoding, SolverOptions? options, CancellationToken cancellationToken) =>
        backend is null ? [] : Conflicts.Narrow(problem.EncodeLogic(encoding), backend, options ?? SolverOptions.Default, cancellationToken);
}

/// <summary>Functions over solve results.</summary>
public static class SolveResults {
    extension(ISolveResult result) {
        /// <summary>The solution, whether or not it is proven optimal; null when there is none.</summary>
        public Solution? SolutionOrNull =>
            result switch {
                Optimal found => found.Solution,
                Feasible found => found.Solution,
                _ => null,
            };

        /// <summary>The same outcome with its solution, if any, transformed.</summary>
        public ISolveResult Select(Func<Solution, Solution> selector) =>
            result switch {
                Optimal found => found with { Solution = selector(found.Solution) },
                Feasible found => found with { Solution = selector(found.Solution) },
                _ => result,
            };

        /// <summary>The same outcome with other statistics.</summary>
        public ISolveResult With(SolveStatistics statistics) =>
            result switch {
                Optimal found => found with { Statistics = statistics },
                Feasible found => found with { Statistics = statistics },
                Infeasible found => found with { Statistics = statistics },
                Unbounded found => found with { Statistics = statistics },
                Unknown found => found with { Statistics = statistics },
                _ => throw new NotSupportedException($"Unknown kind of solve result: {result.GetType().Name}."),
            };

        /// <summary>
        /// How far the solution might be from optimal, as the solver's bound has it; null without a
        /// solution or a bound. Zero, or as good as, for an <see cref="Optimal"/> result.
        /// </summary>
        public double? AbsoluteGap =>
            result.SolutionOrNull is { } solution && result.Statistics.BestBound is { } bound
                ? Math.Abs(solution.ObjectiveValue - bound)
                : null;

        /// <summary>The absolute gap as a fraction of the objective value.</summary>
        public double? RelativeGap =>
            result.AbsoluteGap is { } gap && result.SolutionOrNull is { } solution
                ? gap / Math.Max(Math.Abs(solution.ObjectiveValue), 1e-10)
                : null;
    }

    extension((ISolveResult Result, TimeSpan Elapsed) solved) {
        /// <summary>What a backend returned, as the modeller should see it, with the times filled in.</summary>
        public ISolveResult Presentable(ImmutableArray<Column> columns, ILinearExpression objective, TimeSpan encodingTime) =>
            solved.Result
                .Select(solution => solution.Presentable(columns, objective))
                .With(solved.Result.Statistics with { EncodingTime = encodingTime, SolvingTime = solved.Elapsed });
    }

    extension(SolverOptions options) {
        /// <summary>These options with one more of the solver's own parameters.</summary>
        public SolverOptions With(string parameter, string value) =>
            options with { Parameters = (options.Parameters ?? ImmutableSortedDictionary<string, string>.Empty).SetItem(parameter, value) };
    }

    extension(Solution? start) {
        /// <summary>
        /// The part of a starting solution that a backend can use: its values for the columns of the
        /// encoded problem, whole where the column is. Auxiliary columns are left for the solver to
        /// fill in, which it does readily once the modeller's own variables are given.
        /// </summary>
        public IReadOnlyDictionary<IVariable, double> Over(ImmutableArray<Column> columns) =>
            columns
                .Where(column => start is not null && start.Values.ContainsKey(column.Variable))
                .ToImmutableDictionary(column => column.Variable, column => column.Variable.IsIntegral ? Math.Round(start!.Values[column.Variable]) : start!.Values[column.Variable]);
    }

    extension(Solution solution) {
        /// <summary>
        /// The solution as the modeller should see it: without auxiliary columns, with whole-number
        /// variables snapped to whole numbers, and with the objective as written read off the result.
        /// (The solver's own figure may count an auxiliary variable that was left slack.)
        /// </summary>
        public Solution Presentable(ImmutableArray<Column> columns, ILinearExpression objective) =>
            solution.WithValues(columns
                .Where(column => !column.IsAuxiliary)
                .ToImmutableSortedDictionary(
                    column => column.Variable,
                    column => column.Variable.IsIntegral ? Math.Round(solution.Values[column.Variable]) : solution.Values[column.Variable],
                    VariableOrder.Comparer), objective);

        /// <summary>The solution over other values, with the objective read off them.</summary>
        public Solution WithValues(ImmutableSortedDictionary<IVariable, double> values, ILinearExpression objective) =>
            (solution with { Values = values }) is var presented ? presented with { ObjectiveValue = presented.Value(objective) } : solution;
    }

    extension(ISolver solver) {
        /// <summary>Solves on a background thread, for callers that must not block.</summary>
        public Task<ISolveResult> SolveAsync(IProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
            Task.Run(() => solver.Solve(problem, startingFrom, cancellationToken), cancellationToken);
    }
}
