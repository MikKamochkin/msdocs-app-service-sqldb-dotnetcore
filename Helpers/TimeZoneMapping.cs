using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using TimeZoneConverter;

public static class TimeZoneMapping
{
    // Mapping for preferred time zones (offset hour -> user-friendly name)
    private static readonly Dictionary<string, string> PreferredTimeZones = new()
    {
        { "Dateline Standard Time",       "UTC-11" }, // UTC−12
        { "UTC-11",                       "UTC-10" },  // UTC−11
        { "Hawaiian Standard Time",       "UTC-9" },                         // UTC−10
        { "Alaskan Standard Time",        "UTC-8" },                        // UTC−9
        { "Pacific Standard Time",        "Vancouver Los Angeles" },                     // UTC−8
        { "Mountain Standard Time",       "Calgary" },                         // UTC−7
        { "Central Standard Time",        "Chicago" },                         // UTC−6
        { "Eastern Standard Time",        "Toronto / New-York" },                         // UTC−5
        { "Atlantic Standard Time",       "Halifax" },                        // UTC−4
        { "UTC-02",                       "UTC−2" },  // UTC−2
        { "Azores Standard Time",         "UTC−1" },                    // UTC−1
        { "UTC",                          "London" },                                            // UTC±0
        { "Romance Standard Time",        "Paris" },                  // UTC+1
        { "E. Europe Standard Time",      "Belgrade" },               // UTC+2
        { "Russian Standard Time",        "Moscow / St. Petersburg / Kyiv" },                   // UTC+3
        { "Arabian Standard Time",        "UTC+4" },                     // UTC+4
        { "Pakistan Standard Time",       "UTC+5" },                        // UTC+5
        { "Central Asia Standard Time",   "UTC+6" },                     // UTC+6
        { "SE Asia Standard Time",        "UTC+7" },                       // UTC+7
        { "China Standard Time",          "UTC+8" },                           // UTC+8
        { "Tokyo Standard Time",          "UTC+9" },                             // UTC+9
        { "AUS Eastern Standard Time",    "UTC+10" },                // UTC+10
        { "Magadan Standard Time",        "UTC+11" },                         // UTC+11
        { "New Zealand Standard Time",    "UTC+12" },                    // UTC+12
        { "Tonga Standard Time",          "UTC+13" },                        // UTC+13
        { "Line Islands Standard Time",   "UTC+14" }                  // UTC+14
    };


    public static List<SelectListItem> GetTimeZones()
    {
        var items = new List<SelectListItem>();

        foreach (var kvp in PreferredTimeZones)
        {
            var windowsId = kvp.Key;
            var display   = kvp.Value;

            // Convert Windows ID to IANA
            string ianaId = TZConvert.WindowsToIana(windowsId);

            items.Add(new SelectListItem {
                Value = ianaId,
                Text  = display
            });
        }

        return items;
    }
}
