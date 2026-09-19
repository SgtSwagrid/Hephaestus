using NodaTime;

namespace Hephaestus.NodaTime;

/// <summary>Durations measured in multiples of a unit.</summary>
public sealed record DurationProjection(Duration Unit) : IProjection<Duration> {
    /// <inheritdoc/>
    public double Encode(Duration value) => value / Unit;

    /// <inheritdoc/>
    public Duration Decode(double number) => Unit * number;
}

/// <summary>Instants measured in multiples of a unit since an origin.</summary>
public sealed record InstantProjection(
    Instant Origin,
    Duration Unit
) : IPointProjection<Instant, Duration> {
    /// <inheritdoc/>
    public IProjection<Duration> Delta => new DurationProjection(Unit);

    /// <inheritdoc/>
    public double Encode(Instant value) => (value - Origin) / Unit;

    /// <inheritdoc/>
    public Instant Decode(double number) => Origin + Unit * number;
}

/// <summary>
/// Local date-times measured in multiples of a unit since an origin, along a local time line on
/// which every day has twenty-four hours (as it has for a railway timetable within one zone).
/// </summary>
public sealed record LocalDateTimeProjection(
    LocalDateTime Origin,
    Duration Unit
) : IPointProjection<LocalDateTime, Duration> {
    /// <inheritdoc/>
    public IProjection<Duration> Delta => new DurationProjection(Unit);

    /// <inheritdoc/>
    public double Encode(LocalDateTime value) => (value.InUtc().ToInstant() - Origin.InUtc().ToInstant()) / Unit;

    /// <inheritdoc/>
    public LocalDateTime Decode(double number) => (Origin.InUtc().ToInstant() + Unit * number).InUtc().LocalDateTime.WithCalendar(Origin.Calendar);
}

/// <summary>Offset date-times measured along the global time line; decoded values carry the origin's offset.</summary>
public sealed record OffsetDateTimeProjection(
    OffsetDateTime Origin,
    Duration Unit
) : IPointProjection<OffsetDateTime, Duration> {
    /// <inheritdoc/>
    public IProjection<Duration> Delta => new DurationProjection(Unit);

    /// <inheritdoc/>
    public double Encode(OffsetDateTime value) => (value.ToInstant() - Origin.ToInstant()) / Unit;

    /// <inheritdoc/>
    public OffsetDateTime Decode(double number) => (Origin.ToInstant() + Unit * number).WithOffset(Origin.Offset);
}

/// <summary>Zoned date-times measured along the global time line; decoded values are in the origin's zone.</summary>
public sealed record ZonedDateTimeProjection(
    ZonedDateTime Origin,
    Duration Unit
) : IPointProjection<ZonedDateTime, Duration> {
    /// <inheritdoc/>
    public IProjection<Duration> Delta => new DurationProjection(Unit);

    /// <inheritdoc/>
    public double Encode(ZonedDateTime value) => (value.ToInstant() - Origin.ToInstant()) / Unit;

    /// <inheritdoc/>
    public ZonedDateTime Decode(double number) => (Origin.ToInstant() + Unit * number).InZone(Origin.Zone);
}

/// <summary>
/// Times of day measured in multiples of a unit since midnight. A time of day cannot leave its
/// day, so constrain such variables to it (<c>time.Between(LocalTime.Midnight, LocalTime.MaxValue)</c>);
/// numbers outside the day decode to its nearest end.
/// </summary>
public sealed record LocalTimeProjection(Duration Unit) : IPointProjection<LocalTime, Duration> {
    /// <inheritdoc/>
    public IProjection<Duration> Delta => new DurationProjection(Unit);

    /// <inheritdoc/>
    public double Encode(LocalTime value) => Duration.FromNanoseconds(value.NanosecondOfDay) / Unit;

    /// <inheritdoc/>
    public LocalTime Decode(double number) =>
        LocalTime.FromNanosecondsSinceMidnight(Math.Clamp((long)Math.Round((Unit * number).TotalNanoseconds), 0, NodaConstants.NanosecondsPerDay - 1));
}

/// <summary>Dates measured in whole days since an origin.</summary>
public sealed record LocalDateProjection(LocalDate Origin) : IPointProjection<LocalDate, Period> {
    /// <inheritdoc/>
    public IProjection<Period> Delta => new DayPeriodProjection();

    /// <inheritdoc/>
    public double Encode(LocalDate value) => Period.DaysBetween(Origin, value);

    /// <inheritdoc/>
    public LocalDate Decode(double number) => Origin.PlusDays((int)Math.Round(number));
}

/// <summary>
/// Periods measured in whole days. Only periods made of days and weeks have a fixed length, so
/// only those can be encoded: a month is not a number of days.
/// </summary>
public sealed record DayPeriodProjection : IProjection<Period> {
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The period has a year, month or time-of-day component.</exception>
    public double Encode(Period value) =>
        value is { Years: 0, Months: 0, HasTimeComponent: false }
            ? value.Weeks * 7 + value.Days
            : throw new ArgumentException($"Only periods of days and weeks have a fixed length; '{value}' does not.", nameof(value));

    /// <inheritdoc/>
    public Period Decode(double number) => Period.FromDays((int)Math.Round(number));
}
