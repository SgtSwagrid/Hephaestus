namespace Hephaestus;

/// <summary>
/// Something a solution gives a <typeparamref name="TValue"/> for. The reading itself is
/// <c>solution.Value(expression)</c>; this is the type that says a reading exists.
/// </summary>
public interface IReadableExpression<out TValue>;

/// <summary>
/// Something a plain <typeparamref name="TValue"/> can stand for, in a model: <c>x + 5</c>,
/// <c>flag &amp; true</c>, <c>runtime &lt;= Duration.FromMinutes(5)</c>.
/// </summary>
public interface IWritableExpression<in TValue>;

/// <summary>
/// Both: a value of this type can be written into a model and read back out of a solution. The
/// cases are <see cref="ILinearExpression"/> (over numbers), <see cref="IBooleanExpression"/> (over
/// truths) and anything <see cref="ILinearlyEncodable{TValue}"/> read through a projection. A
/// reading that no constraint can mention is an <see cref="IReadableExpression{TValue}"/> and not this.
/// </summary>
public interface IExpression<TValue> : IReadableExpression<TValue>, IWritableExpression<TValue>;
