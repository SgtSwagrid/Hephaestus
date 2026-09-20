namespace Hephaestus;

/// <summary>
/// A truth-valued expression over linear comparisons and binary variables. The cases are
/// <see cref="BooleanConstant"/>, <see cref="Comparison"/>, <see cref="Negation"/>,
/// <see cref="Conjunction"/>, <see cref="Disjunction"/>, <see cref="Implication"/>,
/// <see cref="Equivalence"/>, <see cref="NamedConstraint"/> and <see cref="BinaryVariable"/>. Like linear expressions, boolean
/// expressions are plain data kept exactly as written.
/// </summary>
public interface IBooleanExpression : IReadableExpression<bool> {
    bool IReadableExpression<bool>.Read(Solution solution) => Evaluation.Holds(solution, this, Evaluation.Tolerance);
}

/// <summary>A fixed truth value.</summary>
public sealed record BooleanConstant(bool Value) : IBooleanExpression {
    /// <summary>The expression that always holds.</summary>
    public static BooleanConstant True { get; } = new(true);

    /// <summary>The expression that never holds.</summary>
    public static BooleanConstant False { get; } = new(false);
}

/// <summary>How the two sides of a <see cref="Comparison"/> relate.</summary>
public enum Relation {
    /// <summary>Left &lt; right.</summary>
    LessThan,

    /// <summary>Left &#8804; right.</summary>
    LessThanOrEqual,

    /// <summary>Left = right.</summary>
    Equal,

    /// <summary>Left &#8800; right.</summary>
    NotEqual,

    /// <summary>Left &#8805; right.</summary>
    GreaterThanOrEqual,

    /// <summary>Left &gt; right.</summary>
    GreaterThan,
}

/// <summary>A comparison between two linear expressions.</summary>
public sealed record Comparison(
    ILinearExpression Left,
    Relation Relation,
    ILinearExpression Right
) : IBooleanExpression;

/// <summary>Holds exactly when the operand does not.</summary>
public sealed record Negation(IBooleanExpression Operand) : IBooleanExpression;

/// <summary>Holds exactly when both sides hold.</summary>
public sealed record Conjunction(
    IBooleanExpression Left,
    IBooleanExpression Right
) : IBooleanExpression;

/// <summary>Holds exactly when at least one side holds.</summary>
public sealed record Disjunction(
    IBooleanExpression Left,
    IBooleanExpression Right
) : IBooleanExpression;

/// <summary>Holds unless the antecedent holds and the consequent does not.</summary>
public sealed record Implication(
    IBooleanExpression Antecedent,
    IBooleanExpression Consequent
) : IBooleanExpression;

/// <summary>Holds exactly when both sides have the same truth value.</summary>
public sealed record Equivalence(
    IBooleanExpression Left,
    IBooleanExpression Right
) : IBooleanExpression;
