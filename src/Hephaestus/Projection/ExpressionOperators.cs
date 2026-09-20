namespace Hephaestus;

/// <summary>
/// Re-viewing a projected expression through a pair of functions, as its projection is re-viewed.
/// Each half admits the mapping its variance allows, and each gives back what it still is: mapping
/// forwards alone leaves something a solution can be asked for but no constraint can mention;
/// mapping backwards alone leaves the opposite.
/// </summary>
public static class ExpressionOperators {
    extension<TValue>(IDecodedExpression<TValue> readable) {
        /// <summary>This, read as something else afterwards: <c>runtime.Select(span =&gt; $"{span}")</c>.</summary>
        public IDecodedExpression<TOther> Select<TOther>(Func<TValue, TOther> selector) =>
            new ReadableExpression<TOther>(readable.Expression, readable.Decoder.Select(selector));
    }

    extension<TValue>(IWritableExpression<TValue> writable) {
        /// <summary>This, taking something else and turning it into a value first.</summary>
        public IWritableExpression<TOther> Preselect<TOther>(Func<TOther, TValue> selector) =>
            new WritableExpression<TOther>(writable.Expression, writable.Encoder.Preselect(selector));
    }

    extension<TValue>(ILinearlyEncodable<TValue> encodable) {
        /// <summary>
        /// This, read and written as another type, given the correspondence between the two:
        /// <c>seconds.Biselect(TimeSpan.FromSeconds, span =&gt; (long)span.TotalSeconds)</c>.
        /// </summary>
        public ILinearlyEncodable<TOther> Biselect<TOther>(Func<TValue, TOther> forward, Func<TOther, TValue> backward) =>
            new EncodableExpression<TOther>(encodable.Expression, encodable.Projection.Biselect(forward, backward));
    }
}

/// <summary>A linear expression with a reading and nothing more.</summary>
internal sealed record ReadableExpression<TValue>(
    ILinearExpression Expression,
    IDecoder<TValue, double> Decoder
) : IDecodedExpression<TValue>;

/// <summary>A linear expression that a value can be written into, and nothing more.</summary>
internal sealed record WritableExpression<TValue>(
    ILinearExpression Expression,
    IEncoder<TValue, double> Encoder
) : IWritableExpression<TValue>;

/// <summary>A linear expression read and written as a type of its own.</summary>
internal sealed record EncodableExpression<TValue>(
    ILinearExpression Expression,
    IProjection<TValue> Projection
) : ILinearlyEncodable<TValue>;
