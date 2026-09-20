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
public interface IReadableExpression<out TValue> {
    /// <summary>
    /// The value of this under a solution. It is the one thing every kind of expression can do and
    /// the others cannot, which is what lets a single <c>solution.Value</c> serve them all; read it
    /// through <see cref="Evaluation"/> rather than calling it.
    /// </summary>
    internal TValue Read(Solution solution);
}

/// <summary>A linear expression read as a <typeparamref name="TValue"/> through a decoder.</summary>
public interface IDecodedExpression<out TValue> : IProjectedExpression, IReadableExpression<TValue> {
    /// <summary>How the underlying number is read as a <typeparamref name="TValue"/>.</summary>
    IDecoder<TValue, double> Decoder { get; }

    TValue IReadableExpression<TValue>.Read(Solution solution) =>
        Decoder.Decode(Math.Round(solution.Value(Expression), Evaluation.DecimalPlaces));
}

/// <summary>
/// Something a plain <typeparamref name="TValue"/> can stand for, in a model. Where that is all it
/// is, no solution can be asked for one: <c>Preselect</c> gives one of these.
/// </summary>
public interface IWritableExpression<in TValue> : IProjectedExpression {
    /// <summary>How a <typeparamref name="TValue"/> is written as a number.</summary>
    IEncoder<TValue, double> Encoder { get; }
}
