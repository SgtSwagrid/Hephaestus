using System.Numerics;

namespace Hephaestus;

/// <summary>
/// Whole numbers of any integer type, standing for themselves. A value is rounded to the nearest
/// whole number as it is read, since a solver's 2.9999999 is a three.
/// </summary>
public sealed record WholeNumberProjection<T> : IProjection<T> where T : IBinaryInteger<T> {
    /// <inheritdoc/>
    public double Encode(T value) => double.CreateChecked(value);

    /// <inheritdoc/>
    public T Decode(double number) => T.CreateChecked(Math.Round(number));
}

/// <summary>Real numbers of any floating-point type (<see cref="decimal"/> included), standing for themselves.</summary>
public sealed record RealNumberProjection<T> : IProjection<T> where T : IFloatingPoint<T> {
    /// <inheritdoc/>
    public double Encode(T value) => double.CreateChecked(value);

    /// <inheritdoc/>
    public T Decode(double number) => T.CreateChecked(number);
}

/// <summary>
/// Typed variables for plain numbers: a count that reads back as an <see cref="int"/>, a cost that
/// reads back as a <see cref="decimal"/>. They keep kinds of number apart (a count plus one and a
/// half does not compile) and spare the casts on the way out.
/// </summary>
public static class NumberVariables {
    extension(Variable) {
        /// <summary>A whole-number variable that is read back as a <typeparamref name="T"/>: <c>Variable.Integer&lt;int&gt;("trains")</c>.</summary>
        public static Quantity<T> Integer<T>(string name) where T : IBinaryInteger<T> => new(Variable.Integer(name), new WholeNumberProjection<T>());

        /// <summary>A real-valued variable that is read back as a <typeparamref name="T"/>: <c>Variable.Continuous&lt;decimal&gt;("cost")</c>.</summary>
        public static Quantity<T> Continuous<T>(string name) where T : IFloatingPoint<T> => new(Variable.Continuous(name), new RealNumberProjection<T>());
    }
}
