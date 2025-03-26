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
}
