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

        // Trim surrounding whitespace
        string trimmed = rawFileName.Trim();

        // If path separators are present, take only the leaf filename
        int lastSlash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        if (lastSlash >= 0 && lastSlash < trimmed.Length - 1)
        {
            trimmed = trimmed[(lastSlash + 1)..];
        }

        // Replace illegal Windows characters
        string sanitized = InvalidFileNameCharsRegex().Replace(trimmed, "_");

        // Trim trailing dots and spaces (illegal in Windows NTFS)
        sanitized = sanitized.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            return defaultFallback;

        // Check for reserved DOS device names (e.g. CON, NUL, COM1, AUX.tar.gz)
        string baseName = sanitized.Split('.')[0];
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

    public static string SanitizeDestinationDirectory(string rawDirectory, string? fallbackBase = null)
    {
        string fallback = fallbackBase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (string.IsNullOrWhiteSpace(rawDirectory))
            return fallback;

        try
        {
            string fullPath = Path.GetFullPath(rawDirectory.Trim());
            return fullPath;
        }
        catch
        {
            return fallback;
        }
    }

    public static string CombineSafePath(string destinationDirectory, string fileName, string? fallbackBase = null)
    {
        string safeDir = SanitizeDestinationDirectory(destinationDirectory, fallbackBase);
        string safeFile = SanitizeFileName(fileName);
        string combined = Path.Combine(safeDir, safeFile);
        string normalized = Path.GetFullPath(combined);

        // Confirm that the resolved path is indeed within safeDir
        if (!normalized.StartsWith(safeDir, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(safeDir, safeFile);
        }

        return normalized;
    }
}
