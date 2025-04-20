using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

public static class DropdownOptions
{
    public static List<SelectListItem> ContactTypes => new List<SelectListItem>
    {
        new SelectListItem { Text = "Email", Value = "Email" },
        new SelectListItem { Text = "Phone", Value = "Phone" },
        new SelectListItem { Text = "Facebook", Value = "Facebook" },
        new SelectListItem { Text = "Instagram", Value = "Instagram" },
        new SelectListItem { Text = "Telegram", Value = "Telegram" },
        new SelectListItem { Text = "Twitter", Value = "Twitter" }
    };

    public static List<SelectListItem> SourceTypes => new List<SelectListItem>
    {
        new SelectListItem { Text = "Google Khinich School", Value = "Google Khinich School" },
        new SelectListItem { Text = "Google Toronto French", Value = "Google Toronto French" },
        new SelectListItem { Text = "Facebok Khinich School", Value = "Facebok Khinich School" },
        new SelectListItem { Text = "Facebook Toronto French", Value = "Facebook Toronto French" },
        new SelectListItem { Text = "Instagram Khinich School", Value = "Instagram Khinich School" },
        new SelectListItem { Text = "Instagram Toronto French", Value = "Instagram Toronto French" },
        new SelectListItem { Text = "Referall from Toronto French", Value = "Referall from Toronto French" },
        new SelectListItem { Text = "Referall from Khinich School", Value = "Referall from Khinich School" },
        new SelectListItem { Text = "DM in Whatsapp Toronto French", Value = "DM in Whatsapp Toronto French" },
        new SelectListItem { Text = "DM in Whatsapp Khinich School", Value = "DM in Whatsapp Khinich School" },
        new SelectListItem { Text = "Phone Call from Toronto French Site", Value = "Phone Call from Toronto French Site" },
        new SelectListItem { Text = "Phone Call from Toronto French by Location", Value = "Phone Call from Toronto French by Location" }
    };

    public static List<SelectListItem> AccountingGroupTypes => new List<SelectListItem>
    {
        new SelectListItem { Text = "A", Value = "A" },
        new SelectListItem { Text = "S", Value = "S" }
    };

    public static List<SelectListItem> PayUnitTypes => new List<SelectListItem>
    {
        new SelectListItem {Text = "RUB", Value = "RUB"},
        new SelectListItem {Text = "CAD", Value = "CAD"},
        new SelectListItem {Text = "EUR", Value = "EUR"},
        new SelectListItem {Text = "USD", Value = "USD"}  
    };

    public static List<SelectListItem> ScheduleStatusTypes => new List<SelectListItem>
    {
        new SelectListItem { Text = "Planned", Value = "Planned" },
        new SelectListItem { Text = "Happened", Value = "Happened" },
        new SelectListItem { Text = "Cancelled", Value = "Cancelled" },
        new SelectListItem { Text = "Deleted", Value = "Deleted" }
    };

    public static List<SelectListItem> LessonDurationTypes => new List<SelectListItem>
    {
        new SelectListItem { Text = "15 minutes", Value = "15" },
        new SelectListItem { Text = "30 minutes", Value = "30" },
        new SelectListItem { Text = "45 minutes", Value = "45" },
        new SelectListItem { Text = "60 minutes", Value = "60" },
        new SelectListItem { Text = "90 minutes", Value = "90" },
        new SelectListItem { Text = "120 minutes", Value = "120" }
    };

}
