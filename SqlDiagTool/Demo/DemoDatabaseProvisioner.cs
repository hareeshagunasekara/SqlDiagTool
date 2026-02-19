using Microsoft.Data.SqlClient;

namespace SqlDiagTool.Demo;

// Ensures demo databases exist on the given server and runs seed scripts so checks have something to diagnose.
public sealed class DemoDatabaseProvisioner
{
    private const int DefaultTimeoutSeconds = 15;

    public static IReadOnlyList<DemoDatabaseSpec> GetDefaultSpecs() => new List<DemoDatabaseSpec>
    {
        new()
        {
            Id = "demo-retailops-legacy",
            DisplayName = "Demo – RetailOps Legacy",
            DatabaseName = "RetailOps_Legacy",
            SeedSql = RetailOpsLegacySeed.GetSeedSql(),
            SeedTimeoutSeconds = 60
        },
        new()
        {
            Id = "demo-medtrack-clinical",
            DisplayName = "Demo – MedTrack Clinical",
            DatabaseName = "MedTrack_Clinical",
            SeedSql = MedTrackClinicalSeed.GetSeedSql(),
            SeedTimeoutSeconds = 60
        },
        new()
        {
            Id = "demo-manufacturingops-industrial",
            DisplayName = "Demo – ManufacturingOps Industrial",
            DatabaseName = "ManufacturingOps_Industrial",
            SeedSql = ManufacturingOpsIndustrialSeed.GetSeedSql(),
            SeedTimeoutSeconds = 60
        },
        new()
        {
            Id = "demo-legalcase-db",
            DisplayName = "Demo – LegalCase DB",
            DatabaseName = "LegalCase_Db",
            SeedSql = LegalCaseDbSeed.GetSeedSql(),
            SeedTimeoutSeconds = 60
        }
    };

    public async Task<DemoProvisionResult> EnsureDatabasesAsync(
        string? serverConnectionString,
        IReadOnlyList<DemoDatabaseSpec>? specs = null,
        int commandTimeoutSeconds = DefaultTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(serverConnectionString))
            return DemoProvisionResult.Fail("Demo server connection string is not configured.");

        var list = specs ?? GetDefaultSpecs();
        if (list.Count == 0)
            return DemoProvisionResult.Ok();

        try
        {
            var masterCs = BuildMasterConnectionString(serverConnectionString);
            foreach (var spec in list)
            {
                await EnsureDatabaseExistsAsync(masterCs, spec.DatabaseName, commandTimeoutSeconds);
                await RunSeedScriptAsync(serverConnectionString, spec, commandTimeoutSeconds);
            }
            return DemoProvisionResult.Ok();
        }
        catch (SqlException ex)
        {
            return DemoProvisionResult.Fail($"Demo databases unavailable: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DemoProvisionResult.Fail($"Demo provision failed: {ex.Message}");
        }
    }

    private static string BuildMasterConnectionString(string serverConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }

    private static async Task EnsureDatabaseExistsAsync(string masterConnectionString, string databaseName, int timeoutSeconds)
    {
        // DatabaseName comes from our own specs, not user input.
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'{databaseName.Replace("'", "''")}')
            CREATE DATABASE [{databaseName.Replace("]", "]]")}];
            """;
        await using var conn = new SqlConnection(masterConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
        await cmd.ExecuteNonQueryAsync();
    }

    private const string BatchSeparator = "-- __BATCH__";

    private static async Task RunSeedScriptAsync(string serverConnectionString, DemoDatabaseSpec spec, int timeoutSeconds)
    {
        var effectiveTimeout = spec.SeedTimeoutSeconds > 0 ? spec.SeedTimeoutSeconds : timeoutSeconds;
        var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = spec.DatabaseName };
        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        var batches = spec.SeedSql.Split(BatchSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var batch in batches)
        {
            var sql = batch.Trim();
            if (sql.Length == 0) continue;
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = effectiveTimeout };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
