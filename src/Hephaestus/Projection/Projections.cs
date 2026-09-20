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
/// A linear expression read as a value of type <typeparamref name="TValue"/>, through a projection.
/// It is what a <see cref="Quantity{T}"/> and a <see cref="Point{T, TDelta}"/> have in common:
/// comparison, reading off a solution, naming and optimising are the same for both and are written
/// once against this. Only the arithmetic differs, and that stays with each.
/// <para>
/// Implement it to have a type of your own treated alike; supply the expression and the projection,
/// and give it whatever algebra suits.
/// </para>
/// </summary>
public interface ILinearlyEncodable<TValue> : IReadableExpression<TValue>, IWritableExpression<TValue> {
    /// <summary>How the underlying number and a <typeparamref name="TValue"/> stand for each other.</summary>
    IProjection<TValue> Projection { get; }

    IDecoder<TValue, double> IReadableExpression<TValue>.Decoder => Projection;

    IEncoder<TValue, double> IWritableExpression<TValue>.Encoder => Projection;
}

/// <summary>
/// A boolean expression read as a value of type <typeparamref name="TValue"/>, through a projection
/// onto truth rather than onto the number line: a two-state type over a single binary, where
/// <see cref="ILinearlyEncodable{TValue}"/> is a type over a column.
/// </summary>
public interface ILogicallyEncodable<TValue> {
    /// <summary>The underlying boolean expression.</summary>
    IBooleanExpression Expression { get; }

    /// <summary>How its truth is read as a <typeparamref name="TValue"/>.</summary>
    IProjection<TValue, bool> Projection { get; }
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

    private static ILinearExpression Affine(ILinearExpression expression, double scale, double offset) =>
        (scale == 1, offset == 0) switch {
            (true, true) => expression,
            (true, false) => expression + offset,
            (false, true) => scale * expression,
            (false, false) => scale * expression + offset,
        };
}
