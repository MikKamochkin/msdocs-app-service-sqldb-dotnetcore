using System.Security.Cryptography;
using System.Text;

public static class PasswordHelper
{
    private const int SaltSize    = 16;     // 128 bit
    private const int KeySize     = 32;     // 256 bit
    private const int Iterations  = 100000; 

    public static void CreatePasswordHash(
        string password,
        out byte[] passwordHash,
        out byte[] passwordSalt)
    {
        using var rng = RandomNumberGenerator.Create();
        passwordSalt = new byte[SaltSize];
        rng.GetBytes(passwordSalt);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, 
            passwordSalt, 
            Iterations, 
            HashAlgorithmName.SHA256);

        passwordHash = pbkdf2.GetBytes(KeySize);
    }

    public static bool VerifyPassword(
        string password,
        byte[] storedHash,
        byte[] storedSalt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, 
            storedSalt, 
            Iterations, 
            HashAlgorithmName.SHA256);

        var computed = pbkdf2.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }
}
