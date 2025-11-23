namespace DocTask.Api.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Convert DateTime to DateOnly
    /// </summary>
    public static DateOnly ToDateOnly(this DateTime dateTime)
    {
        return DateOnly.FromDateTime(dateTime);
    }

    /// <summary>
    /// Convert nullable DateTime to nullable DateOnly
    /// </summary>
    public static DateOnly? ToDateOnly(this DateTime? dateTime)
    {
        return dateTime?.ToDateOnly();
    }

    /// <summary>
    /// Convert DateOnly to DateTime (using midnight as time)
    /// </summary>
    public static DateTime ToDateTime(this DateOnly dateOnly)
    {
        return dateOnly.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// Convert nullable DateOnly to nullable DateTime
    /// </summary>
    public static DateTime? ToDateTime(this DateOnly? dateOnly)
    {
        return dateOnly?.ToDateTime();
    }

    /// <summary>
    /// Convert DateOnly to DateTime with specific time
    /// </summary>
    public static DateTime ToDateTime(this DateOnly dateOnly, TimeOnly timeOnly)
    {
        return dateOnly.ToDateTime(timeOnly);
    }

    /// <summary>
    /// Convert nullable DateOnly to nullable DateTime with specific time
    /// </summary>
    public static DateTime? ToDateTime(this DateOnly? dateOnly, TimeOnly timeOnly)
    {
        return dateOnly?.ToDateTime(timeOnly);
    }
}