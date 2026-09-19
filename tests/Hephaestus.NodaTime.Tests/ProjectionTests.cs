using NodaTime;

namespace Hephaestus.NodaTime.Tests;

/// <summary>Each projection must be affine, increasing and invertible; everything else in the typed layer rests on that.</summary>
public sealed class ProjectionTests {
    private static readonly LocalDateTime LocalOrigin = new(2026, 9, 19, 8, 0);
    private static readonly Instant InstantOrigin = Instant.FromUtc(2026, 9, 19, 8, 0);
    private static readonly Duration Minute = Duration.FromMinutes(1);

    [Fact]
    public void DurationsAreMeasuredInTheirUnit() {
        var projection = new DurationProjection(Minute);

        Assert.Equal(2.5, projection.Encode(Duration.FromSeconds(150)));
        Assert.Equal(Duration.FromSeconds(150), projection.Decode(2.5));
        Assert.Equal(0, projection.Encode(Duration.Zero));
    }

    [Fact]
    public void InstantsAreMeasuredFromTheirOrigin() {
        var projection = new InstantProjection(InstantOrigin, Minute);

        Assert.Equal(90, projection.Encode(InstantOrigin + Duration.FromHours(1.5)));
        Assert.Equal(InstantOrigin - Duration.FromMinutes(30), projection.Decode(-30));
        Assert.Equal(new DurationProjection(Minute), projection.Delta);
    }

    [Fact]
    public void LocalDateTimesAreMeasuredAlongAnUnbrokenLocalTimeLine() {
        var projection = new LocalDateTimeProjection(LocalOrigin, Duration.FromSeconds(1));

        Assert.Equal(86_400 + 3_600, projection.Encode(LocalOrigin.PlusDays(1).PlusHours(1)));
        Assert.Equal(LocalOrigin.PlusSeconds(42), projection.Decode(42));
        Assert.Equal(LocalOrigin.PlusNanoseconds(500_000_000), projection.Decode(0.5));
    }

    [Fact]
    public void OffsetAndZonedDateTimesKeepTheOffsetAndZoneOfTheirOrigin() {
        var sydney = DateTimeZoneProviders.Tzdb["Australia/Sydney"];
        var zoned = new ZonedDateTimeProjection(InstantOrigin.InZone(sydney), Minute);
        var offset = new OffsetDateTimeProjection(InstantOrigin.WithOffset(Offset.FromHours(10)), Minute);

        Assert.Equal(sydney, zoned.Decode(15).Zone);
        Assert.Equal(InstantOrigin + Duration.FromMinutes(15), zoned.Decode(15).ToInstant());
        Assert.Equal(15, zoned.Encode((InstantOrigin + Duration.FromMinutes(15)).InUtc()));
        Assert.Equal(Offset.FromHours(10), offset.Decode(15).Offset);
        Assert.Equal(15, offset.Encode((InstantOrigin + Duration.FromMinutes(15)).WithOffset(Offset.Zero)));
    }

    [Fact]
    public void TimesOfDayAreMeasuredFromMidnightAndNeverLeaveTheDay() {
        var projection = new LocalTimeProjection(Minute);

        Assert.Equal(8 * 60 + 30, projection.Encode(new LocalTime(8, 30)));
        Assert.Equal(new LocalTime(8, 30), projection.Decode(8 * 60 + 30));
        Assert.Equal(LocalTime.Midnight, projection.Decode(-5));
        Assert.Equal(LocalTime.MaxValue, projection.Decode(24 * 60 + 5));
    }

    [Fact]
    public void DatesAreMeasuredInWholeDays() {
        var projection = new LocalDateProjection(new LocalDate(2026, 9, 19));

        Assert.Equal(12, projection.Encode(new LocalDate(2026, 10, 1)));
        Assert.Equal(new LocalDate(2026, 10, 1), projection.Decode(12.0000001));
        Assert.Equal(new LocalDate(2026, 9, 18), projection.Decode(-1));
    }

    [Fact]
    public void OnlyPeriodsOfFixedLengthCanBeMeasured() {
        var projection = new DayPeriodProjection();

        Assert.Equal(17, projection.Encode(Period.FromWeeks(2) + Period.FromDays(3)));
        Assert.Equal(Period.FromDays(3), projection.Decode(3));
        Assert.Throws<ArgumentException>(() => projection.Encode(Period.FromMonths(1)));
        Assert.Throws<ArgumentException>(() => projection.Encode(Period.FromHours(1)));
    }

    [Fact]
    public void EqualProjectionsCompareEqualSoThatNoConversionIsNeededBetweenThem() {
        Assert.Equal(new LocalDateTimeProjection(LocalOrigin, Minute), new LocalDateTimeProjection(LocalOrigin, Minute));
        Assert.NotEqual(new LocalDateTimeProjection(LocalOrigin, Minute), new LocalDateTimeProjection(LocalOrigin.PlusHours(1), Minute));
        Assert.Equal(new LocalDateTimeProjection(LocalOrigin, Minute).Delta, new InstantProjection(InstantOrigin, Minute).Delta);
    }
}
