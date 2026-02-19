using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using SqlDiagTool.Checks;
using SqlDiagTool.Reporting;
using SqlDiagTool.Runner;
using SqlDiagTool.Shared;
using SqlDiagTool.Web.Services;

namespace SqlDiagTool.Web.Pages;

public class IndexModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly DatabaseTargetService _targetService;
    private readonly DiagnosticsRunner _runner;
    private readonly IMemoryCache _cache;

    public IndexModel(DatabaseTargetService targetService, DiagnosticsRunner runner, IMemoryCache cache)
    {
        _targetService = targetService;
        _runner = runner;
        _cache = cache;
    }

    public List<SelectListItem> DatabaseTargets { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<TestResult> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public ScanReport? ScanReport { get; set; }
    public string? ReportId { get; set; }

    [BindProperty]
    public string? SelectedTargetId { get; set; }

    [BindProperty]
    public string? SelectedCategory { get; set; }

    public string? SelectedDatabaseName
    {
        get
        {
            if (ScanReport != null && !string.IsNullOrEmpty(ScanReport.Database.Name))
                return ScanReport.Database.Name;

            if (string.IsNullOrWhiteSpace(SelectedTargetId)) return null;

            var targets = _targetService.GetTargets();
            var target = targets.FirstOrDefault(t => string.Equals(t.Id, SelectedTargetId, StringComparison.OrdinalIgnoreCase));
            return target.Id != null ? target.DisplayName : null;
        }
    }

    public void OnGet()
    {
        PopulateDropdown();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PopulateDropdown();

        if (string.IsNullOrWhiteSpace(SelectedTargetId))
        {
            ModelState.AddModelError(nameof(SelectedTargetId), "Please choose a database.");
            return Page();
        }

        var connectionString = _targetService.GetConnectionString(SelectedTargetId);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ModelState.AddModelError(nameof(SelectedTargetId), "Invalid selection or missing connection string.");
            return Page();
        }

        try
        {
            var categoryFilter = string.IsNullOrWhiteSpace(SelectedCategory) ? null : SelectedCategory;
            var list = await _runner.RunAllAsync(connectionString, categoryFilter);
            Results = list.ToList();

            if (Results.Count > 0)
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = builder.InitialCatalog ?? "";
                var server = builder.DataSource;
                var report = CategorizedReportBuilder.Build(Results, databaseName, server, DateTime.UtcNow);
                ScanReport = report;

                ReportId = Guid.NewGuid().ToString("N");
                _cache.Set(ReportCacheKey(ReportId), report, CacheTtl);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Scan failed: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnGetDownloadReport(string? reportId)
    {
        var report = GetCachedReport(reportId);
        if (report == null)
            return RedirectToPage("/Index");

        var json = JsonSerializer.Serialize(report, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = BuildFileName(report.Database.Name, "json");
        return File(bytes, "application/json", fileName);
    }

    public IActionResult OnGetDownloadPdfReport(string? reportId)
    {
        var report = GetCachedReport(reportId);
        if (report == null)
            return RedirectToPage("/Index");

        try
        {
            var pdfBytes = PdfReportGenerator.Generate(report);
            var fileName = BuildFileName(report.Database.Name, "pdf");
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"PDF generation failed: {ex.Message}";
            PopulateDropdown();
            return Page();
        }
    }

    private ScanReport? GetCachedReport(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return null;
        _cache.TryGetValue(ReportCacheKey(reportId), out ScanReport? report);
        return report;
    }

    private static string ReportCacheKey(string id) => $"scan-report:{id}";

    private static string BuildFileName(string? databaseName, string extension)
    {
        var sanitized = FileHelper.SanitizeFileName(databaseName ?? "");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
        var baseName = string.IsNullOrEmpty(sanitized) ? $"report_{timestamp}" : $"{sanitized}_{timestamp}";
        return $"{baseName}.{extension}";
    }

    private void PopulateDropdown()
    {
        DatabaseTargets = _targetService.GetTargets()
            .Select(t => new SelectListItem(t.DisplayName, t.Id))
            .Prepend(new SelectListItem("Choose database", ""))
            .ToList();

        CategoryOptions = CheckRegistry.Categories
            .Select(c => new SelectListItem(ReportDisplayNames.GetCategoryDisplayName(c), c))
            .Prepend(new SelectListItem("All categories", ""))
            .ToList();
    }
}
