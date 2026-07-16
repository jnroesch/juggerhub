using JuggerHub.Entities;

namespace JuggerHub.Services.Trainings;

/// <summary>
/// Pure calendar math for a training series (feature 018): given a start date, a weekday, an interval and
/// an inclusive end date, produce the ordered list of session dates. Isolated and exhaustively unit-tested
/// so the create/edit/regenerate services stay thin. No side effects, no time zone — <see cref="DateOnly"/>
/// math is DST-immune.
/// </summary>
/// <remarks>
/// <see cref="TrainingInterval.Weekly"/> steps 7 days, <see cref="TrainingInterval.BiWeekly"/> 14 days from
/// the first on-or-after occurrence of the weekday. <see cref="TrainingInterval.Monthly"/> keeps the same
/// weekday-of-month <em>position</em> as that first occurrence (e.g. "3rd Tuesday"), skipping any month
/// that lacks that position (a 5th-weekday start only recurs in months that have a 5th such weekday).
/// </remarks>
public static class RecurrenceExpander
{
    /// <summary>A sane upper bound so a far-future end date cannot generate an unbounded set.</summary>
    public const int MaxSessions = 520; // ~10 years weekly

    /// <summary>
    /// Ordered session dates from <paramref name="startDate"/> through <paramref name="endDate"/> inclusive.
    /// Returns an empty list when no occurrence of <paramref name="weekday"/> falls in the range.
    /// </summary>
    public static IReadOnlyList<DateOnly> Expand(
        DateOnly startDate, DayOfWeek weekday, TrainingInterval interval, DateOnly endDate)
    {
        var dates = new List<DateOnly>();
        if (endDate < startDate)
        {
            return dates;
        }

        var first = FirstOnOrAfter(startDate, weekday);
        if (first > endDate)
        {
            return dates;
        }

        switch (interval)
        {
            case TrainingInterval.Weekly:
            case TrainingInterval.BiWeekly:
            {
                var step = interval == TrainingInterval.Weekly ? 7 : 14;
                for (var d = first; d <= endDate && dates.Count < MaxSessions; d = d.AddDays(step))
                {
                    dates.Add(d);
                }

                break;
            }

            case TrainingInterval.Monthly:
            {
                // The ordinal position of the first occurrence within its month (1..5).
                var ordinal = (first.Day - 1) / 7 + 1;
                var cursor = new DateOnly(first.Year, first.Month, 1);
                while (dates.Count < MaxSessions)
                {
                    var occurrence = NthWeekdayOfMonth(cursor.Year, cursor.Month, weekday, ordinal);
                    if (occurrence is { } date)
                    {
                        if (date > endDate)
                        {
                            break;
                        }

                        if (date >= first)
                        {
                            dates.Add(date);
                        }
                    }

                    cursor = cursor.AddMonths(1);
                    // Guard against runaway when the ordinal never occurs before the end date.
                    if (cursor > endDate)
                    {
                        break;
                    }
                }

                break;
            }
        }

        return dates;
    }

    /// <summary>The first date on or after <paramref name="from"/> that falls on <paramref name="weekday"/>.</summary>
    private static DateOnly FirstOnOrAfter(DateOnly from, DayOfWeek weekday)
    {
        var delta = ((int)weekday - (int)from.DayOfWeek + 7) % 7;
        return from.AddDays(delta);
    }

    /// <summary>
    /// The <paramref name="ordinal"/>-th (1-based) <paramref name="weekday"/> of the given month, or null
    /// when that month has no such occurrence (e.g. no 5th Tuesday).
    /// </summary>
    private static DateOnly? NthWeekdayOfMonth(int year, int month, DayOfWeek weekday, int ordinal)
    {
        var firstOfMonth = new DateOnly(year, month, 1);
        var firstWeekday = FirstOnOrAfter(firstOfMonth, weekday);
        var day = firstWeekday.AddDays(7 * (ordinal - 1));
        return day.Month == month ? day : null;
    }
}
