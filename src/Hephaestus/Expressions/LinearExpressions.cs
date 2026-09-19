namespace Hephaestus;

/// <summary>
/// A real-valued expression that is linear (strictly: affine) in its variables.
/// The cases are <see cref="Constant"/>, <see cref="Sum"/>, <see cref="Product"/> and the
/// <see cref="IVariable"/> records. Expressions are plain data, kept exactly as written;
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
