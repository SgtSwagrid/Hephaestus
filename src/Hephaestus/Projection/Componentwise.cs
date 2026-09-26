using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// What typed expressions do entry by entry, and where the two kinds of component are told apart
/// (reading them apart is <see cref="Evaluation"/>'s): numbers are compared as numbers, and truths
/// by implication, false being less than true. A relation between two values holds when it holds
/// between every pair of their entries, except that two values differ when any pair does.
/// </summary>
internal static class Componentwise {
    /// <summary>The constraint that <paramref name="left"/> and <paramref name="right"/> stand in <paramref name="relation"/>, entry by entry.</summary>
    /// <exception cref="ArgumentException">The two have different numbers of entries, or entries of different kinds.</exception>
    public static IBooleanExpression Relate(ImmutableArray<IComponent> left, Relation relation, ImmutableArray<IComponent> right) =>
        Combined(relation, Paired(left, right).Select(pair => Relate(pair.First, relation, pair.Second)));

    /// <summary>Components that stand for the entries of <paramref name="raw"/>, of the kinds of <paramref name="kinds"/>: a plain value brought into a model.</summary>
    public static ImmutableArray<IComponent> Constants(ImmutableArray<IComponent> kinds, ImmutableArray<double> raw) =>
        [.. Paired(kinds, raw).Select(pair => Fixed(pair.First, pair.Second))];

    /// <summary>
    /// Re-expresses <paramref name="components"/>, read under <paramref name="from"/>, as the
    /// components of the kinds of <paramref name="like"/> that stand for the same value under
    /// <paramref name="to"/>. Both being affine, each entry under the one is an affine function of
    /// the entries under the other, pinned down by where the origin and each unit vector go; a truth
    /// can only be kept or negated.
    /// </summary>
    /// <exception cref="ArgumentException">The projections do not correspond entry by entry.</exception>
    public static ImmutableArray<IComponent> Convert<TValue>(
        ImmutableArray<IComponent> components,
        IProjection<TValue, ImmutableArray<double>> from,
        IProjection<TValue, ImmutableArray<double>> to,
        ImmutableArray<IComponent> like
    ) =>
        from.Equals(to) ? components : Converted(components, Probed(from, to, components.Length), like);

    /// <summary>The entries of two vectors, paired.</summary>
    /// <exception cref="ArgumentException">The vectors have different numbers of entries.</exception>
    public static IEnumerable<(TFirst First, TSecond Second)> Paired<TFirst, TSecond>(ImmutableArray<TFirst> first, ImmutableArray<TSecond> second) =>
        first.Length == second.Length
            ? first.Zip(second)
            : throw new ArgumentException($"Values of {first.Length} and {second.Length} entries cannot be taken entry by entry.");

    private static IBooleanExpression Combined(Relation relation, IEnumerable<IBooleanExpression> relations) =>
        relation == Relation.NotEqual ? relations.AnyOf() : relations.AllOf();

    private static IBooleanExpression Relate(IComponent left, Relation relation, IComponent right) =>
        (left, right) switch {
            (LinearComponent linear, LinearComponent other) => new Comparison(linear.Expression, relation, other.Expression),
            (LogicalComponent logical, LogicalComponent other) => Logically(logical.Expression, relation, other.Expression),
            _ => throw new ArgumentException($"A {Kind(left)} cannot be compared with a {Kind(right)}."),
        };

    private static IBooleanExpression Logically(IBooleanExpression left, Relation relation, IBooleanExpression right) =>
        relation switch {
            Relation.LessThan => !left & right,
            Relation.LessThanOrEqual => left.Implies(right),
            Relation.Equal => left.Iff(right),
            Relation.NotEqual => left ^ right,
            Relation.GreaterThanOrEqual => right.Implies(left),
            Relation.GreaterThan => left & !right,
            _ => throw new NotSupportedException($"Unknown relation: {relation}."),
        };

    private static IComponent Fixed(IComponent kind, double entry) =>
        kind switch {
            LinearComponent => new LinearComponent(new Constant(entry)),
            LogicalComponent => new LogicalComponent(new BooleanConstant(entry > 0.5)),
            _ => throw new NotSupportedException($"Unknown kind of component: {kind.GetType().Name}."),
        };

    private static string Kind(IComponent component) =>
        component switch {
            LinearComponent => "number",
            LogicalComponent => "truth",
            _ => throw new NotSupportedException($"Unknown kind of component: {component.GetType().Name}."),
        };

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

    private static ImmutableArray<IComponent> Converted(ImmutableArray<IComponent> components, AffineMap map, ImmutableArray<IComponent> like) =>
        [.. Paired(like, map.Origin).Select((pair, entry) => Entry(pair.First, Terms(components, map, entry), pair.Second))];

    private static ImmutableArray<(IComponent Component, double Coefficient)> Terms(ImmutableArray<IComponent> components, AffineMap map, int entry) =>
        [.. components.Select((component, index) => (component, map.Steps[index][entry])).Where(term => term.Item2 != 0)];

    private static IComponent Entry(IComponent kind, ImmutableArray<(IComponent Component, double Coefficient)> terms, double offset) =>
        kind switch {
            LinearComponent => new LinearComponent(Projecting.Affine(terms.Select(Scaled).Sum(), scale: 1, offset)),
            LogicalComponent => new LogicalComponent(Kept(terms, offset)),
            _ => throw new NotSupportedException($"Unknown kind of component: {kind.GetType().Name}."),
        };

    private static ILinearExpression Scaled((IComponent Component, double Coefficient) term) =>
        term.Component is LinearComponent linear
            ? Projecting.Affine(linear.Expression, term.Coefficient, offset: 0)
            : throw new ArgumentException($"A number cannot be written in terms of a {Kind(term.Component)}.");

    private static IBooleanExpression Kept(ImmutableArray<(IComponent Component, double Coefficient)> terms, double offset) =>
        (terms, offset) switch {
            ([], 0) => BooleanConstant.False,
            ([], 1) => BooleanConstant.True,
            ([(LogicalComponent logical, 1)], 0) => logical.Expression,
            ([(LogicalComponent logical, -1)], 1) => !logical.Expression,
            _ => throw new ArgumentException("A truth can only be kept or negated between projections, not written in terms of other entries."),
        };
}
