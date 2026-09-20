using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// What a problem is to optimise. It is a value in its own right, which can be built once and set
/// against one constraint after another. It is either an <see cref="ISingleObjective"/> or an
/// <see cref="ILexicographicObjective"/>, and it is the objective that makes a problem an
/// <see cref="IOneShotProblem"/> or an <see cref="IMultipleObjectiveProblem"/>.
/// </summary>
public interface IObjective;

/// <summary>
/// An objective that a solver pursues in one go. The cases are <see cref="NoObjective"/> and
/// <see cref="Optimisation"/>.
/// </summary>
public interface ISingleObjective : IObjective;

/// <summary>Nothing is to be optimised: any assignment under which the constraint holds will do. Written <see cref="Objective.None"/>.</summary>
public sealed record NoObjective : ISingleObjective;

/// <summary>An expression to be made as small or as large as possible.</summary>
public sealed record Optimisation(
    ObjectiveSense Sense,
    ILinearExpression Expression
) : ISingleObjective;

/// <summary>
/// Several objectives in order of priority: the first is optimised, then the second among the
/// solutions that are best for the first, and so on. (Objectives that are to be traded off against
/// each other need nothing special: weigh them into one expression.) The one case so far is
/// <see cref="LexicographicObjective"/>.
/// </summary>
public interface ILexicographicObjective : IObjective {
    /// <summary>The objectives, most important first.</summary>
    ImmutableArray<Prioritised> Priorities { get; }
}

/// <summary>An objective in its place among several, and how much of its optimum it may give up for the sake of those after it.</summary>
/// <param name="Objective">The objective.</param>
/// <param name="AbsoluteTolerance">How much of the optimum may be given up.</param>
/// <param name="RelativeTolerance">The same, as a fraction of the optimum. The two tolerances add.</param>
public sealed record Prioritised(
    Optimisation Objective,
    double AbsoluteTolerance = 0,
    double RelativeTolerance = 0
) {
    /// <summary>An objective takes its place among several as it stands, with no tolerance.</summary>
    public static implicit operator Prioritised(Optimisation objective) => new(objective);
}

/// <inheritdoc cref="ILexicographicObjective"/>
public sealed record LexicographicObjective(ImmutableArray<Prioritised> Priorities) : ILexicographicObjective;

/// <summary>
/// Where objectives start: <c>Objective.Minimise(delay)</c>, or
/// <c>Objective.Minimise(delay).Then(Objective.Maximise(slack))</c>. Given a tolerance, an objective
/// is one that takes its place among several.
/// </summary>
public static class Objective {
    /// <summary>The objective of optimising nothing: any assignment under which the constraint holds will do.</summary>
    public static NoObjective None { get; } = new();

    /// <summary>The objective of making <paramref name="expression"/> as small as possible.</summary>
    public static Optimisation Minimise(ILinearExpression expression) => new(ObjectiveSense.Minimise, expression);

    /// <summary>The objective of making <paramref name="expression"/> as large as possible.</summary>
    public static Optimisation Maximise(ILinearExpression expression) => new(ObjectiveSense.Maximise, expression);

    /// <summary>The objective of making <paramref name="expression"/> as small as possible, give or take a tolerance for the sake of the objectives after it.</summary>
    public static Prioritised Minimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => new(Minimise(expression), absoluteTolerance, relativeTolerance);

    /// <summary>The objective of making <paramref name="expression"/> as large as possible, give or take a tolerance for the sake of the objectives after it.</summary>
    public static Prioritised Maximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => new(Maximise(expression), absoluteTolerance, relativeTolerance);

    /// <summary>The objective of making a typed expression as small as possible.</summary>
    public static Optimisation Minimise<TValue>(ILinearlyEncodable<TValue> quantity) => Minimise(quantity.Expression);

    /// <summary>The objective of making a typed expression as large as possible.</summary>
    public static Optimisation Maximise<TValue>(ILinearlyEncodable<TValue> quantity) => Maximise(quantity.Expression);

    /// <summary>The objective of making a typed quantity as small as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Prioritised Minimise<T>(Quantity<T> quantity, T? tolerance = default, double relativeTolerance = 0) => new(Minimise(quantity.Expression), Encoded(quantity.Projection, tolerance), relativeTolerance);

    /// <summary>The objective of making a typed quantity as large as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Prioritised Maximise<T>(Quantity<T> quantity, T? tolerance = default, double relativeTolerance = 0) => new(Maximise(quantity.Expression), Encoded(quantity.Projection, tolerance), relativeTolerance);

