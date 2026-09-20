using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A problem lowered far enough for a backend to take: bounded columns and a linear objective,
/// whatever shape its rows have. The cases are <see cref="IndicatorProblem"/>, whose rows are
/// guarded by literals, and <see cref="MilpProblem"/>, whose rows are plain. What a solver does
/// either side of handing one over is the same, so it is written once against this.
/// </summary>
public interface ILoweredProblem {
    /// <summary>The variables, with the bounds the solver is given.</summary>
    ImmutableArray<Column> Columns { get; }

    /// <summary>Whether the objective is to be made small or large.</summary>
    ObjectiveSense Sense { get; }

    /// <summary>The expression to optimise.</summary>
    AffineForm Objective { get; }

    /// <summary>
    /// Whether infeasibility is evident without solving: bounds that contradict each other, or a row
    /// that fails whatever the columns are. Worth asking before troubling a solver, and before a
    /// backend that needs finite domains is handed an empty one.
    /// </summary>
    bool IsTriviallyInfeasible { get; }
}
