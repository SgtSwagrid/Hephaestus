namespace Hephaestus;

public static partial class Piecewise {
    /// <summary>The larger of two quantities, under the projection of the first.</summary>
    public static Quantity<T> Max<T>(Quantity<T> left, Quantity<T> right) => left with { Expression = Max(left.Expression, right.In(left.Projection)) };

    /// <inheritdoc cref="Max{T}(Quantity{T}, Quantity{T})"/>
    public static Quantity<T> Max<T>(Quantity<T> left, T right) => left with { Expression = Max(left.Expression, left.Projection.Encode(right)) };

    /// <summary>The largest of several quantities, under the projection of the first.</summary>
    public static Quantity<T> Max<T>(IEnumerable<Quantity<T>> operands) => Fold([.. operands], Max);

    /// <summary>The smaller of two quantities, under the projection of the first.</summary>
    public static Quantity<T> Min<T>(Quantity<T> left, Quantity<T> right) => left with { Expression = Min(left.Expression, right.In(left.Projection)) };

    /// <inheritdoc cref="Min{T}(Quantity{T}, Quantity{T})"/>
    public static Quantity<T> Min<T>(Quantity<T> left, T right) => left with { Expression = Min(left.Expression, left.Projection.Encode(right)) };

    /// <summary>The smallest of several quantities, under the projection of the first.</summary>
    public static Quantity<T> Min<T>(IEnumerable<Quantity<T>> operands) => Fold([.. operands], Min);

    /// <summary>The size of a quantity, whichever its sign: an earliness or a lateness alike.</summary>
    public static Quantity<T> Abs<T>(Quantity<T> operand) => operand with { Expression = Abs(operand.Expression) };

    /// <summary>The later of two points, under the projection of the first.</summary>
    public static Point<T, TDelta> Max<T, TDelta>(Point<T, TDelta> left, Point<T, TDelta> right) => left with { Expression = Max(left.Expression, right.In(left.Projection)) };

    /// <inheritdoc cref="Max{T, TDelta}(Point{T, TDelta}, Point{T, TDelta})"/>
    public static Point<T, TDelta> Max<T, TDelta>(Point<T, TDelta> left, T right) => left with { Expression = Max(left.Expression, left.Projection.Encode(right)) };

    /// <summary>The latest of several points, under the projection of the first.</summary>
    public static Point<T, TDelta> Max<T, TDelta>(IEnumerable<Point<T, TDelta>> operands) => Fold([.. operands], Max);

    /// <summary>The earlier of two points, under the projection of the first.</summary>
    public static Point<T, TDelta> Min<T, TDelta>(Point<T, TDelta> left, Point<T, TDelta> right) => left with { Expression = Min(left.Expression, right.In(left.Projection)) };

    /// <inheritdoc cref="Min{T, TDelta}(Point{T, TDelta}, Point{T, TDelta})"/>
    public static Point<T, TDelta> Min<T, TDelta>(Point<T, TDelta> left, T right) => left with { Expression = Min(left.Expression, left.Projection.Encode(right)) };

    /// <summary>The earliest of several points, under the projection of the first.</summary>
    public static Point<T, TDelta> Min<T, TDelta>(IEnumerable<Point<T, TDelta>> operands) => Fold([.. operands], Min);
}
