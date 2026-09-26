using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Re-viewing and combining typed expressions. Re-viewing goes through a pair of functions, as a
/// projection's does, and each half admits the mapping its variance allows: mapping forwards alone
/// leaves something a solution can be asked for but no constraint can mention, and mapping
/// backwards alone leaves the opposite. Combining is <c>Zip</c>, which puts two typed expressions
/// side by side as one of a pair, whatever each is made of: numbers, truths, or both.
/// </summary>
public static class ExpressionOperators {
    extension<TValue>(IDecodedExpression<TValue> readable) {
        /// <summary>This, read as something else afterwards: <c>runtime.Select(span =&gt; $"{span}")</c>.</summary>
        public IDecodedExpression<TOther> Select<TOther>(Func<TValue, TOther> selector) =>
            new ReadableExpression<TOther>(readable.Components, readable.Decoder.Select(selector));
    }

    extension<TFirst, TSecond>(IDecodedExpression<(TFirst, TSecond)> pair) {
        /// <summary>This pair, read as something made of its two halves: <c>start.Zip(runtime).Select((from, span) =&gt; from + span)</c>.</summary>
        public IDecodedExpression<TOther> Select<TOther>(Func<TFirst, TSecond, TOther> selector) =>
            pair.Select(value => selector(value.Item1, value.Item2));
    }

    extension<TValue>(IWritableExpression<TValue> writable) {
        /// <summary>This, taking something else and turning it into a value first.</summary>
        public IWritableExpression<TOther> Preselect<TOther>(Func<TOther, TValue> selector) =>
            new WritableExpression<TOther>(writable.Components, writable.Encoder.Preselect(selector));
    }

    extension<TValue>(IEncodable<TValue> encodable) {
        /// <summary>
        /// This, read and written as another type, given the correspondence between the two:
        /// <c>position.Biselect(pair =&gt; new Location(pair.Item1, pair.Item2), location =&gt; (location.X, location.Y))</c>.
        /// </summary>
        public IEncodable<TOther> Biselect<TOther>(Func<TValue, TOther> forward, Func<TOther, TValue> backward) =>
            new EncodableExpression<TOther>(encodable.Components, encodable.Projection.Biselect(forward, backward));

        /// <summary>
        /// This and <paramref name="other"/> side by side, read and written as a pair: the
        /// components of this, then those of the other. Anything typed can be zipped with anything
        /// else typed, and the result can be zipped again, compared entry by entry, read, and given
        /// a starting value.
        /// </summary>
        public IEncodable<(TValue, TOther)> Zip<TOther>(IEncodable<TOther> other) =>
            new EncodableExpression<(TValue, TOther)>(
                [.. encodable.Components, .. other.Components],
                new PairedProjection<TValue, TOther>(encodable.Projection, other.Projection, encodable.Components.Length));
    }

    extension<TFirst, TSecond>(IEncodable<(TFirst, TSecond)> pair) {
        /// <summary>
        /// This pair, read and written as something made of its two halves:
        /// <c>x.Zip(y).Biselect((across, down) =&gt; new Location(across, down), location =&gt; (location.X, location.Y))</c>.
        /// </summary>
        public IEncodable<TOther> Biselect<TOther>(Func<TFirst, TSecond, TOther> forward, Func<TOther, (TFirst, TSecond)> backward) =>
            pair.Biselect(value => forward(value.Item1, value.Item2), backward);
    }

    extension<TValue>(ILinearlyEncodable<TValue> encodable) {
        /// <summary>
        /// This, read and written as another type, given the correspondence between the two, and
        /// still one number: <c>seconds.Biselect(TimeSpan.FromSeconds, span =&gt; (long)span.TotalSeconds)</c>.
        /// </summary>
        public ILinearlyEncodable<TOther> Biselect<TOther>(Func<TValue, TOther> forward, Func<TOther, TValue> backward) =>
            new LinearlyEncodableExpression<TOther>(encodable.Expression, encodable.Projection.Biselect(forward, backward));
    }

