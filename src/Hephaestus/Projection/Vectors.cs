using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A fixed number of things of one kind, combined entry by entry: quantities while a model is
/// built, and their values once it is solved. <c>Zip</c> pairs the entries of two vectors and
/// <c>Select</c> maps each, so <c>u.Zip(v).Select((a, b) =&gt; a + b)</c> is the vector of sums;
/// the arithmetic and comparison of vectors of quantities are written that way, and a vector of
/// anything readable is read as <c>u.Select(solution.Value)</c>.
/// </summary>
public sealed record Vector<TElement>(ImmutableArray<TElement> Elements) {
    /// <inheritdoc/>
    public bool Equals(Vector<TElement>? other) => other is not null && Elements.SequenceEqual(other.Elements);

    /// <inheritdoc/>
    public override int GetHashCode() => Elements.Aggregate(0, (hash, element) => HashCode.Combine(hash, element));

    /// <inheritdoc/>
    public override string ToString() => $"[{string.Join(", ", Elements)}]";
}

/// <summary>Building vectors.</summary>
public static class Vector {
    /// <summary>The vector of <paramref name="elements"/>, in order: <c>Vector.Of(x, y, z)</c>.</summary>
    public static Vector<TElement> Of<TElement>(params ImmutableArray<TElement> elements) => new(elements);
}
