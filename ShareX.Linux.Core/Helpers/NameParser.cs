using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ShareX.Linux.Core.Helpers;

public static class NameParser
{
    public static string GenerateFileName(string pattern, string extension = "png")
    {
        var now = DateTime.Now;
        var result = pattern;

        // Date & Time tokens
        result = result.Replace("%y", now.ToString("yyyy"));
        result = result.Replace("%yy", now.ToString("yy"));
        result = result.Replace("%mo", now.ToString("MM"));
        result = result.Replace("%mon", now.ToString("MMMM"));
        result = result.Replace("%d", now.ToString("dd"));
        result = result.Replace("%h", now.ToString("HH"));
        result = result.Replace("%mi", now.ToString("mm"));
        result = result.Replace("%s", now.ToString("ss"));
        result = result.Replace("%ms", now.ToString("fff"));
        result = result.Replace("%t", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        // Random string token: %ra{length}
        result = Regex.Replace(result, @"%ra(?:\{(\d+)\})?", m =>
        {
            int len = 8;
            if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out var parsed))
            {
                len = Math.Clamp(parsed, 1, 64);
            }
            return GenerateRandomAlphaNumeric(len);
        });

        // Remove any invalid file name characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(c, '_');
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            result = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        if (!result.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase))
        {
            result += $".{extension}";
        }

        return result;
    }

    private static string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }
        return sb.ToString();
    }
}