    /// <summary>The objective of making a typed point as early as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Prioritised Minimise<T, TDelta>(Point<T, TDelta> point, TDelta tolerance) => new(Minimise(point), Math.Abs(point.Projection.Delta.Encode(tolerance)));

    /// <summary>The objective of making a typed point as late as possible, give or take <paramref name="tolerance"/>.</summary>
    public static Prioritised Maximise<T, TDelta>(Point<T, TDelta> point, TDelta tolerance) => new(Maximise(point), Math.Abs(point.Projection.Delta.Encode(tolerance)));

    /// <summary>No tolerance at all is the default of every type, reference or value, and stands for none.</summary>
    private static double Encoded<T>(IProjection<T> projection, T? tolerance) =>
        tolerance is null || EqualityComparer<T>.Default.Equals(tolerance, default) ? 0 : Math.Abs(projection.Encode(tolerance));

    /// <summary>The objectives in order of priority.</summary>
    public static ILexicographicObjective InOrder(IEnumerable<Prioritised> priorities) => new LexicographicObjective([.. priorities]);
}

/// <summary>Functions over objectives.</summary>
public static class Objectives {
    extension(IObjective objective) {
        /// <summary>The single objectives that this comes down to, most important first: none, one or several.</summary>
        public ImmutableArray<Prioritised> Priorities =>
            objective switch {
                NoObjective => [],
                Optimisation optimisation => [new Prioritised(optimisation)],
                ILexicographicObjective lexicographic => lexicographic.Priorities,
                _ => throw new NotSupportedException($"Unknown kind of objective: {objective.GetType().Name}."),
            };

        /// <summary>This objective with another after it, which matters only among the solutions that are best for this one.</summary>
        public ILexicographicObjective Then(Prioritised next) => new LexicographicObjective(objective.Priorities.Add(next));

        /// <summary>This objective with further ones after it, to be made small, in order.</summary>
        public ILexicographicObjective ThenMinimise(params IEnumerable<ILinearExpression> expressions) =>
            expressions.Aggregate(Objective.InOrder(objective.Priorities), (chained, expression) => chained.Then(Objective.Minimise(expression)));

        /// <summary>This objective with further ones after it, to be made large, in order.</summary>
        public ILexicographicObjective ThenMaximise(params IEnumerable<ILinearExpression> expressions) =>
            expressions.Aggregate(Objective.InOrder(objective.Priorities), (chained, expression) => chained.Then(Objective.Maximise(expression)));

        /// <summary>This objective with another after it, to be made small, which may give up so much of its optimum for the sake of those after it in turn.</summary>
        public ILexicographicObjective ThenMinimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            objective.Then(Objective.Minimise(expression, absoluteTolerance, relativeTolerance));

        /// <summary>This objective with another after it, to be made large, which may give up so much of its optimum for the sake of those after it in turn.</summary>
        public ILexicographicObjective ThenMaximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            objective.Then(Objective.Maximise(expression, absoluteTolerance, relativeTolerance));
    }

    extension(ISingleObjective objective) {
        /// <summary>The expression being optimised; constantly zero where there is no objective.</summary>
        public ILinearExpression Expression => objective is Optimisation optimisation ? optimisation.Expression : new Constant(0);

        /// <summary>Whether the expression is to be made small or large. (Where there is no objective, the constant zero is as small as it is large.)</summary>
        public ObjectiveSense Sense => objective is Optimisation optimisation ? optimisation.Sense : ObjectiveSense.Minimise;

        /// <summary>An objective of the same kind over another expression.</summary>
        public ISingleObjective With(ILinearExpression expression) => objective is Optimisation optimisation ? optimisation with { Expression = expression } : objective;
    }

    extension(Prioritised priority) {
        /// <summary>
        /// The constraint that this objective does no worse than <paramref name="value"/>, within its
        /// tolerances and a little more. The value is what a solver found, to within its own tolerances:
        /// SCIP or HiGHS will report 119.999999 where only 120 is attainable, and hold a later stage to
        /// that exactly and it is infeasible. So a hundred-thousandth is allowed (and a billionth of the
        /// value, for the rounding of large numbers), but never as much as half a unit, so that a
        /// whole-valued objective is still held to its value exactly.
        /// </summary>
        public IBooleanExpression NoWorseThan(double value) =>
            priority.Objective.Sense == ObjectiveSense.Minimise
                ? priority.Objective.Expression <= value + priority.Slack(value)
                : priority.Objective.Expression >= value - priority.Slack(value);

        private double Slack(double value) =>
            priority.AbsoluteTolerance + priority.RelativeTolerance * Math.Abs(value) + Math.Min(1e-5 + 1e-9 * Math.Abs(value), 0.5);
    }
}
