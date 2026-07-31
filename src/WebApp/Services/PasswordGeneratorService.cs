using System.Security.Cryptography;
using System.Text;

namespace WebApp.Services;

/// <summary>
/// Deterministically derives a per-site password from a master password and a site hostname.
/// </summary>
public class PasswordGeneratorService
{
    private const string LowercaseCharset = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitsCharset = "0123456789";
    private const string UppercaseCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string SymbolsCharset = "!@#$%^&*()-_=+[]{}";

    /// <summary>
    /// Normalizes a raw site URL down to its hostname, stripping protocol, path and query string.
    /// Returns an empty string when the input is empty or cannot be parsed into a hostname.
    /// </summary>
    public string NormalizeHostname(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return string.Empty;
        }

        var candidate = rawUrl.Trim();

        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return uri.Host.ToLowerInvariant();
        }

        return string.Empty;
    }

    /// <summary>
    /// Generates a deterministic, human-readable password from the master password and hostname.
    /// </summary>
    public string GeneratePassword(
        string masterPassword,
        string hostname,
        PasswordHashAlgorithm algorithm,
        int length,
        PasswordCharsetOptions options)
    {
        if (string.IsNullOrEmpty(masterPassword) || string.IsNullOrEmpty(hostname))
        {
            return string.Empty;
        }

        var charset = BuildCharset(options);
        if (charset.Length == 0)
        {
            return string.Empty;
        }

        length = Math.Clamp(length, 4, 128);

        var key = Encoding.UTF8.GetBytes(masterPassword);
        var message = Encoding.UTF8.GetBytes(hostname);
        var keyStream = DeriveKeyStream(algorithm, key, message, length);

        var result = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var index = keyStream[i] % charset.Length;
            result.Append(charset[index]);
        }

        return result.ToString();
    }

    private static string BuildCharset(PasswordCharsetOptions options)
    {
        var builder = new StringBuilder();

        if (options.HasFlag(PasswordCharsetOptions.Lowercase))
        {
            builder.Append(LowercaseCharset);
        }

        if (options.HasFlag(PasswordCharsetOptions.Digits))
        {
            builder.Append(DigitsCharset);
        }

        if (options.HasFlag(PasswordCharsetOptions.Uppercase))
        {
            builder.Append(UppercaseCharset);
        }

        if (options.HasFlag(PasswordCharsetOptions.Symbols))
        {
            builder.Append(SymbolsCharset);
        }

        return builder.ToString();
    }

    private static byte[] DeriveKeyStream(PasswordHashAlgorithm algorithm, byte[] key, byte[] message, int neededBytes)
    {
        using var hmac = CreateHmac(algorithm, key);

        var stream = new List<byte>(neededBytes + hmac.HashSize / 8);
        var counter = 0;
        while (stream.Count < neededBytes)
        {
            var input = new byte[message.Length + sizeof(int)];
            Buffer.BlockCopy(message, 0, input, 0, message.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(counter), 0, input, message.Length, sizeof(int));

            stream.AddRange(hmac.ComputeHash(input));
            counter++;
        }

        return stream.GetRange(0, neededBytes).ToArray();
    }

    private static HMAC CreateHmac(PasswordHashAlgorithm algorithm, byte[] key) => algorithm switch
    {
        PasswordHashAlgorithm.MD5 => new HMACMD5(key),
        PasswordHashAlgorithm.SHA1 => new HMACSHA1(key),
        PasswordHashAlgorithm.SHA256 => new HMACSHA256(key),
        PasswordHashAlgorithm.SHA384 => new HMACSHA384(key),
        PasswordHashAlgorithm.SHA512 => new HMACSHA512(key),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };
}
