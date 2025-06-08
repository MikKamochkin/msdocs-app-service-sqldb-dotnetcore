using System.Collections.Concurrent;

namespace DotNetCoreSqlDb.Helpers
{
    public static class PasswordResetTokenStore
    {
        private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expires)> _tokens = new();

        public static string CreateToken(Guid userId, TimeSpan lifetime)
        {
            var token = Guid.NewGuid().ToString();
            var expires = DateTime.UtcNow.Add(lifetime);
            _tokens[token] = (userId, expires);
            return token;
        }

        public static bool TryRedeem(string token, out Guid userId)
        {
            userId = Guid.Empty;
            if (_tokens.TryGetValue(token, out var entry))
            {
                if (entry.Expires > DateTime.UtcNow)
                {
                    userId = entry.UserId;
                    _tokens.TryRemove(token, out _);
                    return true;
                }
                _tokens.TryRemove(token, out _);
            }
            return false;
        }
    }
}