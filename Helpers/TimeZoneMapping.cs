using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using TimeZoneConverter;

public static class TimeZoneMapping
{
    // Mapping for preferred time zones (offset hour -> user-friendly name)
    private static readonly Dictionary<string, string> PreferredTimeZones = new()
    {
        { "Dateline Standard Time",       "Baker Island" }, // UTC−12
        { "UTC-11",                       "Pago Pago" },  // UTC−11
        { "Hawaiian Standard Time",       "Honolulu" },                         // UTC−10
        { "Alaskan Standard Time",        "Anchorage" },                        // UTC−9
        // BC, Alberta and Manitoba dropped clock changes in 2026, so they no longer match any
        // Windows zone. Etc/GMT+N means UTC-N (the sign is inverted) and doesn't depend on the
        // server or browser having 2026 tz data. Times before each province's switch can show 1h off.
        { "Etc/GMT+7",                    "Vancouver" },                       // UTC−7 year-round
        { "Pacific Standard Time",        "Los Angeles" },                     // UTC−8
        { "Etc/GMT+6",                    "Calgary" },                         // UTC−6 year-round
        { "Mountain Standard Time",       "Denver" },                          // UTC−7
        { "Etc/GMT+5",                    "Winnipeg" },                        // UTC−5 year-round
        { "Central Standard Time",        "Chicago" },                         // UTC−6
        { "Eastern Standard Time",        "Toronto / New-York" },                         // UTC−5
        { "Atlantic Standard Time",       "Halifax" },                        // UTC−4
        { "UTC-02",                       "Fernando de Noronha" },  // UTC−2
        { "Azores Standard Time",         "Ponta Delgada" },                    // UTC−1
        { "GMT Standard Time",            "London" },                                            // UTC±0
        { "Romance Standard Time",        "Paris" },                  // UTC+1
        { "Central Europe Standard Time", "Belgrade" },               // UTC+1
        { "Russian Standard Time",        "Moscow" },
        { "FLE Standard Time",            "Kyiv" },                   // UTC+2 / UTC+3 in summer
        { "Arabian Standard Time",        "Dubai" },                     // UTC+4
        { "Pakistan Standard Time",       "Karachi" },                        // UTC+5
        { "Central Asia Standard Time",   "Dhaka" },                     // UTC+6
        { "SE Asia Standard Time",        "Bangkok" },                       // UTC+7
        { "China Standard Time",          "Beijing" },                           // UTC+8
        { "Tokyo Standard Time",          "Tokyo" },                             // UTC+9
        { "AUS Eastern Standard Time",    "Sydney" },                // UTC+10
        { "Magadan Standard Time",        "Magadan" },                         // UTC+11
        { "New Zealand Standard Time",    "Auckland" },                    // UTC+12
        { "Tonga Standard Time",          "Nuku'alofa" },                        // UTC+13
        { "Line Islands Standard Time",   "Kiritimati" }                  // UTC+14
    };


    public static List<SelectListItem> GetTimeZones()
    {
        var items = new List<SelectListItem>();

        foreach (var kvp in PreferredTimeZones)
        {
            var windowsId = kvp.Key;
            var display   = kvp.Value;

            // Convert Windows ID to IANA (Etc/ entries are already IANA)
            string ianaId = windowsId.StartsWith("Etc/") ? windowsId : TZConvert.WindowsToIana(windowsId);

            items.Add(new SelectListItem {
                Value = ianaId,
                Text  = display
            });
        }

        return items;
    }

    // Moves a UTC time by whole days, keeping the same wall-clock time in the given zone across clock changes
    public static DateTime AddDaysKeepingLocalTime(DateTime utc, int days, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone).AddDays(days);

        // Time skipped by a spring-forward: use the hour after it
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
