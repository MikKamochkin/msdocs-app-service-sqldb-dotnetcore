namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public sealed class TorontoInvoiceClock
{
    private static readonly TimeZoneInfo Zone = GetZone();
    private static TimeZoneInfo GetZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
    public DateTime Today => FromUtc(DateTime.UtcNow).Date;
    public DateTime FromUtc(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public DateTime ToUtc(DateTime toronto) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(toronto, DateTimeKind.Unspecified), Zone);
    // Monday to Wednesday defaults to last week; Thursday to Sunday defaults to this week.
    public (DateTime Start, DateTime End) DefaultInvoiceWeek(DateTime today)
    {
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-daysSinceMonday);
        var start = daysSinceMonday <= 2 ? monday.AddDays(-7) : monday;
        return (start, start.AddDays(6));
    }
}
