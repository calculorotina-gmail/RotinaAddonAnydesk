using System.Security.Cryptography;
using System.Text;
using AnyDeskMonitor.Application.Interfaces;

namespace AnyDeskMonitor.Infrastructure.Authentication;

public class DefaultPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "AnyDeskMonitor_Salt_2026");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        var computed = HashPassword(password);
        return computed == storedHash;
    }
}
