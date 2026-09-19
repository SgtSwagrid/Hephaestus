using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// An optimisation problem: a single constraint, and whatever is to be optimised subject to it. The
/// constraints of a model are conjoined into that one with <c>SubjectTo</c>, <c>&amp;</c> or
/// <c>AllOf()</c>, and can be had back as its <c>Conjuncts</c>. A problem is either an
/// <see cref="ISingleObjectiveProblem"/> or an <see cref="IMultipleObjectiveProblem"/>; any solver
/// solves either, and what concerns the constraint alone (explaining infeasibility, say) takes either.
/// </summary>
public interface IProblem {
    /// <summary>The constraint every solution must satisfy.</summary>
    IBooleanExpression Constraint { get; }
}

/// <summary>
/// A problem with one objective or none, which is what a solver solves in one go. The cases are
/// <see cref="Satisfaction"/>, <see cref="Minimisation"/> and <see cref="Maximisation"/>.
/// </summary>
public interface ISingleObjectiveProblem : IProblem;

/// <summary>
/// A problem with several objectives, which comes down to a sequence of single-objective problems.
/// The one case so far is <see cref="LexicographicProblem"/>, in which the objectives are in order of priority.
/// </summary>
public interface IMultipleObjectiveProblem : IProblem {
    /// <summary>The objectives, most important first.</summary>
    ImmutableArray<Objective> Objectives { get; }
}

/// <summary>Find any assignment under which the constraint holds.</summary>
public sealed record Satisfaction(IBooleanExpression Constraint) : ISingleObjectiveProblem;

/// <summary>Find an assignment that makes the objective as small as possible while the constraint holds.</summary>
public sealed record Minimisation(
    ILinearExpression Objective,
    IBooleanExpression Constraint
) : ISingleObjectiveProblem;

/// <summary>Find an assignment that makes the objective as large as possible while the constraint holds.</summary>
public sealed record Maximisation(
    ILinearExpression Objective,
    IBooleanExpression Constraint
) : ISingleObjectiveProblem;

/// <summary>
/// Where problems start: <c>Problem.Minimise(cost).SubjectTo(constraint)</c>. A problem is a value,
/// and each step of building one gives another problem; nothing is modified.
/// </summary>
public static class Problem {
    /// <inheritdoc cref="Satisfaction"/>
    public static ISingleObjectiveProblem Satisfy(IBooleanExpression constraint) => new Satisfaction(constraint);

    /// <summary>The problem of making <paramref name="objective"/> as small as possible, as yet unconstrained.</summary>
    public static ISingleObjectiveProblem Minimise(ILinearExpression objective) => new Minimisation(objective, BooleanConstant.True);

    /// <summary>The problem of making <paramref name="objective"/> as large as possible, as yet unconstrained.</summary>
    public static ISingleObjectiveProblem Maximise(ILinearExpression objective) => new Maximisation(objective, BooleanConstant.True);

    /// <summary>
    /// The problem of making several objectives as small as possible, in order of priority:
    /// <c>Minimise(a, b, c)</c> is <c>Minimise(a).ThenMinimise(b).ThenMinimise(c)</c>.
    /// </summary>
    public static IMultipleObjectiveProblem Minimise(params IEnumerable<ILinearExpression> objectives) => Lexicographic(objectives.Select(objective => Objective.Minimise(objective)));

    /// <summary>
    /// The problem of making several objectives as large as possible, in order of priority:
    /// <c>Maximise(a, b, c)</c> is <c>Maximise(a).ThenMaximise(b).ThenMaximise(c)</c>.
    /// </summary>
    public static IMultipleObjectiveProblem Maximise(params IEnumerable<ILinearExpression> objectives) => Lexicographic(objectives.Select(objective => Objective.Maximise(objective)));

    /// <summary>The problem of meeting the given objectives in order of priority, as yet unconstrained.</summary>
    public static IMultipleObjectiveProblem Lexicographic(IEnumerable<Objective> objectives) => new LexicographicProblem([.. objectives], BooleanConstant.True);

    /// <summary>Minimises a typed quantity; the unit of its projection does not affect the optimum.</summary>
    public static ISingleObjectiveProblem Minimise<T>(Quantity<T> objective) => Minimise(objective.Expression);

    /// <summary>Maximises a typed quantity; the unit of its projection does not affect the optimum.</summary>
    public static ISingleObjectiveProblem Maximise<T>(Quantity<T> objective) => Maximise(objective.Expression);

    /// <summary>Minimises a typed point, for example "as early as possible".</summary>
    public static ISingleObjectiveProblem Minimise<T, TDelta>(Point<T, TDelta> objective) => Minimise(objective.Expression);

    /// <summary>Maximises a typed point, for example "as late as possible".</summary>
    public static ISingleObjectiveProblem Maximise<T, TDelta>(Point<T, TDelta> objective) => Maximise(objective.Expression);
}

