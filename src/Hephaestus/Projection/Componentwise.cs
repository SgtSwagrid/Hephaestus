using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// What typed expressions do entry by entry. Every entry is a number, a truth counting through its
/// indicator, so a relation between two values holds when it holds between every pair of their
/// entries, except that two values differ when any pair does. Between truths that is logic:
/// <c>[a] &lt;= [b]</c> is <c>a =&gt; b</c>, and <c>[a] == [b]</c> is <c>a &lt;=&gt; b</c>.
/// </summary>
internal static class Componentwise {
    /// <summary>The constraint that <paramref name="left"/> and <paramref name="right"/> stand in <paramref name="relation"/>, entry by entry.</summary>
    /// <exception cref="ArgumentException">The two have different numbers of entries.</exception>
    public static IBooleanExpression<ILinearArithmetic> Relate(ImmutableArray<ILinearExpression> left, Relation relation, ImmutableArray<ILinearExpression> right) =>
        Combined(relation, Paired(left, right).Select(pair => new LinearRelation(pair.First, relation, pair.Second)));

    /// <summary>Components that stand for the entries of <paramref name="raw"/>: a plain value brought into a model.</summary>
    public static ImmutableArray<ILinearExpression> Constants(ImmutableArray<double> raw) =>
        [.. raw.Select(entry => new Constant(entry))];

    /// <summary>
    /// Re-expresses <paramref name="components"/>, read under <paramref name="from"/>, as the
    /// components that stand for the same value under <paramref name="to"/>. Both being affine, each
    /// entry under the one is an affine function of the entries under the other, pinned down by where
    /// the origin and each unit vector go.
    /// </summary>
    public static ImmutableArray<ILinearExpression> Convert<TValue>(
        ImmutableArray<ILinearExpression> components,
        IProjection<TValue, ImmutableArray<double>> from,
        IProjection<TValue, ImmutableArray<double>> to
    ) =>
        from.Equals(to) ? components : Converted(components, Probed(from, to, components.Length));

    /// <summary>The entries of two vectors, paired.</summary>
    /// <exception cref="ArgumentException">The vectors have different numbers of entries.</exception>
    public static IEnumerable<(TFirst First, TSecond Second)> Paired<TFirst, TSecond>(ImmutableArray<TFirst> first, ImmutableArray<TSecond> second) =>
        first.Length == second.Length
            ? first.Zip(second)
            : throw new ArgumentException($"Values of {first.Length} and {second.Length} entries cannot be taken entry by entry.");

    private static IBooleanExpression<ILinearArithmetic> Combined(Relation relation, IEnumerable<IBooleanExpression<ILinearArithmetic>> relations) =>
        relation == Relation.NotEqual ? relations.AnyOf() : relations.AllOf();

    /// <summary>
    /// An affine map on raw forms, as where it sends the origin and how far each entry of its
    /// output moves per unit of each entry of its input (<see cref="Steps"/>, one per input entry).
    /// </summary>
    private sealed record AffineMap(
        ImmutableArray<double> Origin,
        ImmutableArray<ImmutableArray<double>> Steps
    );

    private static AffineMap Probed<TValue>(IProjection<TValue, ImmutableArray<double>> from, IProjection<TValue, ImmutableArray<double>> to, int dimension) =>
        Probed(Image(from, to, Unit(dimension, index: null)), index => Image(from, to, Unit(dimension, index)), dimension);

    private static AffineMap Probed(ImmutableArray<double> origin, Func<int, ImmutableArray<double>> image, int dimension) =>
        new(origin, [.. Enumerable.Range(0, dimension).Select(index => Difference(image(index), origin))]);

    private static ImmutableArray<double> Image<TValue>(IProjection<TValue, ImmutableArray<double>> from, IProjection<TValue, ImmutableArray<double>> to, ImmutableArray<double> raw) =>
        to.Encode(from.Decode(raw));

    private static ImmutableArray<double> Unit(int dimension, int? index) =>
        [.. Enumerable.Range(0, dimension).Select(entry => entry == index ? 1d : 0d)];

    private static ImmutableArray<double> Difference(ImmutableArray<double> left, ImmutableArray<double> right) =>
        [.. Paired(left, right).Select(pair => pair.First - pair.Second)];

    private static ImmutableArray<ILinearExpression> Converted(ImmutableArray<ILinearExpression> components, AffineMap map) =>
        [.. map.Origin.Select((offset, entry) => Projecting.Affine(Terms(components, map, entry).Sum(), scale: 1, offset))];

    private static IEnumerable<ILinearExpression> Terms(ImmutableArray<ILinearExpression> components, AffineMap map, int entry) =>
        components
            .Select((component, index) => (Component: component, Coefficient: map.Steps[index][entry]))
            .Where(term => term.Coefficient != 0)
            .Select(term => Projecting.Affine(term.Component, term.Coefficient, offset: 0));
}
