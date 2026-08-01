using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    // Work factor 12 = 2^12 = 4096 iterations. 10-12 is the standard recommended
    // range as of 2026 - high enough to resist GPU cracking, low enough to not
    // noticeably slow down login (roughly 200-300ms per hash on typical hardware).
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}