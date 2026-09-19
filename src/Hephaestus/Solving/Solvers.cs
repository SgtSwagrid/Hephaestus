using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Anything that can solve a problem. This is the seam along which solvers are swapped: a backend
/// that understands boolean structure natively (such as an SMT solver) implements it directly,
/// while MILP backends implement the much smaller <see cref="IMilpBackend"/> and are wrapped in a
/// <see cref="MilpSolver"/>.
/// </summary>
public interface ISolver {
    /// <summary>Solves the problem. Solving is a function of the problem: nothing is mutated and nothing is remembered.</summary>
    ISolveResult Solve(IProblem problem, CancellationToken cancellationToken = default);
}

/// <summary>A solver for plain mixed-integer linear programmes.</summary>
public interface IMilpBackend {
    /// <summary>Solves the programme, reporting values for every column.</summary>
    ISolveResult Solve(MilpProblem problem, SolverOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// A solver with indicator or half-reified constraints: it takes rows guarded by literals as they
/// stand, so nothing is relaxed with a big-M and no bounds are needed for the encoding.
/// </summary>
public interface IIndicatorBackend {
    /// <summary>Solves the problem, reporting values for every column.</summary>
    ISolveResult Solve(IndicatorProblem problem, SolverOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Turns any MILP backend into a solver for full problems: encode, relax the guards with derived
/// big-M values, solve, then hide the auxiliary variables and snap whole-number variables to whole numbers.
/// </summary>
public sealed record MilpSolver(
    IMilpBackend Backend,
    EncodingOptions? Encoding = null,
    SolverOptions? Options = null
) : ISolver {
    /// <inheritdoc/>
    public ISolveResult Solve(IProblem problem, CancellationToken cancellationToken = default) =>
        problem.Encode(Encoding) is var encoded && encoded.IsTriviallyInfeasible
            ? new Infeasible()
            : Backend.Solve(encoded, Options ?? SolverOptions.Default, cancellationToken).Select(solution => solution.Presentable(encoded.Columns));
}

/// <summary>
/// Turns any indicator backend into a solver for full problems: encode the logic, solve, then hide
/// the auxiliary variables and snap whole-number variables to whole numbers. No big-M is involved.
/// </summary>
public sealed record IndicatorSolver(
    IIndicatorBackend Backend,
    EncodingOptions? Encoding = null,
    SolverOptions? Options = null
) : ISolver {
    /// <inheritdoc/>
    public ISolveResult Solve(IProblem problem, CancellationToken cancellationToken = default) =>
        problem.EncodeLogic(Encoding) is var encoded && encoded.IsTriviallyInfeasible
            ? new Infeasible()
            : Backend.Solve(encoded, Options ?? SolverOptions.Default, cancellationToken).Select(solution => solution.Presentable(encoded.Columns));
}

/// <summary>Functions over solve results.</summary>
public static class SolveResults {
    extension(ISolveResult result) {
        /// <summary>Handles every possible outcome; adding a case to <see cref="ISolveResult"/> would break callers loudly rather than silently.</summary>
        public T Match<T>(Func<Solution, T> optimal, Func<Solution, T> feasible, Func<T> infeasible, Func<T> unbounded, Func<string, T> unknown) =>
            result switch {
                Optimal found => optimal(found.Solution),
                Feasible found => feasible(found.Solution),
                Infeasible => infeasible(),
                Unbounded => unbounded(),
                Unknown stopped => unknown(stopped.Reason),
                _ => throw new NotSupportedException($"Unknown kind of solve result: {result.GetType().Name}."),
            };

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
                Optimal found => new Optimal(selector(found.Solution)),
                Feasible found => new Feasible(selector(found.Solution)),
                _ => result,
            };
    }

    extension(Solution solution) {
        /// <summary>The solution as the modeller should see it: without auxiliary columns, and with whole-number variables snapped to whole numbers.</summary>
        public Solution Presentable(ImmutableArray<Column> columns) =>
            solution with {
                Values = columns
                    .Where(column => !column.IsAuxiliary)
                    .ToImmutableSortedDictionary(
                        column => column.Variable,
                        column => column.Variable.IsIntegral ? Math.Round(solution.Values[column.Variable]) : solution.Values[column.Variable],
                        VariableOrder.Comparer),
            };
    }

    extension(ISolver solver) {
        /// <summary>Solves on a background thread, for callers that must not block.</summary>
        public Task<ISolveResult> SolveAsync(IProblem problem, CancellationToken cancellationToken = default) =>
            Task.Run(() => solver.Solve(problem, cancellationToken), cancellationToken);
    }
}
