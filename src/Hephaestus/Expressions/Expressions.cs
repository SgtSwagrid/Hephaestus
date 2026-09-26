using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// One of the things a solver holds towards a projected value: a linear expression read as a
/// number, or a boolean expression read as a truth. The cases are <see cref="LinearComponent"/> and
/// <see cref="LogicalComponent"/>. The kind is recorded rather than read off the expression, because
/// a <see cref="BinaryVariable"/> is both kinds of expression and only its projection knows which it is.
/// </summary>
public interface IComponent;

/// <summary>A linear expression, standing for a number in the raw form.</summary>
public sealed record LinearComponent(ILinearExpression Expression) : IComponent;

/// <summary>A boolean expression, standing for a truth in the raw form: <c>1</c> if it holds and <c>0</c> if not.</summary>
public sealed record LogicalComponent(IBooleanExpression Expression) : IComponent;

/// <summary>
/// Expressions read together as something else, through one or both halves of a projection. The
/// raw form is a vector with one entry per component, in order: a quantity has one component, and
/// a pair of quantities has two.
/// </summary>
public interface IProjectedExpression {
    /// <summary>The components, one for each entry of the raw form.</summary>
    ImmutableArray<IComponent> Components { get; }
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

/// <summary>Components read as a <typeparamref name="TValue"/> through a decoder.</summary>
public interface IDecodedExpression<out TValue> : IProjectedExpression, IReadableExpression<TValue> {
    /// <summary>How the raw form is read as a <typeparamref name="TValue"/>.</summary>
    IDecoder<TValue, ImmutableArray<double>> Decoder { get; }

    TValue IReadableExpression<TValue>.Read(Solution solution) => Decoder.Decode(Evaluation.Raw(solution, Components));
}

/// <summary>
/// Something a plain <typeparamref name="TValue"/> can stand for, in a model. Where that is all it
/// is, no solution can be asked for one: <c>Preselect</c> gives one of these.
/// </summary>
public interface IWritableExpression<in TValue> : IProjectedExpression {
    /// <summary>How a <typeparamref name="TValue"/> is written in the raw form.</summary>
    IEncoder<TValue, ImmutableArray<double>> Encoder { get; }
}
