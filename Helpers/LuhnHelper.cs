using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetCoreSqlDb.Data;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;               // for SignOutAsync(...)
using Microsoft.AspNetCore.Authentication.Cookies;    // <-- Adjust if your DbContext lives in a different namespace
using DotNetCoreSqlDb.Models;


namespace DotNetCoreSqlDb.Helpers
{
    public class LuhnHelper
    {

        public LuhnHelper()
        {

        }

        public static int ComputeLuhnCheckDigit(string numericBody)
        {
            int sum = 0;
            bool doubleIt = true; // start doubling from rightmost digit of body
            for (int i = numericBody.Length - 1; i >= 0; i--)
            {
                int d = numericBody[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }
            int check = (10 - (sum % 10)) % 10;
            return check;
        }

        public static bool IsLuhnValid(string numericWithCheckDigit)
        {
            int sum = 0;
            bool doubleIt = false; // include check digit this time
            for (int i = numericWithCheckDigit.Length - 1; i >= 0; i--)
            {
                int d = numericWithCheckDigit[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }
            return (sum % 10) == 0;
        }

    }
}
