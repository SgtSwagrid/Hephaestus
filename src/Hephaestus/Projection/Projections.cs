namespace Hephaestus;

/// <summary>
/// How values of a domain type lie along the solver's number line: for example durations measured
/// in seconds, or date-times measured in seconds since some origin. A projection must be affine
/// and increasing (equal steps in <typeparamref name="T"/> are equal steps in number, and later
/// means larger). That is what lets expressions under different projections of the same type be
/// combined: the conversion between any two is recovered from where they send zero and one.
/// Implement projections as records, so that equal projections compare equal.
/// </summary>
public interface IProjection<T> {
    /// <summary>The number that stands for <paramref name="value"/>.</summary>
    double Encode(T value);

    /// <summary>The value that <paramref name="number"/> stands for.</summary>
    T Decode(double number);
}

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
/// A linear expression read as an amount of type <typeparamref name="T"/>: a duration, a length,
/// a cost. Quantities can be added, subtracted, scaled and compared, with each other and with
/// plain <typeparamref name="T"/> values.
/// </summary>
public sealed record Quantity<T>(
    ILinearExpression Expression,
    IProjection<T> Projection
);

/// <summary>
/// A linear expression read as a position of type <typeparamref name="T"/>: a date-time, a
/// chainage. Points can be compared and shifted by quantities of <typeparamref name="TDelta"/>, and
/// the difference of two points is such a quantity; adding or scaling points does not type-check.
/// </summary>
public sealed record Point<T, TDelta>(
    ILinearExpression Expression,
    IPointProjection<T, TDelta> Projection
);
