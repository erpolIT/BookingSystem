using BookingSystem.Services;

namespace BookingSystem.Tests;

public class BookingTimeRulesTests
{
    [Theory]
    [InlineData(10, 11, 10.5, 11.5, true)]
    [InlineData(10.5, 11.5, 10, 11, true)]
    [InlineData(10, 12, 10.5, 11, true)]
    [InlineData(10.5, 11, 10, 12, true)]
    [InlineData(10, 11, 10, 11, true)]
    [InlineData(10, 11, 11, 12, false)]
    [InlineData(11, 12, 10, 11, false)]
    [InlineData(10, 11, 12, 13, false)]
    public void Overlaps_CoversBothOrdersContainmentAndBoundaries(double a, double b, double c, double d, bool expected)
    {
        var midnight = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var result = BookingTimeRules.Overlaps(midnight.AddHours(a), midnight.AddHours(b), midnight.AddHours(c), midnight.AddHours(d));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Overlaps_ComparesInstantsAcrossDifferentOffsets()
    {
        var start = DateTimeOffset.Parse("2026-10-19T10:00:00+02:00");
        Assert.True(BookingTimeRules.Overlaps(start, start.AddMinutes(30), start.ToUniversalTime(), start.ToUniversalTime().AddMinutes(30)));
    }

    [Theory]
    [InlineData(0, 0, 1, 2)]
    [InlineData(2, 1, 1, 2)]
    [InlineData(1, 2, 0, 0)]
    [InlineData(1, 2, 2, 1)]
    public void Overlaps_RejectsInvalidIntervals(int a, int b, int c, int d)
    {
        var midnight = DateTimeOffset.UnixEpoch;
        Assert.Throws<ArgumentException>(() => BookingTimeRules.Overlaps(midnight.AddHours(a), midnight.AddHours(b), midnight.AddHours(c), midnight.AddHours(d)));
    }
}
