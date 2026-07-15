using JuggerHub.Entities;
using JuggerHub.Services.Trainings;

namespace JuggerHub.Api.IntegrationTests.Trainings;

/// <summary>
/// Pure unit tests for the recurrence math (feature 018, SC-002). No database — just the calendar
/// expansion the create/edit/regenerate paths rely on.
/// </summary>
public sealed class RecurrenceExpanderTests
{
    [Fact]
    public void Weekly_counts_every_occurrence_of_the_weekday_inclusive()
    {
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 16), DayOfWeek.Tuesday, TrainingInterval.Weekly, new DateOnly(2025, 11, 11));

        // Sep 16,23,30; Oct 7,14,21,28; Nov 4,11 = 9
        Assert.Equal(9, dates.Count);
        Assert.Equal(new DateOnly(2025, 9, 16), dates[0]);
        Assert.Equal(new DateOnly(2025, 11, 11), dates[^1]);
        Assert.All(dates, d => Assert.Equal(DayOfWeek.Tuesday, d.DayOfWeek));
    }

    [Fact]
    public void BiWeekly_steps_fourteen_days()
    {
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 16), DayOfWeek.Tuesday, TrainingInterval.BiWeekly, new DateOnly(2025, 11, 11));

        Assert.Equal(
            new[] { new DateOnly(2025, 9, 16), new DateOnly(2025, 9, 30), new DateOnly(2025, 10, 14), new DateOnly(2025, 10, 28), new DateOnly(2025, 11, 11) },
            dates);
    }

    [Fact]
    public void Monthly_keeps_the_same_weekday_of_month_position()
    {
        // Sep 16 2025 is the 3rd Tuesday of September.
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 16), DayOfWeek.Tuesday, TrainingInterval.Monthly, new DateOnly(2026, 1, 31));

        Assert.Equal(
            new[] { new DateOnly(2025, 9, 16), new DateOnly(2025, 10, 21), new DateOnly(2025, 11, 18), new DateOnly(2025, 12, 16), new DateOnly(2026, 1, 20) },
            dates);
    }

    [Fact]
    public void Monthly_skips_months_lacking_the_fifth_weekday()
    {
        // Jul 29 2025 is the 5th Tuesday of July; only Sep and Dec 2025 also have a 5th Tuesday.
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 7, 29), DayOfWeek.Tuesday, TrainingInterval.Monthly, new DateOnly(2025, 12, 31));

        Assert.Equal(
            new[] { new DateOnly(2025, 7, 29), new DateOnly(2025, 9, 30), new DateOnly(2025, 12, 30) },
            dates);
    }

    [Fact]
    public void First_occurrence_aligns_to_the_weekday_on_or_after_the_start()
    {
        // Start Monday Sep 15; first Tuesday is Sep 16.
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 15), DayOfWeek.Tuesday, TrainingInterval.Weekly, new DateOnly(2025, 9, 30));

        Assert.Equal(new[] { new DateOnly(2025, 9, 16), new DateOnly(2025, 9, 23), new DateOnly(2025, 9, 30) }, dates);
    }

    [Fact]
    public void Single_day_range_on_the_weekday_yields_one()
    {
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 16), DayOfWeek.Tuesday, TrainingInterval.Weekly, new DateOnly(2025, 9, 16));

        Assert.Single(dates);
    }

    [Fact]
    public void Range_without_the_weekday_yields_none()
    {
        // Mon-only range: no Tuesday.
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 15), DayOfWeek.Tuesday, TrainingInterval.Weekly, new DateOnly(2025, 9, 15));

        Assert.Empty(dates);
    }

    [Fact]
    public void End_before_start_yields_none()
    {
        var dates = RecurrenceExpander.Expand(
            new DateOnly(2025, 9, 16), DayOfWeek.Tuesday, TrainingInterval.Weekly, new DateOnly(2025, 9, 1));

        Assert.Empty(dates);
    }
}
