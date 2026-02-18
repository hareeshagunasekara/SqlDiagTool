namespace SqlDiagTool.Reporting;

// Shared utility for file operations.
public static class FileHelper
{
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Where(c => !invalid.Contains(c)).ToArray();
        return new string(chars).Trim();
    }
}
