namespace Hephaestus;

/// <summary>
/// The piecewise-linear functions: <c>Max</c>, <c>Min</c> and <c>Abs</c>. Like the operators, they
/// only build data. When a problem is encoded, each is replaced by an auxiliary variable tied to its
/// operands, with a binary variable only where the problem could otherwise cheat: minimising
/// <c>Max(a, b)</c> or bounding <c>Abs(x)</c> from above costs none. With
/// <c>using static Hephaestus.Piecewise;</c> they read as the mathematics: <c>Abs(x - target) &lt;= 5</c>.
/// </summary>
public static partial class Piecewise {
    /// <summary>The larger of two expressions.</summary>
    public static ILinearExpression Max(ILinearExpression left, ILinearExpression right) => new Maximum(left, right);

    /// <inheritdoc cref="Max(ILinearExpression, ILinearExpression)"/>
    public static ILinearExpression Max(ILinearExpression left, double right) => new Maximum(left, new Constant(right));

    /// <inheritdoc cref="Max(ILinearExpression, ILinearExpression)"/>
    public static ILinearExpression Max(double left, ILinearExpression right) => new Maximum(new Constant(left), right);

    /// <summary>The largest of several expressions, built as a balanced tree.</summary>
    /// <exception cref="InvalidOperationException">The sequence is empty, and so has no largest member.</exception>
    public static ILinearExpression Max(IEnumerable<ILinearExpression> operands) => Fold([.. operands], Max);

    /// <summary>The smaller of two expressions.</summary>
    public static ILinearExpression Min(ILinearExpression left, ILinearExpression right) => new Minimum(left, right);

    /// <inheritdoc cref="Min(ILinearExpression, ILinearExpression)"/>
    public static ILinearExpression Min(ILinearExpression left, double right) => new Minimum(left, new Constant(right));

    /// <inheritdoc cref="Min(ILinearExpression, ILinearExpression)"/>
    public static ILinearExpression Min(double left, ILinearExpression right) => new Minimum(new Constant(left), right);

    /// <summary>The smallest of several expressions, built as a balanced tree.</summary>
    /// <exception cref="InvalidOperationException">The sequence is empty, and so has no smallest member.</exception>
    public static ILinearExpression Min(IEnumerable<ILinearExpression> operands) => Fold([.. operands], Min);

    /// <summary>The distance of an expression from zero.</summary>
    public static ILinearExpression Abs(ILinearExpression operand) => new AbsoluteValue(operand);

    private static T Fold<T>(IReadOnlyList<T> operands, Func<T, T, T> combine) =>
        operands.Count == 0
            ? throw new InvalidOperationException("An empty sequence has neither a largest nor a smallest member.")
            : Balanced.Fold(operands, operands[0], combine);
}
