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
/// The atoms of linear arithmetic, as written: relations between linear expressions. A formula over
/// them is an <c>IBooleanExpression&lt;ILinearRelation&gt;</c>, which is what <c>x &lt;= y</c>,
/// <c>(x &lt;= y) &amp; flag</c> and every typed comparison are. The one case is <see cref="LinearRelation"/>.
/// </summary>
public interface ILinearRelation : IAtom;

/// <summary>A relation between two linear expressions: <c>Left &lt;= Right</c>, say.</summary>
public sealed record LinearRelation(
    ILinearExpression Left,
    Relation Relation,
    ILinearExpression Right
) : ILinearRelation, IBooleanExpression<ILinearRelation>;
