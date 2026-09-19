using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>An assignment of values to a problem's variables, as found by a solver.</summary>
public sealed record Solution(
    ImmutableSortedDictionary<IVariable, double> Values,
    double ObjectiveValue
);

/// <summary>
/// The outcome of solving a problem. The cases are <see cref="Optimal"/>, <see cref="Feasible"/>,
/// <see cref="Infeasible"/>, <see cref="Unbounded"/> and <see cref="Unknown"/>.
/// </summary>
public interface ISolveResult;

/// <summary>A solution that is proven to be the best possible.</summary>
public sealed record Optimal(Solution Solution) : ISolveResult;

/// <summary>A solution that satisfies the constraint but is not proven best, typically because a limit was reached.</summary>
public sealed record Feasible(Solution Solution) : ISolveResult;

/// <summary>The constraint cannot be satisfied.</summary>
public sealed record Infeasible : ISolveResult;

/// <summary>The objective can be improved without limit.</summary>
public sealed record Unbounded : ISolveResult;

/// <summary>The solver stopped without an answer.</summary>
public sealed record Unknown(string Reason) : ISolveResult;

/// <summary>Limits and tuning that apply to any solver.</summary>
/// <param name="TimeLimit">Stop and report the best solution found after this long.</param>
/// <param name="RelativeGap">Stop once the best solution is proven to be within this fraction of optimal.</param>
/// <param name="Threads">The number of threads the solver may use.</param>
public sealed record SolverOptions(
    TimeSpan? TimeLimit = null,
    double? RelativeGap = null,
    int? Threads = null
) {
    /// <summary>No limits; solver defaults throughout.</summary>
    public static SolverOptions Default { get; } = new();
}
