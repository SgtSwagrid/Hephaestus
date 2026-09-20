using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// An optimisation problem: an objective, and the single constraint it is subject to. The
/// constraints of a model are conjoined into that one with <c>SubjectTo</c>, <c>&amp;</c> or
/// <c>AllOf()</c>, and can be had back as its <c>Conjuncts</c>. A problem is either an
/// <see cref="IOneShotProblem"/> or an <see cref="IMultipleObjectiveProblem"/>, according to
/// its objective; any solver solves either, and what concerns the constraint alone (explaining
/// infeasibility, say) takes either.
/// </summary>
public interface IProblem {
    /// <summary>What is to be optimised.</summary>
    IObjective Objective { get; }

    /// <summary>The constraint every solution must satisfy.</summary>
    IBooleanExpression Constraint { get; }
}

/// <summary>
/// A problem a solver takes in one go, as against a sequence: it has one objective or none. The
/// cases are <see cref="SatisfactionProblem"/> and <see cref="SingleObjectiveProblem"/>.
/// </summary>
public interface IOneShotProblem : IProblem {
    /// <inheritdoc cref="IProblem.Objective"/>
    new ISingleObjective Objective { get; }
}

/// <summary>A problem with several objectives, which comes down to a sequence of one-shot problems.</summary>
public interface IMultipleObjectiveProblem : IProblem {
    /// <inheritdoc cref="IProblem.Objective"/>
    new ILexicographicObjective Objective { get; }
}

/// <summary>The problem of satisfying a constraint, with nothing to optimise. Written <see cref="Problem.Satisfy"/>.</summary>
public sealed record SatisfactionProblem(
    IBooleanExpression Constraint
) : IOneShotProblem {
    /// <summary>Nothing is to be optimised.</summary>
    public NoObjective Objective => Hephaestus.Objective.None;

    ISingleObjective IOneShotProblem.Objective => Objective;

    IObjective IProblem.Objective => Objective;
}

/// <summary>A problem with an objective to pursue. A problem with none is a <see cref="SatisfactionProblem"/>.</summary>
public sealed record SingleObjectiveProblem(
    Optimisation Objective,
    IBooleanExpression Constraint
) : IOneShotProblem {
    ISingleObjective IOneShotProblem.Objective => Objective;

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
    public static SatisfactionProblem Satisfy(IBooleanExpression constraint) => new(constraint);

    /// <summary>The problem of pursuing <paramref name="objective"/>, as yet unconstrained.</summary>
    public static IOneShotProblem Optimise(ISingleObjective objective) =>
        objective switch {
            Optimisation optimisation => new SingleObjectiveProblem(optimisation, BooleanConstant.True),
            NoObjective => new SatisfactionProblem(BooleanConstant.True),
            _ => throw new NotSupportedException($"Unknown kind of objective: {objective.GetType().Name}."),
        };

    /// <summary>The problem of pursuing several objectives in order of priority, as yet unconstrained.</summary>
    public static IMultipleObjectiveProblem Optimise(ILexicographicObjective objective) => new MultipleObjectiveProblem(objective, BooleanConstant.True);

    /// <summary>The problem of making <paramref name="objective"/> as small as possible, as yet unconstrained.</summary>
    public static IOneShotProblem Minimise(ILinearExpression objective) => Optimise(Objective.Minimise(objective));

    /// <summary>The problem of making <paramref name="objective"/> as large as possible, as yet unconstrained.</summary>
    public static IOneShotProblem Maximise(ILinearExpression objective) => Optimise(Objective.Maximise(objective));

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
    public static IOneShotProblem Minimise<T>(Quantity<T> objective) => Minimise(objective.Expression);

    /// <summary>Maximises a typed quantity; the unit of its projection does not affect the optimum.</summary>
    public static IOneShotProblem Maximise<T>(Quantity<T> objective) => Maximise(objective.Expression);

    /// <summary>Minimises a typed point, for example "as early as possible".</summary>
    public static IOneShotProblem Minimise<T, TDelta>(Point<T, TDelta> objective) => Minimise(objective.Expression);

    /// <summary>Maximises a typed point, for example "as late as possible".</summary>
    public static IOneShotProblem Maximise<T, TDelta>(Point<T, TDelta> objective) => Maximise(objective.Expression);
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
                IOneShotProblem single => single.SubjectTo(constraint),
                IMultipleObjectiveProblem multiple => multiple.SubjectTo(constraint),
                _ => throw new NotSupportedException($"Unknown kind of problem: {problem.GetType().Name}."),
            };

        /// <summary>The same problem with several further constraints, all of which must hold: <c>SubjectTo(a, b, c)</c> is <c>SubjectTo(a &amp; b &amp; c)</c>.</summary>
        public IProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        // A problem is an objective and a constraint. As SubjectTo refines the one, these refine the other, and they are
        // defined by what they do to it: problem.ThenMinimise(x) is the problem of problem.Objective.ThenMinimise(x).

        /// <summary>The same problem with a further objective, which matters only among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem Then(Prioritised objective) => new MultipleObjectiveProblem(problem.Objective.Then(objective), problem.Constraint);

        /// <summary>The same problem with further objectives, to be made small, in order, among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem ThenMinimise(params IEnumerable<ILinearExpression> expressions) => new MultipleObjectiveProblem(problem.Objective.ThenMinimise(expressions), problem.Constraint);

        /// <summary>The same problem with further objectives, to be made large, in order, among the solutions that are best for those it has.</summary>
        public IMultipleObjectiveProblem ThenMaximise(params IEnumerable<ILinearExpression> expressions) => new MultipleObjectiveProblem(problem.Objective.ThenMaximise(expressions), problem.Constraint);

        /// <summary>The same problem with a further objective, to be made small, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMinimise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            new MultipleObjectiveProblem(problem.Objective.ThenMinimise(expression, absoluteTolerance, relativeTolerance), problem.Constraint);

        /// <summary>The same problem with a further objective, to be made large, which may give up so much of its optimum for the sake of those after it.</summary>
        public IMultipleObjectiveProblem ThenMaximise(ILinearExpression expression, double absoluteTolerance = 0, double relativeTolerance = 0) =>
            new MultipleObjectiveProblem(problem.Objective.ThenMaximise(expression, absoluteTolerance, relativeTolerance), problem.Constraint);
    }

    extension(IOneShotProblem problem) {
        /// <inheritdoc cref="SubjectTo(IProblem, IBooleanExpression)"/>
        public IOneShotProblem SubjectTo(IBooleanExpression constraint) => problem.With(problem.Objective.Expression, problem.Constraint.And(constraint));

        /// <inheritdoc cref="SubjectTo(IProblem, IEnumerable{IBooleanExpression})"/>
        public IOneShotProblem SubjectTo(params IEnumerable<IBooleanExpression> constraints) => problem.SubjectTo(constraints.AllOf());

        /// <summary>Whether the objective is to be made small or large.</summary>
        public ObjectiveSense Sense => problem.Objective.Sense;

        /// <summary>A problem of the same kind over another objective expression and constraint. A problem with nothing to optimise stays one.</summary>
        public IOneShotProblem With(ILinearExpression objective, IBooleanExpression constraint) =>
            problem.Objective is Optimisation optimisation
                ? new SingleObjectiveProblem(optimisation with { Expression = objective }, constraint)
                : new SatisfactionProblem(constraint);
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
