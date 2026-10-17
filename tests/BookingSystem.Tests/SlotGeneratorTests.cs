using BookingSystem.Models;
using BookingSystem.Services;

namespace BookingSystem.Tests;

public class SlotGeneratorTests
{
    private static readonly DateOnly Date = new(2026, 10, 19);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T12:00:00Z");
    private static WorkingHours Hours => new() { DayOfWeek = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(17, 0) };

    [Fact]
    public void Generate_UsesFifteenMinuteStepsAndFitsWholeVisit()
    {
        var slots = SlotGenerator.Generate(Date, Hours, 30, [], Now);
        Assert.Equal(31, slots.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T07:00:00Z"), slots[0].StartTime);
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T14:30:00Z"), slots[^1].StartTime);
        Assert.All(slots.Zip(slots.Skip(1)), pair => Assert.Equal(TimeSpan.FromMinutes(15), pair.Second.StartTime - pair.First.StartTime));
    }

    [Fact]
    public void Generate_RemovesOverlapsButAllowsAdjacentVisits()
    {
        AvailableSlot[] occupied = [new(DateTimeOffset.Parse("2026-10-19T08:00:00Z"), DateTimeOffset.Parse("2026-10-19T08:30:00Z"))];
        var slots = SlotGenerator.Generate(Date, Hours, 30, occupied, Now);
        Assert.Equal(28, slots.Count);
        Assert.DoesNotContain(slots, slot => slot.StartTime == occupied[0].StartTime.AddMinutes(-15));
        Assert.DoesNotContain(slots, slot => slot.StartTime == occupied[0].StartTime);
        Assert.DoesNotContain(slots, slot => slot.StartTime == occupied[0].StartTime.AddMinutes(15));
        Assert.Contains(slots, slot => slot.EndTime == occupied[0].StartTime);
        Assert.Contains(slots, slot => slot.StartTime == occupied[0].EndTime);
    }

    [Fact]
    public void Generate_SkipsPastTimesAndTheCurrentInstant()
    {
        var now = DateTimeOffset.Parse("2026-10-19T08:00:00Z");
        var slots = SlotGenerator.Generate(Date, Hours, 30, [], now);
        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.True(slot.StartTime > now));
    }

    [Fact]
    public void Generate_RejectsPastDaysWrongWeekdaysAndDatesBeyondHorizon()
    {
        Assert.Empty(SlotGenerator.Generate(Date, Hours, 30, [], Now.AddDays(3)));
        Assert.Empty(SlotGenerator.Generate(Date.AddDays(1), Hours, 30, [], Now));
        Assert.Empty(SlotGenerator.Generate(Date.AddYears(2), Hours, 30, [], Now));
    }

    [Fact]
    public void Generate_ReturnsEmptyWhenServiceDoesNotFit()
    {
        var hours = Hours;
        hours.EndTime = new TimeOnly(9, 15);
        Assert.Empty(SlotGenerator.Generate(Date, hours, 30, [], Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(481)]
    public void Generate_RejectsInvalidDurations(int duration) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SlotGenerator.Generate(Date, Hours, duration, [], Now));

    [Theory]
    [InlineData("2026-01-19", "2026-01-19T08:00:00Z")]
    [InlineData("2026-07-20", "2026-07-20T07:00:00Z")]
    public void Generate_UsesWarsawWinterAndSummerOffsets(string date, string expected)
    {
        var day = DateOnly.Parse(date);
        var slots = SlotGenerator.Generate(day, Hours, 30, [], DateTimeOffset.Parse(expected).AddDays(-1));
        Assert.Equal(DateTimeOffset.Parse(expected), slots[0].StartTime);
    }

    [Theory]
    [InlineData("2026-03-29T02:30:00")]
    [InlineData("2026-10-25T02:30:00")]
    public void ToUtc_RejectsNonexistentOrAmbiguousDstTimes(string local) =>
        Assert.Null(SlotGenerator.ToUtc(DateTime.Parse(local)));
}
