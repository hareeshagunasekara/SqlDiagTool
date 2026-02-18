using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlDiagTool.Configuration;
using SqlDiagTool.Reporting;
using SqlDiagTool.Runner;
using SqlDiagTool.Shared;
using SqlDiagTool.Web.Services;
using SqlDiagTool;

namespace SqlDiagTool.Web.Pages;

public class IndexModel : PageModel
{
    private readonly DatabaseTargetService _targetService;
    private readonly DiagnosticsRunner _runner;
    private readonly IWebHostEnvironment _env;
    private readonly SqlDiagOptions _options;

    public IndexModel(DatabaseTargetService targetService, DiagnosticsRunner runner, IWebHostEnvironment env, IOptions<SqlDiagOptions> options)
    {
        _targetService = targetService;
        _runner = runner;
        _env = env;
        _options = options.Value;
    }

    public List<SelectListItem> DatabaseTargets { get; set; } = new();
    public List<TestResult> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public ScanReport? ScanReport { get; set; }
    public string? LastReportPath { get; set; }

    [BindProperty]
    public string? SelectedTargetId { get; set; }

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
        PopulateDropdown(); // so dropdown is repopulated when we re-display after validation or error

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
            var list = await _runner.RunAllAsync(connectionString);
            Results = list.ToList();

            if (Results.Count > 0)
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = builder.InitialCatalog ?? "";
                var server = builder.DataSource;
                var report = CategorizedReportBuilder.Build(Results, databaseName, server, DateTime.UtcNow);
                ScanReport = report;

                var reportsDir = _options.ReportsDirectory;
                if (string.IsNullOrWhiteSpace(reportsDir))
                    reportsDir = Path.Combine(_env.ContentRootPath, "reports");
                var path = JsonReportWriter.BuildReportFilePath(reportsDir, databaseName);
                JsonReportWriter.Write(report, path);
                ReportGenerator.CleanupOldReports(reportsDir, 10);
                LastReportPath = path;
                TempData["LastReportPath"] = path;
                
                // Store report data for PDF generation
                var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                TempData["LastReportJson"] = reportJson;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Scan failed: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnGetDownloadReport()
    {
        var path = TempData["LastReportPath"] as string;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return RedirectToPage("/Index");
        var fileName = Path.GetFileName(path);
        return PhysicalFile(path, "application/json", fileName);
    }

    public IActionResult OnGetDownloadPdfReport()
    {
        var reportJson = TempData["LastReportJson"] as string;
        if (string.IsNullOrEmpty(reportJson))
            return RedirectToPage("/Index");

        try
        {
            var report = JsonSerializer.Deserialize<ScanReport>(reportJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (report == null)
                return RedirectToPage("/Index");

            var pdfBytes = PdfReportGenerator.Generate(report);
            var databaseName = report.Database.Name;
            var sanitized = SanitizeFileName(databaseName);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
            var fileName = string.IsNullOrEmpty(sanitized) ? $"report_{timestamp}.pdf" : $"{sanitized}_{timestamp}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"PDF generation failed: {ex.Message}";
            return RedirectToPage("/Index");
        }
    }

    private static string SanitizeFileName(string name) => SqlDiagTool.Reporting.FileHelper.SanitizeFileName(name);

    private void PopulateDropdown()
    {
        var items = _targetService.GetTargets()
            .Select(t => new SelectListItem(t.DisplayName, t.Id))
            .ToList();
        items.Insert(0, new SelectListItem("Choose database", ""));
        DatabaseTargets = items;
    }
}