/// <summary>The steps by which a problem is built up. Each gives a new problem; <c>SubjectTo</c> and the objectives may come in any order.</summary>
public static class ProblemBuilding {
    extension(IProblem problem) {
        /// <summary>
        /// The same problem with a further constraint: <c>problem.SubjectTo(a).SubjectTo(b)</c> is
        /// subject to <c>a &amp; b</c>. (The first takes the place of the trivial constraint that a
        /// problem starts with, which nobody wrote and nobody wants to read.)
        /// </summary>
        public IProblem SubjectTo(IBooleanExpression constraint) =>
            problem switch {
                ISingleObjectiveProblem single => single.SubjectTo(constraint),
                IMultipleObjectiveProblem multiple => multiple.SubjectTo(constraint),
                _ => throw new NotSupportedException($"Unknown kind of problem: {problem.GetType().Name}."),
            };

        /// <summary>The same problem with several further constraints, all of which must hold: <c>SubjectTo(a, b, c)</c> is <c>SubjectTo(a &amp; b &amp; c)</c>.</summary>
        public IProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        /// <summary>The objectives of the problem, most important first: none, one or several.</summary>
        public ImmutableArray<Objective> Objectives =>
            problem switch {
                Satisfaction => [],
                ISingleObjectiveProblem single => [new Objective(single.Sense, single.Objective)],
                IMultipleObjectiveProblem multiple => multiple.Objectives,
                _ => throw new NotSupportedException($"Unknown kind of problem: {problem.GetType().Name}."),
            };
    }

    extension(ISingleObjectiveProblem problem) {
        /// <inheritdoc cref="SubjectTo(IProblem, IBooleanExpression)"/>
        public ISingleObjectiveProblem SubjectTo(IBooleanExpression constraint) => problem.With(problem.Objective, problem.Constraint.And(constraint));

        /// <inheritdoc cref="SubjectTo(IProblem, IEnumerable{IBooleanExpression})"/>
        public ISingleObjectiveProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        /// <summary>The same problem with a further objective, which matters only among the solutions that are best for this one.</summary>
        public IMultipleObjectiveProblem Then(Objective objective) => new LexicographicProblem(problem.Objectives.Add(objective), problem.Constraint);

        /// <summary>The same problem with further objectives, to be made small, in order, among the solutions that are best for this one.</summary>
        public IMultipleObjectiveProblem ThenMinimise(params IEnumerable<ILinearExpression> expressions) => new LexicographicProblem(problem.Objectives, problem.Constraint).ThenMinimise(expressions);

        /// <summary>The same problem with further objectives, to be made large, in order, among the solutions that are best for this one.</summary>
        public IMultipleObjectiveProblem ThenMaximise(params IEnumerable<ILinearExpression> expressions) => new LexicographicProblem(problem.Objectives, problem.Constraint).ThenMaximise(expressions);

        /// <summary>The same problem with a further objective, to be made small, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMinimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => problem.Then(Objective.Minimise(expression, absoluteTolerance, relativeTolerance));

        /// <summary>The same problem with a further objective, to be made large, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMaximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => problem.Then(Objective.Maximise(expression, absoluteTolerance, relativeTolerance));
    }

    extension(IMultipleObjectiveProblem problem) {
        /// <inheritdoc cref="SubjectTo(IProblem, IBooleanExpression)"/>
        public IMultipleObjectiveProblem SubjectTo(IBooleanExpression constraint) => new LexicographicProblem(problem.Objectives, problem.Constraint.And(constraint));

        /// <inheritdoc cref="SubjectTo(IProblem, IEnumerable{IBooleanExpression})"/>
        public IMultipleObjectiveProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        /// <inheritdoc cref="Then(ISingleObjectiveProblem, Objective)"/>
        public IMultipleObjectiveProblem Then(Objective objective) => new LexicographicProblem(problem.Objectives.Add(objective), problem.Constraint);

        /// <inheritdoc cref="ThenMinimise(ISingleObjectiveProblem, IEnumerable{ILinearExpression})"/>
        public IMultipleObjectiveProblem ThenMinimise(params IEnumerable<ILinearExpression> expressions) => expressions.Aggregate(problem, (chained, expression) => chained.Then(Objective.Minimise(expression)));

        /// <inheritdoc cref="ThenMaximise(ISingleObjectiveProblem, IEnumerable{ILinearExpression})"/>
        public IMultipleObjectiveProblem ThenMaximise(params IEnumerable<ILinearExpression> expressions) => expressions.Aggregate(problem, (chained, expression) => chained.Then(Objective.Maximise(expression)));

        /// <inheritdoc cref="ThenMinimise(ISingleObjectiveProblem, ILinearExpression, double, double)"/>
        public IMultipleObjectiveProblem ThenMinimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => problem.Then(Objective.Minimise(expression, absoluteTolerance, relativeTolerance));

        /// <inheritdoc cref="ThenMaximise(ISingleObjectiveProblem, ILinearExpression, double, double)"/>
        public IMultipleObjectiveProblem ThenMaximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) => problem.Then(Objective.Maximise(expression, absoluteTolerance, relativeTolerance));
    }

    extension(IBooleanExpression constraint) {
        /// <summary>The conjunction with <paramref name="further"/>, or whichever of the two is not the constant <c>true</c>, which says nothing.</summary>
        internal IBooleanExpression And(IBooleanExpression further) =>
            constraint is BooleanConstant { Value: true } ? further
            : further is BooleanConstant { Value: true } ? constraint
            : constraint & further;
    }
}
