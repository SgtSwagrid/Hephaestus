namespace Hephaestus;

/// <summary>
/// An optimisation problem: a single constraint, optionally with an objective. There is no list of
/// constraints, because a list would only duplicate what <c>&amp;</c> already means; conjoin a
/// collection with <c>AllOf()</c>. The cases are <see cref="Satisfaction"/>,
/// <see cref="Minimisation"/> and <see cref="Maximisation"/>.
/// </summary>
public interface IProblem;

/// <summary>Find any assignment under which the constraint holds.</summary>
public sealed record Satisfaction(IBooleanExpression Constraint) : IProblem;

/// <summary>Find an assignment that makes the objective as small as possible while the constraint holds.</summary>
public sealed record Minimisation(
    ILinearExpression Objective,
    IBooleanExpression Constraint
) : IProblem;

/// <summary>Find an assignment that makes the objective as large as possible while the constraint holds.</summary>
public sealed record Maximisation(
    ILinearExpression Objective,
    IBooleanExpression Constraint
) : IProblem;

/// <summary>Factory functions that read like the mathematics: <c>Problem.Minimise(cost, subjectTo: constraint)</c>.</summary>
public static class Problem {
    /// <inheritdoc cref="Satisfaction"/>
    public static IProblem Satisfy(IBooleanExpression constraint) => new Satisfaction(constraint);

    /// <inheritdoc cref="Minimisation"/>
    public static IProblem Minimise(ILinearExpression objective, IBooleanExpression subjectTo) => new Minimisation(objective, subjectTo);

    /// <inheritdoc cref="Maximisation"/>
    public static IProblem Maximise(ILinearExpression objective, IBooleanExpression subjectTo) => new Maximisation(objective, subjectTo);

    /// <summary>Minimises a typed quantity; the unit of its projection does not affect the optimum.</summary>
    public static IProblem Minimise<T>(Quantity<T> objective, IBooleanExpression subjectTo) => new Minimisation(objective.Expression, subjectTo);

    /// <summary>Maximises a typed quantity; the unit of its projection does not affect the optimum.</summary>
    public static IProblem Maximise<T>(Quantity<T> objective, IBooleanExpression subjectTo) => new Maximisation(objective.Expression, subjectTo);

    /// <summary>Minimises a typed point, for example "as early as possible".</summary>
    public static IProblem Minimise<T, TDelta>(Point<T, TDelta> objective, IBooleanExpression subjectTo) => new Minimisation(objective.Expression, subjectTo);

    /// <summary>Maximises a typed point, for example "as late as possible".</summary>
    public static IProblem Maximise<T, TDelta>(Point<T, TDelta> objective, IBooleanExpression subjectTo) => new Maximisation(objective.Expression, subjectTo);
}
