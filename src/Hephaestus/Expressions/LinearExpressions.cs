namespace Hephaestus;

/// <summary>
/// A real-valued expression that is linear (strictly: affine) in its variables, or piecewise so.
/// The cases are <see cref="Constant"/>, <see cref="Sum"/>, <see cref="Product"/>, the
/// <see cref="INumericVariable"/> records, <see cref="Indicator"/>, the piecewise-linear
/// <see cref="Maximum"/>, <see cref="Minimum"/>, <see cref="AbsoluteValue"/> and
/// <see cref="Conditional"/>, which the encoder lowers to linear form, and <see cref="NamedTerm"/>. Expressions are plain data, kept exactly as written;
/// all interpretation (normalisation, bounds, encoding, evaluation) happens in later passes.
/// </summary>
public interface ILinearExpression : IReadableExpression<double> {
    double IReadableExpression<double>.Read(Solution solution) => Evaluation.Evaluate(solution, this);
}

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

/// <summary>
/// One expression or another, according to whether a condition holds. Build it with
/// <see cref="Piecewise.If(IBooleanExpression{ILinearRelation}, ILinearExpression, ILinearExpression)"/>, or as the product
/// of an <see cref="Indicator"/> and an expression, which is the expression if the condition holds and zero if not.
/// </summary>
public sealed record Conditional(
    IBooleanExpression<ILinearRelation> Condition,
    ILinearExpression Then,
    ILinearExpression Otherwise
) : ILinearExpression;

/// <summary>
/// One when a condition holds, and zero when it does not: the number a truth stands for. Write it
/// <c>condition.Indicator</c>. The indicator of a binary variable, or of its negation, is that
/// variable's column; any other condition is lowered as <c>If(condition, 1, 0)</c>. Multiplying an
/// expression by one is the one product of two expressions that stays linear:
/// <c>needsSetup.Indicator * setupTime</c> is <c>If(needsSetup, setupTime, 0)</c>.
/// </summary>
public sealed record Indicator(IBooleanExpression<ILinearRelation> Condition) : ILinearExpression;
