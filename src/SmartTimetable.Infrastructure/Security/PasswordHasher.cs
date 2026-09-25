using System.Security.Cryptography;
using SmartTimetable.Application.Abstractions;

namespace SmartTimetable.Infrastructure.Security;

/// <summary>
/// PBKDF2 (SHA-256) password hashing with a per-user random salt.
/// Hash and salt are stored Base64-encoded.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;

    public (string Hash, string Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        try
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] expected = Convert.FromBase64String(hash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, saltBytes, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }
}
