using System.Security.Cryptography;
using System.Text;
using BinomoBackend.Application.Interfaces;
using Konscious.Security.Cryptography;

namespace BinomoBackend.Infrastructure.Services;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemorySize = 65536;
    private const int Parallelism = 4;

    public string HashPassword(string password)
    {
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };

        var hash = argon2.GetBytes(HashSize);
        var hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            var hashBytes = Convert.FromBase64String(hash);
            var salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = Parallelism,
                MemorySize = MemorySize,
                Iterations = Iterations
            };

            var computedHash = argon2.GetBytes(HashSize);
            return CryptographicOperations.FixedTimeEquals(
                computedHash, 
                hashBytes.AsSpan(SaltSize, HashSize)
            );
        }
        catch
        {
            return false;
        }
    }
}