    extension<TValue>(IEnumerable<IEncodable<TValue>> encodables) {
        /// <summary>
        /// The typed expressions side by side, read and written as one array of their values:
        /// <c>solution.Value(starts.Sequence())</c> is the whole schedule, and
        /// <c>starts.Sequence().EqualTo(plan)</c> pins it. An array must have one value for each.
        /// </summary>
        public IEncodable<ImmutableArray<TValue>> Sequence() => Sequenced([.. encodables]);
    }

    extension(ILinearExpression expression) {
        /// <summary>This expression as the plain number it stands for, so that it can be zipped with typed ones.</summary>
        public Quantity<double> AsEncodable() => new(expression, new RealNumberProjection<double>());
    }

    extension(IBooleanExpression expression) {
        /// <summary>This expression as the truth it stands for, so that it can be zipped with typed ones.</summary>
        public ILogicallyEncodable<bool> AsEncodable() => new LogicallyEncodableExpression<bool>(expression, new TruthProjection());
    }

    extension(BinaryVariable variable) {
        /// <summary>This variable as the truth it stands for, so that it can be zipped with typed ones. (It is both kinds of expression; this reads it as <c>solution.Value</c> does.)</summary>
        public ILogicallyEncodable<bool> AsEncodable() => new LogicallyEncodableExpression<bool>(variable, new TruthProjection());
    }

    private static IEncodable<ImmutableArray<TValue>> Sequenced<TValue>(ImmutableArray<IEncodable<TValue>> encodables) =>
        new EncodableExpression<ImmutableArray<TValue>>(
            [.. encodables.SelectMany(encodable => encodable.Components)],
            new SequencedProjection<TValue>([.. Parts(encodables)]));

    private static ImmutableList<SequencedPart<TValue>> Parts<TValue>(ImmutableArray<IEncodable<TValue>> encodables) =>
        encodables.Aggregate(
            ImmutableList<SequencedPart<TValue>>.Empty,
            (parts, encodable) => parts.Add(new SequencedPart<TValue>(encodable.Projection, parts.IsEmpty ? 0 : parts[^1].Start + parts[^1].Dimension, encodable.Components.Length)));
}

/// <summary>Components with a reading and nothing more.</summary>
internal sealed record ReadableExpression<TValue>(
    ImmutableArray<IComponent> Components,
    IDecoder<TValue, ImmutableArray<double>> Decoder
) : IDecodedExpression<TValue>;

/// <summary>Components that a value can be written into, and nothing more.</summary>
internal sealed record WritableExpression<TValue>(
    ImmutableArray<IComponent> Components,
    IEncoder<TValue, ImmutableArray<double>> Encoder
) : IWritableExpression<TValue>;

/// <summary>Components read and written as a type of their own. Two are equal when their components and projections are, so zipping the same things twice gives equal zips.</summary>
internal sealed record EncodableExpression<TValue>(
    ImmutableArray<IComponent> Components,
    IProjection<TValue, ImmutableArray<double>> Projection
) : IEncodable<TValue> {
    /// <inheritdoc/>
    public bool Equals(EncodableExpression<TValue>? other) => other is not null && Components.SequenceEqual(other.Components) && Projection.Equals(other.Projection);

    /// <inheritdoc/>
    public override int GetHashCode() => Components.Aggregate(Projection.GetHashCode(), HashCode.Combine);
}

/// <summary>A linear expression read and written as a type of its own.</summary>
internal sealed record LinearlyEncodableExpression<TValue>(
    ILinearExpression Expression,
    IProjection<TValue> Projection
) : ILinearlyEncodable<TValue>;

/// <summary>A boolean expression read and written as a type of its own.</summary>
internal sealed record LogicallyEncodableExpression<TValue>(
    IBooleanExpression Expression,
    IProjection<TValue, bool> Projection
) : ILogicallyEncodable<TValue>;

/// <summary>Truths standing for themselves.</summary>
internal sealed record TruthProjection : IProjection<bool, bool> {
    /// <inheritdoc/>
    public bool Encode(bool value) => value;

    /// <inheritdoc/>
    public bool Decode(bool representation) => representation;
}
