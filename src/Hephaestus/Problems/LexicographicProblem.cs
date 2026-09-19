using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>One objective among several.</summary>
/// <param name="Sense">Whether the expression is to be made small or large.</param>
/// <param name="Expression">The expression being optimised.</param>
/// <param name="AbsoluteTolerance">How much of this objective's optimum may be given up for the sake of those that follow it.</param>
/// <param name="RelativeTolerance">The same, as a fraction of the optimum. The two tolerances add.</param>
public sealed record Objective(
    ObjectiveSense Sense,
    ILinearExpression Expression,
    double AbsoluteTolerance = 0,
    double RelativeTolerance = 0
) {
    /// <summary>The objective of making <paramref name="expression"/> as small as possible.</summary>
    public static Objective Minimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
        new(ObjectiveSense.Minimise, expression, absoluteTolerance, relativeTolerance);

    /// <summary>The objective of making <paramref name="expression"/> as large as possible.</summary>
    public static Objective Maximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
        new(ObjectiveSense.Maximise, expression, absoluteTolerance, relativeTolerance);

    /// <summary>The objective of making a typed quantity as small as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Objective Minimise<T>(Quantity<T> quantity, T? tolerance = default, double relativeTolerance = 0) =>
        Minimise(quantity.Expression, Encoded(quantity.Projection, tolerance), relativeTolerance);

    /// <summary>The objective of making a typed quantity as large as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Objective Maximise<T>(Quantity<T> quantity, T? tolerance = default, double relativeTolerance = 0) =>
        Maximise(quantity.Expression, Encoded(quantity.Projection, tolerance), relativeTolerance);

    /// <summary>The objective of making a typed point as early as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Objective Minimise<T, TDelta>(Point<T, TDelta> point, TDelta? tolerance = default) =>
        Minimise(point.Expression, Encoded(point.Projection.Delta, tolerance));

    /// <summary>The objective of making a typed point as late as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Objective Maximise<T, TDelta>(Point<T, TDelta> point, TDelta? tolerance = default) =>
        Maximise(point.Expression, Encoded(point.Projection.Delta, tolerance));

    /// <summary>No tolerance at all is the default of every type, reference or value, and stands for none.</summary>
    private static double Encoded<T>(IProjection<T> projection, T? tolerance) =>
        tolerance is null || EqualityComparer<T>.Default.Equals(tolerance, default) ? 0 : Math.Abs(projection.Encode(tolerance));
}

/// <summary>
/// A problem with several objectives in order of priority: the first is optimised, then the second
/// among the solutions that are best for the first, and so on. (Objectives that are to be traded off
/// against each other need nothing special: weigh them into one expression.) An objective's tolerance
/// loosens its hold on those after it. Build one by following an ordinary problem with <c>Then</c>,
/// <c>ThenMinimise</c> or <c>ThenMaximise</c>, or all at once with <c>Problem.Lexicographic</c>.
/// </summary>
public sealed record LexicographicProblem(
    ImmutableArray<Objective> Objectives,
    IBooleanExpression Constraint
) : IMultipleObjectiveProblem;

/// <summary>Functions over objectives.</summary>
public static class Objectives {
    extension(Objective objective) {
        /// <summary>The ordinary problem of optimising this objective alone.</summary>
        public ISingleObjectiveProblem SubjectTo(IBooleanExpression constraint) =>
            (objective.Sense == ObjectiveSense.Minimise ? Problem.Minimise(objective.Expression) : Problem.Maximise(objective.Expression)).SubjectTo(constraint);

        /// <summary>
        /// The constraint that this objective does no worse than <paramref name="value"/>, within its
        /// tolerances and a little more. The value is what a solver found, to within its own tolerances:
        /// SCIP or HiGHS will report 119.999999 where only 120 is attainable, and hold a later stage to
        /// that exactly and it is infeasible. So a hundred-thousandth is allowed (and a billionth of the
        /// value, for the rounding of large numbers), but never as much as half a unit, so that a
        /// whole-valued objective is still held to its value exactly.
        /// </summary>
        public IBooleanExpression NoWorseThan(double value) =>
            objective.Sense == ObjectiveSense.Minimise
                ? objective.Expression <= value + objective.Slack(value)
                : objective.Expression >= value - objective.Slack(value);

        private double Slack(double value) =>
            objective.AbsoluteTolerance + objective.RelativeTolerance * Math.Abs(value) + Math.Min(1e-5 + 1e-9 * Math.Abs(value), 0.5);
    }
}
