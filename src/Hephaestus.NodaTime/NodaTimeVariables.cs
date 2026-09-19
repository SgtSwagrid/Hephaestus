using NodaTime;

namespace Hephaestus.NodaTime;

/// <summary>
/// Typed variables for the NodaTime types, offered alongside <c>Variable.Continuous</c> and friends.
/// In every case <c>unit</c> is what the number one stands for (a second unless stated), and
/// <c>inWholeUnits</c> makes the underlying variable an integer, for quantised times.
/// </summary>
public static class NodaTimeVariables {
    extension(Variable) {
        /// <summary>A duration variable.</summary>
        public static Quantity<Duration> Duration(string name, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new DurationProjection(unit ?? Second));

        /// <summary>An instant variable, measured from <paramref name="origin"/>; choose an origin near the values of interest, such as the start of the planning horizon.</summary>
        public static Point<Instant, Duration> Instant(string name, Instant origin, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new InstantProjection(origin, unit ?? Second));

        /// <summary>A local date-time variable, measured from <paramref name="origin"/> along a local time line without daylight-saving gaps.</summary>
        public static Point<LocalDateTime, Duration> LocalDateTime(string name, LocalDateTime origin, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new LocalDateTimeProjection(origin, unit ?? Second));

        /// <summary>An offset date-time variable; solutions are reported with the origin's offset.</summary>
        public static Point<OffsetDateTime, Duration> OffsetDateTime(string name, OffsetDateTime origin, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new OffsetDateTimeProjection(origin, unit ?? Second));

        /// <summary>A zoned date-time variable; solutions are reported in the origin's zone.</summary>
        public static Point<ZonedDateTime, Duration> ZonedDateTime(string name, ZonedDateTime origin, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new ZonedDateTimeProjection(origin, unit ?? Second));

        /// <summary>A time-of-day variable, measured from midnight. Remember to constrain it to the day.</summary>
        public static Point<LocalTime, Duration> LocalTime(string name, Duration? unit = null, bool inWholeUnits = false) =>
            new(Underlying(name, inWholeUnits), new LocalTimeProjection(unit ?? Second));

        /// <summary>A date variable, in whole days from <paramref name="origin"/>.</summary>
        public static Point<LocalDate, Period> LocalDate(string name, LocalDate origin) =>
            new(Variable.Integer(name), new LocalDateProjection(origin));
    }

    private static Duration Second { get; } = global::NodaTime.Duration.FromSeconds(1);

    private static IVariable Underlying(string name, bool inWholeUnits) =>
        inWholeUnits ? Variable.Integer(name) : Variable.Continuous(name);
}
