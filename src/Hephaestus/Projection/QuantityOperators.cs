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
