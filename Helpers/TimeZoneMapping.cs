using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

public static class TimeZoneMapping
{
    // Mapping for preferred time zones (offset hour -> system time zone ID)
    public static readonly Dictionary<int, string> PreferredTimeZones = new Dictionary<int, string>
    {
        { -12, "Dateline Standard Time" },
        { -11, "UTC-11" },
        { -10, "Hawaiian Standard Time" },
        { -9,  "Alaskan Standard Time" },
        { -8,  "Pacific Standard Time" },
        { -7,  "US Mountain Standard Time" },
        { -6,  "Central Standard Time" },
        { -5,  "Eastern Standard Time" },
        { -4,  "Atlantic Standard Time" },
        // For non-integer offsets, adjust or add additional mappings as needed.
        {  0,  "GMT Standard Time" },
        {  1,  "Central Europe Standard Time" },
        {  2,  "E. Europe Standard Time" },
        {  3,  "Turkey Standard Time" },
        {  4,  "Azerbaijan Standard Time" },
        {  5,  "Pakistan Standard Time" },
        {  6,  "Central Asia Standard Time" },
        {  7,  "SE Asia Standard Time" },
        {  8,  "China Standard Time" },
        {  9,  "Tokyo Standard Time" },
        { 10,  "AUS Eastern Standard Time" },
        { 11,  "Central Pacific Standard Time" },
        { 12,  "New Zealand Standard Time" },
        { 13,  "Tonga Standard Time" }
    };

    public static List<SelectListItem> GetTimeZones()
    {
        var timeZones = new List<SelectListItem>();

        foreach (var kvp in PreferredTimeZones)
        {
            TimeZoneInfo tz = null;
            try
            {
                // Attempt to get the system time zone.
                tz = TimeZoneInfo.FindSystemTimeZoneById(kvp.Value);
            }
            catch (TimeZoneNotFoundException)
            {
                // If not found, create a custom time zone based on the offset.
                TimeSpan offset = TimeSpan.FromHours(kvp.Key);
                string customTimeZoneId = $"Custom_{offset}";
                tz = TimeZoneInfo.CreateCustomTimeZone(
                    customTimeZoneId,
                    offset,
                    $"Custom TimeZone (UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:00})",
                    customTimeZoneId);
            }
            timeZones.Add(new SelectListItem { Value = tz.Id, Text = tz.DisplayName });
        }

        return timeZones;
    }
}
