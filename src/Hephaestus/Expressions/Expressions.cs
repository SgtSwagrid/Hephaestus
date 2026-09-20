namespace Hephaestus;

/// <summary>A linear expression read as something else, through one or both halves of a projection.</summary>
public interface IProjectedExpression {
    /// <summary>The underlying linear expression, counted in the projection's own unit.</summary>
    ILinearExpression Expression { get; }
}

/// <summary>
/// Something a solution gives a <typeparamref name="TValue"/> for. Where that is all it is, no
/// constraint can mention it: <c>Select</c> gives one of these.
/// </summary>
public interface IReadableExpression<out TValue> : IProjectedExpression {
    /// <summary>How the underlying number is read as a <typeparamref name="TValue"/>.</summary>
    IDecoder<TValue, double> Decoder { get; }
}

/// <summary>
/// Something a plain <typeparamref name="TValue"/> can stand for, in a model. Where that is all it
/// is, no solution can be asked for one: <c>Preselect</c> gives one of these.
/// </summary>
public interface IWritableExpression<in TValue> : IProjectedExpression {
    /// <summary>How a <typeparamref name="TValue"/> is written as a number.</summary>
    IEncoder<TValue, double> Encoder { get; }
}
