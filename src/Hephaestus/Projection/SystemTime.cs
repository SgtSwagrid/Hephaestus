namespace Hephaestus;

/// <summary>Time spans measured in multiples of a unit.</summary>
public sealed record TimeSpanProjection(TimeSpan Unit) : IProjection<TimeSpan> {
    /// <inheritdoc/>
    public double Encode(TimeSpan value) => value / Unit;

    /// <inheritdoc/>
    public TimeSpan Decode(double number) => Unit * number;
}

/// <summary>Date-times measured in multiples of a unit since an origin.</summary>
public sealed record DateTimeProjection(
    DateTime Origin,
    TimeSpan Unit
) : IPointProjection<DateTime, TimeSpan> {
    /// <inheritdoc/>
    public IProjection<TimeSpan> Delta => new TimeSpanProjection(Unit);

    /// <inheritdoc/>
    public double Encode(DateTime value) => (value - Origin) / Unit;

    /// <inheritdoc/>
    public DateTime Decode(double number) => Origin + Unit * number;
}

/// <summary>Date-times with offsets, measured in multiples of a unit since an origin.</summary>
public sealed record DateTimeOffsetProjection(
    DateTimeOffset Origin,
    TimeSpan Unit
) : IPointProjection<DateTimeOffset, TimeSpan> {
    /// <inheritdoc/>
    public IProjection<TimeSpan> Delta => new TimeSpanProjection(Unit);

    /// <inheritdoc/>
    public double Encode(DateTimeOffset value) => (value - Origin) / Unit;

    /// <inheritdoc/>
    public DateTimeOffset Decode(double number) => Origin + Unit * number;
}

/// <summary>A plain date-time shifted by a time-span quantity is a point whose origin is that date-time.</summary>
public static class SystemTimeOperators {
    extension(Quantity<TimeSpan>) {
        public static Point<DateTime, TimeSpan> operator +(DateTime origin, Quantity<TimeSpan> shift) => shift.Beyond(origin, new DateTimeProjection(origin, shift.Unit));
        public static Point<DateTime, TimeSpan> operator +(Quantity<TimeSpan> shift, DateTime origin) => origin + shift;
        public static Point<DateTime, TimeSpan> operator -(DateTime origin, Quantity<TimeSpan> shift) => origin + -shift;

        public static Point<DateTimeOffset, TimeSpan> operator +(DateTimeOffset origin, Quantity<TimeSpan> shift) => shift.Beyond(origin, new DateTimeOffsetProjection(origin, shift.Unit));
        public static Point<DateTimeOffset, TimeSpan> operator +(Quantity<TimeSpan> shift, DateTimeOffset origin) => origin + shift;
        public static Point<DateTimeOffset, TimeSpan> operator -(DateTimeOffset origin, Quantity<TimeSpan> shift) => origin + -shift;
    }
}

/// <summary>Typed variables for the time types of the base class library.</summary>
public static class SystemTimeVariables {
    extension(Variable) {
        /// <summary>A time-span variable, measured in <paramref name="unit"/>s (seconds unless stated).</summary>
        /// <param name="name">The name of the underlying variable.</param>
        /// <param name="unit">The span that the number one stands for. Pick it so that typical values are neither tiny nor huge.</param>
        /// <param name="inWholeUnits">Whether the span must be a whole number of units, making the underlying variable an integer.</param>
        public static Quantity<TimeSpan> TimeSpan(string name, TimeSpan? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new TimeSpanProjection(unit ?? Second));

        /// <summary>A date-time variable, measured in <paramref name="unit"/>s since <paramref name="origin"/>.</summary>
        /// <param name="name">The name of the underlying variable.</param>
        /// <param name="origin">The date-time that the number zero stands for; choose one near the values of interest, such as the start of the planning horizon.</param>
        /// <param name="unit">The span that the number one stands for (a second unless stated).</param>
        /// <param name="inWholeUnits">Whether the date-time must lie a whole number of units from the origin.</param>
        public static Point<DateTime, TimeSpan> DateTime(string name, DateTime origin, TimeSpan? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new DateTimeProjection(origin, unit ?? Second));

        /// <inheritdoc cref="DateTime(string, System.DateTime, System.TimeSpan?, bool)"/>
        public static Point<DateTimeOffset, TimeSpan> DateTimeOffset(string name, DateTimeOffset origin, TimeSpan? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new DateTimeOffsetProjection(origin, unit ?? Second));
    }

    private static TimeSpan Second { get; } = System.TimeSpan.FromSeconds(1);

    private static IVariable Underlying(string name, bool inWholeUnits) =>
        inWholeUnits ? Variable.Integer(name) : Variable.Continuous(name);
}
