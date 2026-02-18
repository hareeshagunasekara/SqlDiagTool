namespace SqlDiagTool;

// Utility for report file management.
public static class ReportGenerator
{
    public static void CleanupOldReports(string reportDir, int keepCount = 5)
    {
        if (!Directory.Exists(reportDir) || keepCount <= 0) return;
        var files = Directory.GetFiles(reportDir).Select(f => new FileInfo(f)).OrderByDescending(fi => fi.LastWriteTimeUtc).ToList();
        foreach (var f in files.Skip(keepCount))
            try { f.Delete(); } catch { /* ignore */ }
    }
}
