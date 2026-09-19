namespace Hephaestus;

/// <summary>
/// The algebra of amounts. Every operator reduces to the corresponding operator on the underlying
/// linear expressions, after bringing the right-hand side into the left-hand side's projection.
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

        public static IBooleanExpression operator <=(Quantity<T> left, Quantity<T> right) => left.Expression <= right.In(left.Projection);
        public static IBooleanExpression operator <=(Quantity<T> left, T right) => left.Expression <= left.Projection.Encode(right);
        public static IBooleanExpression operator <=(T left, Quantity<T> right) => right.Projection.Encode(left) <= right.Expression;

        public static IBooleanExpression operator >=(Quantity<T> left, Quantity<T> right) => left.Expression >= right.In(left.Projection);
        public static IBooleanExpression operator >=(Quantity<T> left, T right) => left.Expression >= left.Projection.Encode(right);
        public static IBooleanExpression operator >=(T left, Quantity<T> right) => right.Projection.Encode(left) >= right.Expression;

        public static IBooleanExpression operator <(Quantity<T> left, Quantity<T> right) => left.Expression < right.In(left.Projection);
        public static IBooleanExpression operator <(Quantity<T> left, T right) => left.Expression < left.Projection.Encode(right);
        public static IBooleanExpression operator <(T left, Quantity<T> right) => right.Projection.Encode(left) < right.Expression;

        public static IBooleanExpression operator >(Quantity<T> left, Quantity<T> right) => left.Expression > right.In(left.Projection);
        public static IBooleanExpression operator >(Quantity<T> left, T right) => left.Expression > left.Projection.Encode(right);
        public static IBooleanExpression operator >(T left, Quantity<T> right) => right.Projection.Encode(left) > right.Expression;
    }

    extension<T>(Quantity<T> quantity) {
        /// <summary>The constraint that this quantity equals <paramref name="other"/>.</summary>
        public IBooleanExpression EqualTo(Quantity<T> other) => quantity.Expression.EqualTo(other.In(quantity.Projection));

        /// <inheritdoc cref="EqualTo{T}(Quantity{T}, Quantity{T})"/>
        public IBooleanExpression EqualTo(T other) => quantity.Expression.EqualTo(quantity.Projection.Encode(other));

        /// <summary>The constraint that this quantity differs from <paramref name="other"/>.</summary>
        public IBooleanExpression NotEqualTo(Quantity<T> other) => quantity.Expression.NotEqualTo(other.In(quantity.Projection));

        /// <inheritdoc cref="NotEqualTo{T}(Quantity{T}, Quantity{T})"/>
        public IBooleanExpression NotEqualTo(T other) => quantity.Expression.NotEqualTo(quantity.Projection.Encode(other));

        /// <summary>The constraint <c>lower &lt;= quantity &lt;= upper</c>.</summary>
        public IBooleanExpression Between(T lower, T upper) => lower <= quantity & quantity <= upper;

        /// <inheritdoc cref="Between{T}(Quantity{T}, T, T)"/>
        public IBooleanExpression Between(Quantity<T> lower, Quantity<T> upper) => lower <= quantity & quantity <= upper;

        /// <inheritdoc cref="Between{T}(Quantity{T}, T, T)"/>
        public IBooleanExpression Between(T lower, Quantity<T> upper) => lower <= quantity & quantity <= upper;

        /// <inheritdoc cref="Between{T}(Quantity{T}, T, T)"/>
        public IBooleanExpression Between(Quantity<T> lower, T upper) => lower <= quantity & quantity <= upper;

        /// <summary>
        /// How many of <paramref name="unit"/> this quantity amounts to, as a plain linear expression:
        /// <c>delay.In(Duration.FromMinutes(1))</c> is the delay in minutes, ready for a cost function.
        /// </summary>
        public ILinearExpression In(T unit) => quantity.Expression / quantity.Projection.Encode(unit);

        /// <summary>
        /// The plain linear expression that measures this quantity under another projection, for
        /// example a duration in minutes for use in a cost function.
        /// </summary>
        public ILinearExpression In(IProjection<T> projection) => Projecting.Convert(quantity.Expression, quantity.Projection, projection);
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
