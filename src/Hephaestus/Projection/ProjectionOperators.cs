namespace Hephaestus;

/// <summary>
/// Re-viewing a projection through a pair of functions. Each half admits exactly the mapping its
/// variance allows: a decoder may be mapped forwards, an encoder backwards, and a whole projection
/// both ways at once. Mapping a projection forwards alone gives a decoder, which is how a reading
/// that no constraint can mention comes about: <c>seconds.Select(n =&gt; $"{n}s")</c>.
/// </summary>
public static class ProjectionOperators {
    extension<TValue, TRaw>(IDecoder<TValue, TRaw> decoder) {
        /// <summary>This decoder, reading its value as something else afterwards.</summary>
        public IDecoder<TOther, TRaw> Select<TOther>(Func<TValue, TOther> selector) =>
            new SelectedDecoder<TOther, TRaw>(representation => selector(decoder.Decode(representation)));
    }

    extension<TValue, TRaw>(IEncoder<TValue, TRaw> encoder) {
        /// <summary>This encoder, taking something else and turning it into a value first.</summary>
        public IEncoder<TOther, TRaw> Preselect<TOther>(Func<TOther, TValue> selector) =>
            new SelectedEncoder<TOther, TRaw>(other => encoder.Encode(selector(other)));
    }

    extension<TValue>(IProjection<TValue> projection) {
        /// <inheritdoc cref="Biselect{TValue, TRaw, TOther}(IProjection{TValue, TRaw}, Func{TValue, TOther}, Func{TOther, TValue})"/>
        public IProjection<TOther> Biselect<TOther>(Func<TValue, TOther> forward, Func<TOther, TValue> backward) =>
            new SelectedNumberProjection<TOther>(
                number => forward(projection.Decode(number)),
                other => projection.Encode(backward(other)));
    }

    extension<TValue, TRaw>(IProjection<TValue, TRaw> projection) {
        /// <summary>
        /// This projection seen as one of another type, given the correspondence between the two:
        /// <c>seconds.Biselect(TimeSpan.FromSeconds, span =&gt; (long)span.TotalSeconds)</c>.
        /// </summary>
        public IProjection<TOther, TRaw> Biselect<TOther>(Func<TValue, TOther> forward, Func<TOther, TValue> backward) =>
            new SelectedProjection<TOther, TRaw>(
                representation => forward(projection.Decode(representation)),
                other => projection.Encode(backward(other)));
    }
}

/// <summary>
/// A decoder built from a function. Two of these never compare equal, even where they read alike,
/// because two functions never do; write a record of your own where projections must compare.
/// </summary>
internal sealed record SelectedDecoder<TValue, TRaw>(Func<TRaw, TValue> Reading) : IDecoder<TValue, TRaw> {
    /// <inheritdoc/>
    public TValue Decode(TRaw representation) => Reading(representation);
}

/// <inheritdoc cref="SelectedDecoder{TValue, TRaw}"/>
internal sealed record SelectedEncoder<TValue, TRaw>(Func<TValue, TRaw> Writing) : IEncoder<TValue, TRaw> {
    /// <inheritdoc/>
    public TRaw Encode(TValue value) => Writing(value);
}

/// <inheritdoc cref="SelectedDecoder{TValue, TRaw}"/>
internal sealed record SelectedProjection<TValue, TRaw>(
    Func<TRaw, TValue> Reading,
    Func<TValue, TRaw> Writing
) : IProjection<TValue, TRaw> {
    /// <inheritdoc/>
    public TValue Decode(TRaw representation) => Reading(representation);

    /// <inheritdoc/>
    public TRaw Encode(TValue value) => Writing(value);
}

/// <inheritdoc cref="SelectedDecoder{TValue, TRaw}"/>
internal sealed record SelectedNumberProjection<TValue>(
    Func<double, TValue> Reading,
    Func<TValue, double> Writing
) : IProjection<TValue> {
    /// <inheritdoc/>
    public TValue Decode(double representation) => Reading(representation);

    /// <inheritdoc/>
    public double Encode(TValue value) => Writing(value);
}
