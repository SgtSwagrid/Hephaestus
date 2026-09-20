using NodaTime;

namespace Hephaestus.NodaTime;

/// <summary>
/// A plain NodaTime value shifted by a quantity is a point whose origin is that value:
/// <c>start + runtime</c>, <c>runtime + start</c>, <c>start - runtime</c>.
/// </summary>
public static class NodaTimeOperators {
    extension(Quantity<Duration>) {
        public static Point<Instant, Duration> operator +(Instant origin, Quantity<Duration> shift) => shift.Beyond(origin, new InstantProjection(origin, shift.Unit));
        public static Point<Instant, Duration> operator +(Quantity<Duration> shift, Instant origin) => origin + shift;
        public static Point<Instant, Duration> operator -(Instant origin, Quantity<Duration> shift) => origin + -shift;

        public static Point<LocalDateTime, Duration> operator +(LocalDateTime origin, Quantity<Duration> shift) => shift.Beyond(origin, new LocalDateTimeProjection(origin, shift.Unit));
        public static Point<LocalDateTime, Duration> operator +(Quantity<Duration> shift, LocalDateTime origin) => origin + shift;
        public static Point<LocalDateTime, Duration> operator -(LocalDateTime origin, Quantity<Duration> shift) => origin + -shift;

        public static Point<OffsetDateTime, Duration> operator +(OffsetDateTime origin, Quantity<Duration> shift) => shift.Beyond(origin, new OffsetDateTimeProjection(origin, shift.Unit));
        public static Point<OffsetDateTime, Duration> operator +(Quantity<Duration> shift, OffsetDateTime origin) => origin + shift;
        public static Point<OffsetDateTime, Duration> operator -(OffsetDateTime origin, Quantity<Duration> shift) => origin + -shift;

        public static Point<ZonedDateTime, Duration> operator +(ZonedDateTime origin, Quantity<Duration> shift) => shift.Beyond(origin, new ZonedDateTimeProjection(origin, shift.Unit));
        public static Point<ZonedDateTime, Duration> operator +(Quantity<Duration> shift, ZonedDateTime origin) => origin + shift;
        public static Point<ZonedDateTime, Duration> operator -(ZonedDateTime origin, Quantity<Duration> shift) => origin + -shift;

        // A time of day is always measured from midnight, so here the plain value becomes an offset rather than the origin.
        public static Point<LocalTime, Duration> operator +(LocalTime origin, Quantity<Duration> shift) => shift.Beyond(origin, new LocalTimeProjection(shift.Unit));
        public static Point<LocalTime, Duration> operator +(Quantity<Duration> shift, LocalTime origin) => origin + shift;
        public static Point<LocalTime, Duration> operator -(LocalTime origin, Quantity<Duration> shift) => origin + -shift;
    }

    extension(Quantity<Period>) {
        public static Point<LocalDate, Period> operator +(LocalDate origin, Quantity<Period> shift) => shift.Beyond(origin, new LocalDateProjection(origin));
        public static Point<LocalDate, Period> operator +(Quantity<Period> shift, LocalDate origin) => origin + shift;
        public static Point<LocalDate, Period> operator -(LocalDate origin, Quantity<Period> shift) => origin + -shift;
    }
}
