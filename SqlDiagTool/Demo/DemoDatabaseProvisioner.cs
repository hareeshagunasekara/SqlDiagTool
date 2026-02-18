using Microsoft.Data.SqlClient;

namespace SqlDiagTool.Demo;

// Ensures demo databases exist on the given server and runs seed scripts so checks have something to diagnose.
public sealed class DemoDatabaseProvisioner
{
    private const int DefaultTimeoutSeconds = 15;

    // Default demo DBs: NoPKs triggers MissingPrimaryKeysCheck WARNING, Clean triggers PASS.
    public static IReadOnlyList<DemoDatabaseSpec> GetDefaultSpecs() => new List<DemoDatabaseSpec>
    {
        new()
        {
            Id = "demo-nopks",
            DisplayName = "Demo – No primary keys",
            DatabaseName = "SqlDiagTool_Demo_NoPKs",
            SeedSql = """
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'NoPkTable')
                CREATE TABLE dbo.NoPkTable (Id int, Name nvarchar(100));
                """
        },
        new()
        {
            Id = "demo-clean",
            DisplayName = "Demo – Clean",
            DatabaseName = "SqlDiagTool_Demo_Clean",
            SeedSql = """
                IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'CleanTable')
                CREATE TABLE dbo.CleanTable (Id int PRIMARY KEY, Name nvarchar(100));
                """
        },
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

    private static async Task RunSeedScriptAsync(string serverConnectionString, DemoDatabaseSpec spec, int timeoutSeconds)
    {
        var effectiveTimeout = spec.SeedTimeoutSeconds > 0 ? spec.SeedTimeoutSeconds : timeoutSeconds;
        var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = spec.DatabaseName };
        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(spec.SeedSql, conn) { CommandTimeout = effectiveTimeout };
        await cmd.ExecuteNonQueryAsync();
    }
}
