using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A formula lowered for a solver: in negation normal form, over affine relations. Negations are
/// pushed down to the leaves, every relation compares an affine form with zero, nested junctions of
/// the same kind are flattened, and no piecewise-linear function is left. It is what
/// <c>problem.Linearise()</c> gives for each constraint, and all that a backend is ever handed. The
/// cases are <see cref="Literal"/>, <see cref="AffineRelation"/>, <see cref="All"/> and <see cref="Any"/>.
/// </summary>
public interface INormalForm;

/// <summary>A binary variable, or its negation.</summary>
public sealed record Literal(
    BinaryVariable Variable,
    bool IsPositive
) : INormalForm;

/// <summary>
/// <c>Difference</c> related to zero, exactly as written: <c>lhs ~ rhs</c> is <c>lhs - rhs ~ 0</c>,
/// strictness and disequality are kept for the solver or the encoding to deal with, and a relation
/// under negation is its opposite rather than being turned round. Which way it faces matters: a
/// shadow price is the rate of change as the right-hand side as written is raised.
/// </summary>
public sealed record AffineRelation(
    AffineForm Difference,
    Relation Relation
) : INormalForm;

/// <summary>Holds when every operand holds; with no operands it is the constant true.</summary>
public sealed record All(ImmutableList<INormalForm> Operands) : INormalForm {
    /// <inheritdoc/>
    public bool Equals(All? other) => other is not null && Operands.SequenceEqual(other.Operands);

    /// <inheritdoc/>
    public override int GetHashCode() => Operands.Aggregate(typeof(All).GetHashCode(), HashCode.Combine);
}

/// <summary>Holds when some operand holds; with no operands it is the constant false.</summary>
public sealed record Any(ImmutableList<INormalForm> Operands) : INormalForm {
    /// <inheritdoc/>
    public bool Equals(Any? other) => other is not null && Operands.SequenceEqual(other.Operands);

    /// <inheritdoc/>
    public override int GetHashCode() => Operands.Aggregate(typeof(Any).GetHashCode(), HashCode.Combine);
}

/// <summary>Functions over normal forms.</summary>
public static class NormalForms {
    extension(INormalForm formula) {
        /// <summary>Every variable mentioned in the formula, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => Collect(formula, ImmutableSortedSet.Create(VariableOrder.Comparer));

        /// <summary>The formula in the notation it would have been written in: <c>(x - y &lt;= 3) | !a</c>.</summary>
        public string Format() => Formatted(formula, isOperand: false);
    }

    private static ImmutableSortedSet<IVariable> Collect(INormalForm formula, ImmutableSortedSet<IVariable> found) =>
        formula switch {
            Literal literal => found.Add(literal.Variable),
            AffineRelation relation => found.Union(relation.Difference.Coefficients.Keys),
            All all => all.Operands.Aggregate(found, (sofar, operand) => Collect(operand, sofar)),
            Any any => any.Operands.Aggregate(found, (sofar, operand) => Collect(operand, sofar)),
            _ => throw new NotSupportedException($"Unknown kind of normal form: {formula.GetType().Name}."),
        };

    private static string Formatted(INormalForm formula, bool isOperand) =>
        formula switch {
            Literal literal => (literal.IsPositive ? "" : "!") + literal.Variable.Name,
            AffineRelation relation => Bracketed(relation.Format(), isOperand),
            All { Operands.IsEmpty: true } => "true",
            Any { Operands.IsEmpty: true } => "false",
            All all => Bracketed(string.Join(" & ", all.Operands.Select(operand => Formatted(operand, isOperand: true))), isOperand && all.Operands.Count > 1),
            Any any => Bracketed(string.Join(" | ", any.Operands.Select(operand => Formatted(operand, isOperand: true))), isOperand && any.Operands.Count > 1),
            _ => throw new NotSupportedException($"Unknown kind of normal form: {formula.GetType().Name}."),
        };

    private static string Bracketed(string text, bool isBracketed) => isBracketed ? $"({text})" : text;

    /// <summary>
    /// The formula with every relation facing one way, <c>&lt;=</c>, <c>&lt;</c>, <c>==</c> or
    /// <c>!=</c>, and every equation and disequation with a positive leading coefficient: equal for
    /// formulas that mean the same, so that the encoding can let them share an auxiliary.
    /// </summary>
    internal static INormalForm Canonical(INormalForm formula) =>
        formula switch {
            AffineRelation { Relation: Relation.GreaterThanOrEqual } relation => new AffineRelation(relation.Difference.Negated, Relation.LessThanOrEqual),
            AffineRelation { Relation: Relation.GreaterThan } relation => new AffineRelation(relation.Difference.Negated, Relation.LessThan),
            AffineRelation { Relation: Relation.Equal or Relation.NotEqual } relation when relation.Difference.Coefficients.First().Value < 0 => relation with { Difference = relation.Difference.Negated },
            All all => new All([.. all.Operands.Select(Canonical)]),
            Any any => new Any([.. any.Operands.Select(Canonical)]),
            _ => formula,
        };
}
