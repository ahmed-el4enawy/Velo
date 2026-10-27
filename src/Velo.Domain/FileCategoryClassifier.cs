namespace Velo.Domain;

/// <summary>
/// Classifies downloads into standard categories based on file extension and MIME type (PRD v2.0 Section 3 FR-16).
/// Automatically resolves categorized target directory structures.
/// </summary>
public static class FileCategoryClassifier
{
    private static readonly Dictionary<string, CategoryType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Compressed / Archives
        { ".zip", CategoryType.Compressed },
        { ".rar", CategoryType.Compressed },
        { ".7z", CategoryType.Compressed },
        { ".tar", CategoryType.Compressed },
        { ".gz", CategoryType.Compressed },
        { ".bz2", CategoryType.Compressed },
        { ".xz", CategoryType.Compressed },
        { ".iso", CategoryType.Compressed },
        { ".img", CategoryType.Compressed },
        { ".vhd", CategoryType.Compressed },
        { ".vhdx", CategoryType.Compressed },

        // Video
        { ".mp4", CategoryType.Video },
        { ".mkv", CategoryType.Video },
        { ".avi", CategoryType.Video },
        { ".mov", CategoryType.Video },
        { ".wmv", CategoryType.Video },
        { ".flv", CategoryType.Video },
        { ".webm", CategoryType.Video },
        { ".m4v", CategoryType.Video },
        { ".ts", CategoryType.Video },

        // Music / Audio
        { ".mp3", CategoryType.Music },
        { ".flac", CategoryType.Music },
        { ".wav", CategoryType.Music },
        { ".aac", CategoryType.Music },
        { ".ogg", CategoryType.Music },
        { ".wma", CategoryType.Music },
        { ".m4a", CategoryType.Music },
        { ".opus", CategoryType.Music },

        // Documents
        { ".pdf", CategoryType.Documents },
        { ".doc", CategoryType.Documents },
        { ".docx", CategoryType.Documents },
        { ".xls", CategoryType.Documents },
        { ".xlsx", CategoryType.Documents },
        { ".ppt", CategoryType.Documents },
        { ".pptx", CategoryType.Documents },
        { ".txt", CategoryType.Documents },
        { ".rtf", CategoryType.Documents },
        { ".epub", CategoryType.Documents },
        { ".md", CategoryType.Documents },

        // Programs / Installers
        { ".exe", CategoryType.Programs },
        { ".msi", CategoryType.Programs },
        { ".apk", CategoryType.Programs },
        { ".bat", CategoryType.Programs },
        { ".cmd", CategoryType.Programs },
        { ".ps1", CategoryType.Programs }
    };

    public static CategoryType Classify(string fileName, string? contentType = null)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            string ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && ExtensionMap.TryGetValue(ext, out var category))
            {
                return category;
            }
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return CategoryType.Video;
            if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return CategoryType.Music;
            if (contentType.StartsWith("application/zip", StringComparison.OrdinalIgnoreCase) ||
                contentType.StartsWith("application/x-rar", StringComparison.OrdinalIgnoreCase) ||
                contentType.StartsWith("application/x-7z", StringComparison.OrdinalIgnoreCase))
                return CategoryType.Compressed;
            if (contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase) ||
                contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return CategoryType.Documents;
        }

        return CategoryType.Others;
    }

    public static string ResolveCategoryFolder(string baseDownloadFolder, CategoryType category)
    {
        if (category == CategoryType.All || category == CategoryType.Others)
        {
            return baseDownloadFolder;
        }

        return Path.Combine(baseDownloadFolder, category.ToString());
    }
}
