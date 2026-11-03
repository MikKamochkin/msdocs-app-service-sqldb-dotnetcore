using DotNetCoreSqlDb.Models;

namespace DotNetCoreSqlDb.Helpers
{
    // StudentBalance stores one column per currency; these map a currency code to the matching column.
    public static class StudentBalanceHelper
    {
        public static bool IsSupportedCurrency(string? currency)
        {
            switch (currency?.Trim().ToUpperInvariant())
            {
                case "CAD":
                case "USD":
                case "EUR":
                case "RUB":
                    return true;
                default:
                    return false;
            }
        }

        public static float GetBalance(StudentBalance balance, string currency)
        {
            switch (currency.Trim().ToUpperInvariant())
            {
                case "CAD": return balance.CADBalance;
                case "USD": return balance.USDBalance;
                case "EUR": return balance.EURBalance;
                case "RUB": return balance.RUBBalance;
                default: throw new ArgumentException($"Unsupported currency '{currency}'", nameof(currency));
            }
        }

        // Adds amount (negative to subtract) to the balance for that currency and returns the new balance.
        public static float AdjustBalance(StudentBalance balance, string currency, float amount)
        {
            switch (currency.Trim().ToUpperInvariant())
            {
                case "CAD": return balance.CADBalance += amount;
                case "USD": return balance.USDBalance += amount;
                case "EUR": return balance.EURBalance += amount;
                case "RUB": return balance.RUBBalance += amount;
                default: throw new ArgumentException($"Unsupported currency '{currency}'", nameof(currency));
            }
        }
    }
}
