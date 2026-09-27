namespace Hephaestus;

/// <summary>How the two sides of a relation relate.</summary>
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

/// <summary>
/// Linear arithmetic: <see cref="ILogic"/> with relations between linear expressions for atoms.
/// <c>x &lt;= y</c>, <c>(x &lt;= y) &amp; flag</c> and every typed comparison are formulas of it,
/// and so is the constraint of every problem.
/// </summary>
public interface ILinearArithmetic : ILogic;

/// <summary>The atom of linear arithmetic: a relation between two linear expressions, <c>Left &lt;= Right</c> say.</summary>
public sealed record LinearRelation(
    ILinearExpression Left,
    Relation Relation,
    ILinearExpression Right
) : IBooleanExpression<ILinearArithmetic>;
