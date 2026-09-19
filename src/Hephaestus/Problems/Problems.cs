using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// An optimisation problem: an objective, and the single constraint it is subject to. The
/// constraints of a model are conjoined into that one with <c>SubjectTo</c>, <c>&amp;</c> or
/// <c>AllOf()</c>, and can be had back as its <c>Conjuncts</c>. A problem is either an
/// <see cref="ISingleObjectiveProblem"/> or an <see cref="IMultipleObjectiveProblem"/>, according to
/// its objective; any solver solves either, and what concerns the constraint alone (explaining
/// infeasibility, say) takes either.
/// </summary>
public interface IProblem {
    /// <summary>What is to be optimised.</summary>
    IObjective Objective { get; }

    /// <summary>The constraint every solution must satisfy.</summary>
    IBooleanExpression Constraint { get; }
}

/// <summary>A problem with one objective or none, which is what a solver solves in one go.</summary>
public interface ISingleObjectiveProblem : IProblem {
    /// <inheritdoc cref="IProblem.Objective"/>
    new ISingleObjective Objective { get; }
}

/// <summary>A problem with several objectives, which comes down to a sequence of single-objective problems.</summary>
public interface IMultipleObjectiveProblem : IProblem {
    /// <inheritdoc cref="IProblem.Objective"/>
    new ILexicographicObjective Objective { get; }
}

/// <inheritdoc cref="ISingleObjectiveProblem"/>
public sealed record SingleObjectiveProblem(
    ISingleObjective Objective,
    IBooleanExpression Constraint
) : ISingleObjectiveProblem {
    IObjective IProblem.Objective => Objective;
}

/// <inheritdoc cref="IMultipleObjectiveProblem"/>
public sealed record MultipleObjectiveProblem(
    ILexicographicObjective Objective,
    IBooleanExpression Constraint
) : IMultipleObjectiveProblem {
    IObjective IProblem.Objective => Objective;
}

/// <summary>
/// Where problems start: <c>Problem.Minimise(cost).SubjectTo(constraint)</c>. A problem is a value,
/// and each step of building one gives another problem; nothing is modified.
/// </summary>
public static class Problem {
    /// <summary>The problem of finding any assignment under which the constraint holds.</summary>
    public static ISingleObjectiveProblem Satisfy(IBooleanExpression constraint) => new SingleObjectiveProblem(new NoObjective(), constraint);

    /// <summary>The problem of pursuing <paramref name="objective"/>, as yet unconstrained.</summary>
    public static ISingleObjectiveProblem Optimise(ISingleObjective objective) => new SingleObjectiveProblem(objective, BooleanConstant.True);

    /// <summary>The problem of pursuing several objectives in order of priority, as yet unconstrained.</summary>
    public static IMultipleObjectiveProblem Optimise(ILexicographicObjective objective) => new MultipleObjectiveProblem(objective, BooleanConstant.True);

    /// <summary>The problem of making <paramref name="objective"/> as small as possible, as yet unconstrained.</summary>
    public static ISingleObjectiveProblem Minimise(ILinearExpression objective) => Optimise(Objective.Minimise(objective));

    /// <summary>The problem of making <paramref name="objective"/> as large as possible, as yet unconstrained.</summary>
    public static ISingleObjectiveProblem Maximise(ILinearExpression objective) => Optimise(Objective.Maximise(objective));

    /// <summary>
    /// The problem of making several objectives as small as possible, in order of priority:
    /// <c>Minimise(a, b, c)</c> is <c>Minimise(a).ThenMinimise(b).ThenMinimise(c)</c>.
    /// </summary>
    public static IMultipleObjectiveProblem Minimise(params IEnumerable<ILinearExpression> objectives) => Lexicographic(objectives.Select(objective => new Prioritised(Objective.Minimise(objective))));

    /// <summary>
    /// The problem of making several objectives as large as possible, in order of priority:
    /// <c>Maximise(a, b, c)</c> is <c>Maximise(a).ThenMaximise(b).ThenMaximise(c)</c>.
    /// </summary>
    public static IMultipleObjectiveProblem Maximise(params IEnumerable<ILinearExpression> objectives) => Lexicographic(objectives.Select(objective => new Prioritised(Objective.Maximise(objective))));

    /// <summary>The problem of meeting the given objectives in order of priority, as yet unconstrained.</summary>
    public static IMultipleObjectiveProblem Lexicographic(IEnumerable<Prioritised> objectives) => Optimise(Objective.InOrder(objectives));

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

        /// <summary>The same problem with a further objective, which matters only among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem Then(Prioritised objective) => new MultipleObjectiveProblem(problem.Objective.Then(objective), problem.Constraint);

        /// <summary>The same problem with further objectives, to be made small, in order, among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem ThenMinimise(params IEnumerable<ILinearExpression> expressions) =>
            new MultipleObjectiveProblem(expressions.Aggregate(Objective.InOrder(problem.Objective.Priorities), (objective, expression) => objective.Then(Objective.Minimise(expression))), problem.Constraint);

        /// <summary>The same problem with further objectives, to be made large, in order, among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem ThenMaximise(params IEnumerable<ILinearExpression> expressions) =>
            new MultipleObjectiveProblem(expressions.Aggregate(Objective.InOrder(problem.Objective.Priorities), (objective, expression) => objective.Then(Objective.Maximise(expression))), problem.Constraint);

        /// <summary>The same problem with a further objective, to be made small, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMinimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            problem.Then(new Prioritised(Objective.Minimise(expression), absoluteTolerance, relativeTolerance));

        /// <summary>The same problem with a further objective, to be made large, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMaximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            problem.Then(new Prioritised(Objective.Maximise(expression), absoluteTolerance, relativeTolerance));
    }

    extension(ISingleObjectiveProblem problem) {
        /// <inheritdoc cref="SubjectTo(IProblem, IBooleanExpression)"/>
        public ISingleObjectiveProblem SubjectTo(IBooleanExpression constraint) => new SingleObjectiveProblem(problem.Objective, problem.Constraint.And(constraint));

        /// <inheritdoc cref="SubjectTo(IProblem, IEnumerable{IBooleanExpression})"/>
        public ISingleObjectiveProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        /// <summary>Whether the objective is to be made small or large.</summary>
        public ObjectiveSense Sense => problem.Objective.Sense;

        /// <summary>A problem of the same kind over another objective expression and constraint.</summary>
        public ISingleObjectiveProblem With(ILinearExpression objective, IBooleanExpression constraint) => new SingleObjectiveProblem(problem.Objective.With(objective), constraint);
    }

    extension(IMultipleObjectiveProblem problem) {
        /// <inheritdoc cref="SubjectTo(IProblem, IBooleanExpression)"/>
        public IMultipleObjectiveProblem SubjectTo(IBooleanExpression constraint) => new MultipleObjectiveProblem(problem.Objective, problem.Constraint.And(constraint));

        /// <inheritdoc cref="SubjectTo(IProblem, IEnumerable{IBooleanExpression})"/>
        public IMultipleObjectiveProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());
    }

    extension(IBooleanExpression constraint) {
        /// <summary>The conjunction with <paramref name="further"/>, or whichever of the two is not the constant <c>true</c>, which says nothing.</summary>
        internal IBooleanExpression And(IBooleanExpression further) =>
            constraint is BooleanConstant { Value: true } ? further
            : further is BooleanConstant { Value: true } ? constraint
            : constraint & further;
    }
}
