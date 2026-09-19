namespace Hephaestus;

/// <summary>
/// A real-valued expression that is linear (strictly: affine) in its variables, or piecewise so.
/// The cases are <see cref="Constant"/>, <see cref="Sum"/>, <see cref="Product"/>, the
/// <see cref="IVariable"/> records, and the piecewise-linear <see cref="Maximum"/>,
/// <see cref="Minimum"/> and <see cref="AbsoluteValue"/>, which the encoder lowers to linear form. Expressions are plain data, kept exactly as written;
/// all interpretation (normalisation, bounds, encoding, evaluation) happens in later passes.
/// </summary>
public interface ILinearExpression;

/// <summary>A fixed real number.</summary>
public sealed record Constant(double Value) : ILinearExpression;

/// <summary>The sum of two linear expressions.</summary>
public sealed record Sum(
    ILinearExpression Left,
    ILinearExpression Right
) : ILinearExpression;

/// <summary>
/// A linear expression scaled by a fixed coefficient. There is deliberately no product of two
/// expressions: non-linear terms are unrepresentable.
/// </summary>
public sealed record Product(
    double Coefficient,
    ILinearExpression Expression
) : ILinearExpression;

/// <summary>The larger of two expressions. Build it with <see cref="Piecewise.Max(ILinearExpression, ILinearExpression)"/>.</summary>
public sealed record Maximum(
    ILinearExpression Left,
    ILinearExpression Right
) : ILinearExpression;

/// <summary>The smaller of two expressions. Build it with <see cref="Piecewise.Min(ILinearExpression, ILinearExpression)"/>.</summary>
public sealed record Minimum(
    ILinearExpression Left,
    ILinearExpression Right
) : ILinearExpression;

/// <summary>The distance of an expression from zero. Build it with <see cref="Piecewise.Abs(ILinearExpression)"/>.</summary>
public sealed record AbsoluteValue(ILinearExpression Operand) : ILinearExpression;
