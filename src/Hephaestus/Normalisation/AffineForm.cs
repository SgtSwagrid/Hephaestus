using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// The normal form of a linear expression: <c>constant + &#931; coefficient &#183; variable</c>, with
/// variables in a fixed order and no zero coefficients. Two linear expressions denote the same
/// function exactly when their affine forms are equal, so <c>x + y</c> and <c>y + x</c> normalise
/// to equal values even though the expressions themselves differ.
/// </summary>
public sealed record AffineForm(
    ImmutableSortedDictionary<IVariable, double> Coefficients,
    double Constant
) {
    /// <summary>The affine form of the constant zero.</summary>
    public static AffineForm Zero { get; } = new(ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer), 0);

    /// <inheritdoc/>
    public bool Equals(AffineForm? other) =>
        other is not null
        && Constant.Equals(other.Constant)
        && Coefficients.Count == other.Coefficients.Count
        && Coefficients.SequenceEqual(other.Coefficients);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        Coefficients.Aggregate(Constant.GetHashCode(), (hash, term) => HashCode.Combine(hash, term.Key, term.Value));

    /// <summary>The form in mathematical notation, since the default rendering of a dictionary says nothing.</summary>
    public override string ToString() => this.Format();
}

/// <summary>A closed interval of real numbers; either end may be infinite.</summary>
public readonly record struct Interval(
    double Lower,
    double Upper
) {
    /// <summary>The whole real line.</summary>
    public static Interval Unbounded { get; } = new(double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>The interval from zero to one.</summary>
    public static Interval Unit { get; } = new(0, 1);
}

/// <summary>The fixed order in which variables appear in normal forms: by name, then by kind.</summary>
public static class VariableOrder {
    /// <summary>Compares variables by ordinal name, then by kind.</summary>
    public static IComparer<IVariable> Comparer { get; } = Comparer<IVariable>.Create((left, right) =>
        string.CompareOrdinal(left.Name, right.Name) is var byName and not 0
            ? byName
            : string.CompareOrdinal(left.GetType().Name, right.GetType().Name));
}
