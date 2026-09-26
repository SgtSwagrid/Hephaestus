using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// How values of a domain type lie along the solver's number line: for example durations measured
/// in seconds, or date-times measured in seconds since some origin. A projection must be affine
/// and increasing (equal steps in <typeparamref name="T"/> are equal steps in number, and later
/// means larger). That is what lets expressions under different projections of the same type be
/// combined: the conversion between any two is recovered from where they send zero and one.
/// Implement projections as records, so that equal projections compare equal.
/// </summary>
public interface IProjection<T> : IProjection<T, double>;

/// <summary>Reads a raw form from the solver as a <typeparamref name="TValue"/>. One half of a projection.</summary>
public interface IDecoder<out TValue, in TRaw> {
    /// <summary>The value that <paramref name="representation"/> stands for.</summary>
    TValue Decode(TRaw representation);
}

/// <summary>Writes a <typeparamref name="TValue"/> as a raw form the solver understands. The other half.</summary>
public interface IEncoder<in TValue, out TRaw> {
    /// <summary>The raw form that stands for <paramref name="value"/>.</summary>
    TRaw Encode(TValue value);
}

/// <summary>
/// Both halves: what lets a value be written into a model and read back out of a solution. The raw
/// form is a number for anything that becomes a column, and could be a truth for anything that
/// becomes a binary.
/// </summary>
public interface IProjection<TValue, TRaw> : IDecoder<TValue, TRaw>, IEncoder<TValue, TRaw>;

/// <summary>
/// The projection of a type whose values are positions rather than amounts (date-times, not
/// durations). Positions cannot be added or scaled, but the difference between two of them is an
/// amount, of type <typeparamref name="TDelta"/>.
/// </summary>
public interface IPointProjection<T, TDelta> : IProjection<T> {
    /// <summary>The projection of differences, in the same unit as this projection.</summary>
    IProjection<TDelta> Delta { get; }
}

/// <summary>
/// Components read and written as a value of type <typeparamref name="TValue"/>, through a
/// projection onto their raw form: one number for each component, a truth counting as <c>1</c> or
/// <c>0</c>. It is what every typed expression is, however many components it has and of whatever
/// kinds: a <see cref="Quantity{T}"/> is one number, a two-state type one truth, and <c>Zip</c>
/// puts any two side by side. Comparison, reading and <c>With</c> are written once against it,
/// entry by entry.
/// <para>
/// A projection onto several entries must be affine and increasing in each of them, as one onto a
/// single number is, and must read the entry of a truth as a truth. Those that <c>Zip</c> and
/// <c>Sequence</c> build out of lawful projections are.
/// </para>
/// </summary>
public interface IEncodable<TValue> : IDecodedExpression<TValue>, IWritableExpression<TValue> {
    /// <summary>How the raw form and a <typeparamref name="TValue"/> stand for each other.</summary>
    IProjection<TValue, ImmutableArray<double>> Projection { get; }

    IDecoder<TValue, ImmutableArray<double>> IDecodedExpression<TValue>.Decoder => Projection;

    IEncoder<TValue, ImmutableArray<double>> IWritableExpression<TValue>.Encoder => Projection;
}

/// <summary>
/// A linear expression read as a value of type <typeparamref name="TValue"/>, through a projection:
/// an <see cref="IEncodable{TValue}"/> of one number. It is what a <see cref="Quantity{T}"/> and a
/// <see cref="Point{T, TDelta}"/> have in common, and adds to it what only one number can have:
/// measuring in another unit (<c>In</c>), optimising, and the piecewise functions. Only the
/// arithmetic differs between the two, and that stays with each.
/// <para>
/// Implement it to have a type of your own treated alike; supply the expression and the projection,
/// and give it whatever algebra suits.
/// </para>
/// </summary>
public interface ILinearlyEncodable<TValue> : IEncodable<TValue> {
    /// <summary>The underlying linear expression, counted in the projection's own unit.</summary>
    ILinearExpression Expression { get; }

    /// <summary>How the underlying number and a <typeparamref name="TValue"/> stand for each other.</summary>
    new IProjection<TValue> Projection { get; }

    ImmutableArray<IComponent> IProjectedExpression.Components => [new LinearComponent(Expression)];

    IProjection<TValue, ImmutableArray<double>> IEncodable<TValue>.Projection => new SingleNumber<TValue>(Projection);
}

/// <summary>
/// A boolean expression read as a value of type <typeparamref name="TValue"/>, through a projection
/// onto truth rather than onto the number line: an <see cref="IEncodable{TValue}"/> of one truth,
/// carrying a two-state type on a single binary.
/// </summary>
public interface ILogicallyEncodable<TValue> : IEncodable<TValue> {
    /// <summary>The underlying boolean expression.</summary>
    IBooleanExpression Expression { get; }

    /// <summary>How its truth is read as a <typeparamref name="TValue"/>.</summary>
    new IProjection<TValue, bool> Projection { get; }

    ImmutableArray<IComponent> IProjectedExpression.Components => [new LogicalComponent(Expression)];

    IProjection<TValue, ImmutableArray<double>> IEncodable<TValue>.Projection => new SingleTruth<TValue>(Projection);
}

