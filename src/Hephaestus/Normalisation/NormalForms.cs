using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Negation normal form: negations pushed down to the leaves, comparisons reduced to
/// <c>form &lt;= 0</c> or <c>form == 0</c>, and nested junctions of the same kind flattened.
/// The cases are <see cref="Literal"/>, <see cref="Atom"/>, <see cref="All"/> and <see cref="Any"/>.
/// </summary>
internal interface INormalForm;

/// <summary>A binary variable, or its negation.</summary>
public sealed record Literal(
    BinaryVariable Variable,
    bool IsPositive
) : INormalForm;

/// <summary><c>Expression &lt;= 0</c>, or <c>Expression == 0</c> when <see cref="IsEquality"/>.</summary>
internal sealed record Atom(
    AffineForm Expression,
    bool IsEquality
) : INormalForm;

/// <summary>Holds when every operand holds; with no operands it is the constant true.</summary>
internal sealed record All(ImmutableList<INormalForm> Operands) : INormalForm {
    public bool Equals(All? other) => other is not null && Operands.SequenceEqual(other.Operands);

    public override int GetHashCode() => Operands.Aggregate(typeof(All).GetHashCode(), HashCode.Combine);
}

/// <summary>Holds when some operand holds; with no operands it is the constant false.</summary>
internal sealed record Any(ImmutableList<INormalForm> Operands) : INormalForm {
    public bool Equals(Any? other) => other is not null && Operands.SequenceEqual(other.Operands);

    public override int GetHashCode() => Operands.Aggregate(typeof(Any).GetHashCode(), HashCode.Combine);
}
