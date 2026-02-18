using System.Text.Json;

namespace SqlDiagTool.Reporting;

// Writes ScanReport to a JSON file; creates directory if needed.
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(ScanReport report, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(report, Options);
        File.WriteAllText(filePath, json);
    }

    // Builds path: reportsDirectory / Sanitize(databaseName)_yyyy-MM-ddTHH-mm-ss.json
    public static string BuildReportFilePath(string? reportsDirectory, string? databaseName)
    {
        var dir = string.IsNullOrWhiteSpace(reportsDirectory) ? "." : reportsDirectory;
        var sanitized = SanitizeFileName(databaseName ?? "");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
        var fileName = string.IsNullOrEmpty(sanitized) ? timestamp : $"{sanitized}_{timestamp}";
        return Path.Combine(dir, $"{fileName}.json");
    }

    private static string SanitizeFileName(string name) => FileHelper.SanitizeFileName(name);
}
