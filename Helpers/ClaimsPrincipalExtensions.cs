using System.Security.Claims;

namespace DotNetCoreSqlDb.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool IsImpersonating(this ClaimsPrincipal user) =>
            user?.FindFirst("Impersonating")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }
}
