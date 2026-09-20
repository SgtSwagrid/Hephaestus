namespace Hephaestus;

/// <summary>
/// The arithmetic of amounts: what may be done to a quantity and not to a position. Comparison and
/// conversion are the same for both and live on <see cref="ILinearlyEncodable{TValue}"/>.
/// </summary>
public static class QuantityOperators {
    extension<T>(Quantity<T>) {
        public static Quantity<T> operator +(Quantity<T> left, Quantity<T> right) => left with { Expression = left.Expression + right.In(left.Projection) };
        public static Quantity<T> operator +(Quantity<T> left, T right) => left with { Expression = left.Expression + left.Projection.Encode(right) };
        public static Quantity<T> operator +(T left, Quantity<T> right) => right with { Expression = right.Projection.Encode(left) + right.Expression };

        public static Quantity<T> operator -(Quantity<T> operand) => operand with { Expression = -operand.Expression };
        public static Quantity<T> operator -(Quantity<T> left, Quantity<T> right) => left with { Expression = left.Expression - right.In(left.Projection) };
        public static Quantity<T> operator -(Quantity<T> left, T right) => left with { Expression = left.Expression - left.Projection.Encode(right) };
        public static Quantity<T> operator -(T left, Quantity<T> right) => right with { Expression = right.Projection.Encode(left) - right.Expression };

        public static Quantity<T> operator *(double factor, Quantity<T> quantity) => quantity with { Expression = factor * quantity.Expression };
        public static Quantity<T> operator *(Quantity<T> quantity, double factor) => quantity with { Expression = factor * quantity.Expression };
        public static Quantity<T> operator /(Quantity<T> quantity, double divisor) => quantity with { Expression = quantity.Expression / divisor };
    }

    extension<T>(Quantity<T> quantity) {
        /// <summary>
        /// How many of <paramref name="unit"/> this quantity amounts to, as a plain linear expression:
        /// <c>delay.In(Duration.FromMinutes(1))</c> is the delay in minutes, ready for a cost function.
        /// </summary>
        public ILinearExpression In(T unit) => quantity.Expression / quantity.Projection.Encode(unit);

    }

    extension<T>(IEnumerable<Quantity<T>> quantities) {
        /// <summary>The sum of the quantities, under the projection of the first.</summary>
        /// <exception cref="InvalidOperationException">The sequence is empty, so there is no projection to sum under.</exception>
        public Quantity<T> Sum() =>
            quantities.ToList() is { Count: > 0 } items
                ? items[0] with { Expression = items.Select(item => item.In(items[0].Projection)).Sum() }
                : throw new InvalidOperationException("Cannot sum an empty sequence of quantities: there is no projection to express the zero in.");
    }
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
