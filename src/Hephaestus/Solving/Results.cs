using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>An assignment of values to a problem's variables, as found by a solver.</summary>
public sealed record Solution(
    ImmutableSortedDictionary<IVariable, double> Values,
    double ObjectiveValue
) {
    /// <summary>The solution that says nothing: the seed for a starting solution built by hand with <c>With</c>.</summary>
    public static Solution Empty { get; } = new(ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer), 0);
}

/// <summary>What a solve cost and how far it got, whatever its outcome. A figure the solver does not report is null.</summary>
/// <param name="EncodingTime">The time spent lowering the problem to the form the solver takes.</param>
/// <param name="SolvingTime">The time spent in the solver itself.</param>
/// <param name="BestBound">The value that no solution can better: a lower bound when minimising, an upper bound when maximising.</param>
/// <param name="Nodes">The number of branch-and-bound nodes (or, for CP-SAT, branches) explored.</param>
/// <param name="Iterations">The number of simplex iterations performed.</param>
public sealed record SolveStatistics(
    TimeSpan EncodingTime,
    TimeSpan SolvingTime,
    double? BestBound = null,
    long? Nodes = null,
    long? Iterations = null
) {
    /// <summary>Nothing measured and nothing reported.</summary>
    public static SolveStatistics None { get; } = new(TimeSpan.Zero, TimeSpan.Zero);
}

/// <summary>
/// The outcome of solving a problem. The cases are <see cref="Optimal"/>, <see cref="Feasible"/>,
/// <see cref="Infeasible"/>, <see cref="Unbounded"/> and <see cref="Unknown"/>.
/// </summary>
public interface ISolveResult {
    /// <summary>What the solve cost and how far it got.</summary>
    SolveStatistics Statistics { get; }
}

/// <summary>A solution that is proven to be the best possible.</summary>
public sealed record Optimal(Solution Solution) : ISolveResult {
    /// <inheritdoc/>
    public SolveStatistics Statistics { get; init; } = SolveStatistics.None;
}

/// <summary>A solution that satisfies the constraint but is not proven best, typically because a limit was reached.</summary>
public sealed record Feasible(Solution Solution) : ISolveResult {
    /// <inheritdoc/>
    public SolveStatistics Statistics { get; init; } = SolveStatistics.None;
}

/// <summary>The constraint cannot be satisfied.</summary>
public sealed record Infeasible : ISolveResult {
    /// <inheritdoc/>
    public SolveStatistics Statistics { get; init; } = SolveStatistics.None;
}

/// <summary>The objective can be improved without limit.</summary>
public sealed record Unbounded : ISolveResult {
    /// <inheritdoc/>
    public SolveStatistics Statistics { get; init; } = SolveStatistics.None;
}

/// <summary>The solver stopped without an answer.</summary>
public sealed record Unknown(string Reason) : ISolveResult {
    /// <inheritdoc/>
    public SolveStatistics Statistics { get; init; } = SolveStatistics.None;
}

/// <summary>
/// Limits and tuning. The named options mean the same to every solver that has them, and a solver
/// that lacks one ignores it: Z3 is exact, so gaps mean nothing to it, and OR-Tools passes a seed
/// or an absolute gap on to SCIP alone. <paramref name="Parameters"/> are the solver's own, and a
/// name or value it does not recognise is an error.
/// </summary>
/// <param name="TimeLimit">Stop and report the best solution found after this long.</param>
/// <param name="RelativeGap">Stop once the best solution is proven to be within this fraction of optimal.</param>
/// <param name="Threads">The number of threads the solver may use.</param>
/// <param name="AbsoluteGap">Stop once the best solution is proven to be within this much of optimal.</param>
/// <param name="Seed">The seed for the solver's random choices, for reproducible (or deliberately varied) runs.</param>
/// <param name="Log">
/// Receives the solver's log, a line or a chunk at a time, possibly from another thread. Gurobi and
/// CP-SAT deliver it here; the others can only write to standard output, and do so when this is set.
/// </param>
/// <param name="Parameters">
/// Parameters by the solver's own names, applied after everything else: <c>MIPFocus</c> for Gurobi,
/// <c>mip_heuristic_effort</c> for HiGHS, <c>limits/nodes</c> for SCIP, <c>linearization_level</c> for CP-SAT.
/// </param>
public sealed record SolverOptions(
    TimeSpan? TimeLimit = null,
    double? RelativeGap = null,
    int? Threads = null,
    double? AbsoluteGap = null,
    int? Seed = null,
    Action<string>? Log = null,
    ImmutableSortedDictionary<string, string>? Parameters = null
) {
    /// <summary>No limits; solver defaults throughout.</summary>
    public static SolverOptions Default { get; } = new();
}
