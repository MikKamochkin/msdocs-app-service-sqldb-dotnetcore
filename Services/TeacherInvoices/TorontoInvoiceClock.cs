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
    public (DateTime Start, DateTime End) LastWeek()
    {
        var today = Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (monday.AddDays(-7), monday.AddDays(-1));
    }
}
