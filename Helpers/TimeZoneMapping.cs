using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

public static class TimeZoneMapping
{
    // Mapping for preferred time zones (offset hour -> user-friendly name)
    public static readonly Dictionary<int, string> PreferredTimeZones = new Dictionary<int, string>
    {
        { -12, "Dateline Standard Time" },
        { -11, "UTC-11" },
        { -10, "Hawaiian Standard Time" },
        { -9,  "Alaskan Standard Time" },
        { -8,  "Pacific Standard Time" },
        { -7,  "US Mountain Standard Time" },
        { -6,  "Central Standard Time" },
        { -5,  "Toronto" },  // <-- Example: just "Toronto"
        { -4,  "Atlantic Standard Time" },
        {  0,  "GMT Standard Time" },
        {  1,  "Central Europe Standard Time (Paris)" },
        {  2,  "E. Europe Standard Time" },
        {  3,  "Russian Standard Time (Moscow, St. Petersburg)" },
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
            var offset = TimeSpan.FromHours(kvp.Key);

            try
            {
                // Attempt to get the system time zone by ID.
                // If kvp.Value is a valid Windows time zone ID, it won't throw.
                tz = TimeZoneInfo.FindSystemTimeZoneById(kvp.Value);
            }
            catch (TimeZoneNotFoundException)
            {
                // If the system time zone isn't found, create a custom one.
                string customTimeZoneId = $"Custom_{offset}";
                tz = TimeZoneInfo.CreateCustomTimeZone(
                    customTimeZoneId,
                    offset,
                    kvp.Value,   // <-- Use your dictionary value as the display name
                    kvp.Value
                );
            }

            // Use the dictionary value (kvp.Value) for the dropdown text
            // so it always matches exactly what you wrote in PreferredTimeZones.
            timeZones.Add(new SelectListItem
            {
                Value = tz.Id,
                Text = kvp.Value
            });
        }

        return timeZones;
    }
}
