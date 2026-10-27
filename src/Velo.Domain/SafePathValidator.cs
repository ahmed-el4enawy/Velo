using System.Text.RegularExpressions;

namespace Velo.Domain;

/// <summary>
/// Strictly enforces safe filename generation and path sanitization according to PRD v2.0 Section 8.3.
/// Prevents path traversal, absolute path injection, and Windows reserved DOS device name exploits.
/// </summary>
public static partial class SafePathValidator
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]", RegexOptions.Compiled)]
    private static partial Regex InvalidFileNameCharsRegex();

    public static string SanitizeFileName(string rawFileName, string defaultFallback = "download.bin")
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
            return defaultFallback;

        // Strip path traversal characters
        string sanitized = Path.GetFileName(rawFileName.Trim());

        // Replace illegal Windows characters
        sanitized = InvalidFileNameCharsRegex().Replace(sanitized, "_");

        // Trim trailing dots and spaces (illegal in Windows NTFS)
        sanitized = sanitized.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            return defaultFallback;

        // Check for reserved DOS device names (e.g. CON, NUL, COM1, etc.)
        string baseName = Path.GetFileNameWithoutExtension(sanitized);
        if (ReservedDeviceNames.Contains(baseName))
        {
            sanitized = "_" + sanitized;
        }

        // Clamp length to 255 chars
        if (sanitized.Length > 255)
        {
            string ext = Path.GetExtension(sanitized);
            int maxBaseLen = 255 - ext.Length;
            sanitized = sanitized[..Math.Max(1, maxBaseLen)] + ext;
        }

        return sanitized;
    }
}
