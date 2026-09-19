using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>Whether the objective of a <see cref="MilpProblem"/> is to be made small or large.</summary>
public enum ObjectiveSense {
    /// <summary>Smaller is better.</summary>
    Minimise,

    /// <summary>Larger is better.</summary>
    Maximise,
}

/// <summary>
/// A variable as a MILP solver sees it: with native bounds. The bounds are those the problem's
/// top-level constraints state outright (<c>x &lt;= 10</c>), or the variable's intrinsic ones.
/// </summary>
public sealed record Column(
    IVariable Variable,
    double LowerBound,
    double UpperBound,
    bool IsAuxiliary
);

/// <summary>The linear constraint <c>LowerBound &lt;= &#931; coefficient &#183; variable &lt;= UpperBound</c>.</summary>
public sealed record LinearRow(
    ImmutableSortedDictionary<IVariable, double> Coefficients,
    double LowerBound,
    double UpperBound
) {
    /// <summary>The row in mathematical notation, since the default rendering of a dictionary says nothing.</summary>
    public override string ToString() => this.Format();
}

/// <summary>
/// A problem lowered to a plain mixed-integer linear programme: bounded columns, linear rows and a
/// linear objective. This is all a MILP backend ever needs to understand.
/// </summary>
public sealed record MilpProblem(
    ImmutableArray<Column> Columns,
    ImmutableArray<LinearRow> Rows,
    ObjectiveSense Sense,
    AffineForm Objective
);

/// <summary>Settings for lowering a problem to a <see cref="MilpProblem"/>.</summary>
/// <param name="StrictnessEpsilon">
/// The gap that stands in for strictness over the reals: <c>a &lt; b</c> is encoded as
/// <c>a + epsilon &lt;= b</c>. It should comfortably exceed the solver's feasibility tolerance.
/// Whole-valued comparisons use a gap of exactly one and are unaffected.
/// </param>
/// <param name="FallbackBigM">
/// The big-M to use when none can be derived because an expression is unbounded. Leave it unset
/// (the default) to get an error naming the unbounded variables instead: a guessed big-M that is
/// too small silently cuts off solutions, and one that is too large invites numerical trouble.
/// </param>
/// <param name="BoundPropagationRounds">
/// The most sweeps of bound propagation to run over the unconditional constraints. The first sweep
/// reads off stated bounds such as <c>x &lt;= 10</c>; later ones derive implied bounds.
/// </param>
/// <param name="AuxiliaryPrefix">The prefix for the names of auxiliary binary variables.</param>
public sealed record EncodingOptions(
    double StrictnessEpsilon = 1e-4,
    double? FallbackBigM = null,
    int BoundPropagationRounds = 10,
    string AuxiliaryPrefix = "_aux"
) {
    /// <summary>The default settings.</summary>
    public static EncodingOptions Default { get; } = new();
}

/// <summary>Functions over <see cref="MilpProblem"/>.</summary>
public static class MilpProblems {
    extension(MilpProblem problem) {
        /// <summary>Whether infeasibility is evident without solving: contradictory stated bounds, or a constant row that fails.</summary>
        public bool IsTriviallyInfeasible =>
            problem.Columns.Any(column => column.LowerBound > column.UpperBound)
            || problem.Rows.Any(row => row.Coefficients.IsEmpty && !(row.LowerBound <= 0 && 0 <= row.UpperBound));
    }
}
