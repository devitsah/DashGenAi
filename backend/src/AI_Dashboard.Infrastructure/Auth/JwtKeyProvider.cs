using System.Security.Cryptography;
using System.Text.Json;

namespace AI_Dashboard.Infrastructure.Auth;

/// Generates a cryptographically random signing key and persists it to a local file
/// outside source control. The key automatically rotates after RotationDays - once
/// rotated, all previously issued tokens become invalid immediately (same effect as
/// a global logout), so users will need to log in again after a rotation.
public static class JwtKeyProvider
{
    private const string KeyFileName = "jwt.key";
    private const int RotationDays = 1; // change this to whatever cadence you want

    private class KeyFileContent
    {
        public string Key { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }

    public static string GetOrCreateKey(string contentRootPath)
    {
        var keyFilePath = Path.Combine(contentRootPath, KeyFileName);

        if (File.Exists(keyFilePath))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<KeyFileContent>(File.ReadAllText(keyFilePath));
                if (existing is not null && !string.IsNullOrWhiteSpace(existing.Key))
                {
                    var age = DateTime.UtcNow - existing.CreatedAt;
                    if (age.TotalDays < RotationDays)
                        return existing.Key; // still valid, reuse it

                    // expired - fall through and generate a fresh one
                }
            }
            catch (JsonException)
            {
                // file exists but isn't valid JSON (e.g. leftover from before this rotation
                // logic existed, when the file was just a raw key string) - treat as expired
            }
        }

        return GenerateAndPersist(keyFilePath);
    }

    private static string GenerateAndPersist(string keyFilePath)
    {
        var keyBytes = new byte[32]; // 256 bits - matches HmacSha256 requirement
        RandomNumberGenerator.Fill(keyBytes);
        var key = Convert.ToBase64String(keyBytes);

        var content = new KeyFileContent { Key = key, CreatedAt = DateTime.UtcNow };
        File.WriteAllText(keyFilePath, JsonSerializer.Serialize(content));

        return key;
    }
}