/// <summary>
/// A linear expression read as an amount of type <typeparamref name="T"/>: a duration, a length,
/// a cost. Quantities can be added, subtracted, scaled and compared, with each other and with
/// plain <typeparamref name="T"/> values.
/// </summary>
public sealed record Quantity<T>(
    ILinearExpression Expression,
    IProjection<T> Projection
) : ILinearlyEncodable<T>;

/// <summary>
/// A linear expression read as a position of type <typeparamref name="T"/>: a date-time, a
/// position. Points can be compared and shifted by quantities of <typeparamref name="TDelta"/>, and
/// the difference of two points is such a quantity; adding or scaling points does not type-check.
/// </summary>
public sealed record Point<T, TDelta>(
    ILinearExpression Expression,
    IPointProjection<T, TDelta> Projection
) : ILinearlyEncodable<T> {
    IProjection<T> ILinearlyEncodable<T>.Projection => Projection;
}

/// <summary>Conversion between projections of the same type.</summary>
internal static class Projecting {
    /// <summary>
    /// Re-expresses <paramref name="expression"/>, a number under <paramref name="from"/>, as the
    /// number that stands for the same value under <paramref name="to"/>. Both being affine, the
    /// conversion is <c>scale &#183; expression + offset</c>, pinned down by the images of zero and one.
    /// </summary>
    public static ILinearExpression Convert<T>(ILinearExpression expression, IProjection<T> from, IProjection<T> to) =>
        from.Equals(to)
            ? expression
            : Affine(expression, scale: to.Encode(from.Decode(1)) - to.Encode(from.Decode(0)), offset: to.Encode(from.Decode(0)));

    internal static ILinearExpression Affine(ILinearExpression expression, double scale, double offset) =>
        (scale == 1, offset == 0) switch {
            (true, true) => expression,
            (true, false) => expression + offset,
            (false, true) => scale * expression,
            (false, false) => scale * expression + offset,
        };
}

/// <summary>A projection onto one number, seen as one onto a raw form of one entry.</summary>
internal sealed record SingleNumber<TValue>(IProjection<TValue> Projection) : IProjection<TValue, ImmutableArray<double>> {
    /// <inheritdoc/>
    public TValue Decode(ImmutableArray<double> representation) => Projection.Decode(representation.Single());

    /// <inheritdoc/>
    public ImmutableArray<double> Encode(TValue value) => [Projection.Encode(value)];
}

/// <summary>A projection onto one truth, seen as one onto a raw form of one entry: <c>1</c> for a truth that holds, <c>0</c> for one that does not.</summary>
internal sealed record SingleTruth<TValue>(IProjection<TValue, bool> Projection) : IProjection<TValue, ImmutableArray<double>> {
    /// <inheritdoc/>
    public TValue Decode(ImmutableArray<double> representation) => Projection.Decode(representation.Single() > 0.5);

    /// <inheritdoc/>
    public ImmutableArray<double> Encode(TValue value) => [Projection.Encode(value) ? 1 : 0];
}

/// <summary>Two projections side by side: the first reads the leading <see cref="Split"/> entries of the raw form, and the second the rest.</summary>
internal sealed record PairedProjection<TFirst, TSecond>(
    IProjection<TFirst, ImmutableArray<double>> First,
    IProjection<TSecond, ImmutableArray<double>> Second,
    int Split
) : IProjection<(TFirst, TSecond), ImmutableArray<double>> {
    /// <inheritdoc/>
    public (TFirst, TSecond) Decode(ImmutableArray<double> representation) => (First.Decode(representation[..Split]), Second.Decode(representation[Split..]));

    /// <inheritdoc/>
    public ImmutableArray<double> Encode((TFirst, TSecond) value) => [.. First.Encode(value.Item1), .. Second.Encode(value.Item2)];
}

/// <summary>One of the projections in a <see cref="SequencedProjection{TValue}"/>, reading <see cref="Dimension"/> entries from <see cref="Start"/>.</summary>
internal sealed record SequencedPart<TValue>(
    IProjection<TValue, ImmutableArray<double>> Projection,
    int Start,
    int Dimension
);

/// <summary>Projections end to end, read and written as an array of their values, one for each.</summary>
internal sealed record SequencedProjection<TValue>(ImmutableArray<SequencedPart<TValue>> Parts) : IProjection<ImmutableArray<TValue>, ImmutableArray<double>> {
    /// <inheritdoc/>
    public ImmutableArray<TValue> Decode(ImmutableArray<double> representation) =>
        [.. Parts.Select(part => part.Projection.Decode(representation.Slice(part.Start, part.Dimension)))];

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The array does not have one value for each projection.</exception>
    public ImmutableArray<double> Encode(ImmutableArray<TValue> value) =>
        value.Length == Parts.Length
            ? [.. Parts.Zip(value, (part, item) => part.Projection.Encode(item)).SelectMany(raw => raw)]
            : throw new ArgumentException($"An array of {value.Length} values cannot be written where {Parts.Length} are expected.", nameof(value));

    /// <inheritdoc/>
    public bool Equals(SequencedProjection<TValue>? other) => other is not null && Parts.SequenceEqual(other.Parts);

    /// <inheritdoc/>
    public override int GetHashCode() => Parts.Aggregate(0, HashCode.Combine);
}
