namespace Hephaestus;

/// <summary>
/// The arithmetic of positions: point - point is a quantity, and point &#177; quantity is a point.
/// There is deliberately no point + point and no scaling. Comparison and conversion are the same
/// as for an amount and live on <see cref="ILinearlyEncodable{TValue}"/>.
/// </summary>
public static class PointOperators {
    extension<T, TDelta>(Point<T, TDelta>) {
        public static Quantity<TDelta> operator -(Point<T, TDelta> left, Point<T, TDelta> right) => new(left.Expression - right.In(left.Projection), left.Projection.Delta);
        public static Quantity<TDelta> operator -(Point<T, TDelta> left, T right) => new(left.Expression - left.Projection.Encode(right), left.Projection.Delta);
        public static Quantity<TDelta> operator -(T left, Point<T, TDelta> right) => new(right.Projection.Encode(left) - right.Expression, right.Projection.Delta);

        public static Point<T, TDelta> operator +(Point<T, TDelta> point, Quantity<TDelta> shift) => point with { Expression = point.Expression + shift.In(point.Projection.Delta) };
        public static Point<T, TDelta> operator +(Point<T, TDelta> point, TDelta shift) => point with { Expression = point.Expression + point.Projection.Delta.Encode(shift) };
        public static Point<T, TDelta> operator +(Quantity<TDelta> shift, Point<T, TDelta> point) => point + shift;
        public static Point<T, TDelta> operator +(TDelta shift, Point<T, TDelta> point) => point + shift;

        public static Point<T, TDelta> operator -(Point<T, TDelta> point, Quantity<TDelta> shift) => point with { Expression = point.Expression - shift.In(point.Projection.Delta) };
        public static Point<T, TDelta> operator -(Point<T, TDelta> point, TDelta shift) => point with { Expression = point.Expression - point.Projection.Delta.Encode(shift) };
    }
}

/// <summary>
/// Building points out of plain values and quantities: <c>start + runtime</c>. These cannot be
/// generic operators, because no operand is a <see cref="Point{T, TDelta}"/> and generic code does
/// not know how a <c>TDelta</c> is added to a <c>T</c>; so each point type declares its own
/// <c>T + Quantity&lt;TDelta&gt;</c> operators beside its projection, in terms of these.
/// </summary>
public static class Anchoring {
    extension<TDelta>(Quantity<TDelta> shift) {
        /// <summary>The amount that the number one stands for under this quantity's projection.</summary>
        public TDelta Unit => shift.Projection.Decode(1);

        /// <summary>The point that lies this far beyond <paramref name="origin"/>, located under <paramref name="projection"/>.</summary>
        public Point<T, TDelta> Beyond<T>(T origin, IPointProjection<T, TDelta> projection) =>
            projection.Encode(origin) is var offset && offset == 0
                ? new Point<T, TDelta>(shift.In(projection.Delta), projection)
                : new Point<T, TDelta>(offset + shift.In(projection.Delta), projection);
    }
}
