using Microsoft.Data.SqlClient;
using System.Diagnostics;

/// <summary>Cross-DB, linked server, and SQL Agent dependency checks for migration readiness.</summary>
static class DependencyChecks
{
    // ─── CheckCrossDbReferences: Finds three-part names in procs/views/functions ─
    //
    // A three-part name like OtherDb.dbo.TableName means this database depends on
    // another database on the same server. This is a hard migration blocker:
    //   - Can't move one database to Azure SQL if it queries another locally
    //   - Creates hidden coupling between databases
    //   - Breaks if the referenced database is renamed, moved, or dropped
    //
    // Uses sys.sql_expression_dependencies which tracks referenced_database_name
    // for cross-database references. When referenced_database_name is not NULL
    // and differs from the current DB, it's a cross-database dependency.

    public static async Task<TestResult> CheckCrossDbReferences(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    SCHEMA_NAME(o.schema_id)  AS SchemaName,
                    o.name                     AS ObjectName,
                    o.type_desc                AS ObjectType,
                    sed.referenced_database_name  AS ReferencedDatabase,
                    sed.referenced_schema_name    AS ReferencedSchema,
                    sed.referenced_entity_name    AS ReferencedObject
                FROM sys.sql_expression_dependencies sed
                JOIN sys.objects o ON sed.referencing_id = o.object_id
                WHERE
                    -- Cross-database: referenced_database_name is set and differs from current DB
                    sed.referenced_database_name IS NOT NULL
                    AND sed.referenced_database_name <> DB_NAME()

                    -- Skip system objects
                    AND o.is_ms_shipped = 0
                    AND SCHEMA_NAME(o.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY sed.referenced_database_name, SCHEMA_NAME(o.schema_id), o.name
                """, conn);
            cmd.CommandTimeout = 10;

            var refs = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var obj = $"{reader["SchemaName"]}.{reader["ObjectName"]}";
                var objType = FormatObjectType(reader["ObjectType"].ToString()!);
                var refDb = reader["ReferencedDatabase"];
                var refSchema = reader["ReferencedSchema"] is DBNull ? "?" : reader["ReferencedSchema"];
                var refObj = reader["ReferencedObject"];
                refs.Add($"{obj} ({objType}) → {refDb}.{refSchema}.{refObj}");
            }

            sw.Stop();

            if (refs.Count == 0)
                return new TestResult("Cross-Database References", Status.PASS,
                    "No cross-database references found — this database is self-contained",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", refs.Take(15));
            var more = refs.Count > 15 ? $"\n           ... and {refs.Count - 15} more" : "";
            return new TestResult("Cross-Database References", Status.WARNING,
                $"Found {refs.Count} cross-database reference(s) — migration blockers:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Cross-Database References", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── CheckLinkedServerUsage: Finds four-part names and OPENQUERY/OPENROWSET ──
    //
    // Linked servers allow querying remote servers via four-part names
    // (Server.Database.Schema.Table) or OPENQUERY/OPENROWSET/OPENDATASOURCE.
    //   - Linked servers do NOT exist in Azure SQL Database
    //   - Must be replaced with APIs, ETL pipelines, or Azure SQL Managed Instance
    //   - OPENROWSET/OPENDATASOURCE are blocked in Azure SQL DB by default
    //
    // Scans sys.sql_modules definitions for these patterns:
    //   1. OPENQUERY(   — distributed query to linked server
    //   2. OPENROWSET(  — ad-hoc distributed query
    //   3. OPENDATASOURCE( — ad-hoc OLE DB connection
    //   4. Four-part names via sys.sql_expression_dependencies (referenced_server_name IS NOT NULL)

    public static async Task<TestResult> CheckLinkedServerUsage(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var hits = new List<string>();

            //  Check sys.sql_expression_dependencies for four-part names
            //         (referenced_server_name IS NOT NULL means a linked server is used)
            await using (var cmd = new SqlCommand("""
                SELECT
                    SCHEMA_NAME(o.schema_id)  AS SchemaName,
                    o.name                     AS ObjectName,
                    o.type_desc                AS ObjectType,
                    sed.referenced_server_name    AS LinkedServer,
                    sed.referenced_database_name  AS ReferencedDatabase,
                    sed.referenced_schema_name    AS ReferencedSchema,
                    sed.referenced_entity_name    AS ReferencedObject
                FROM sys.sql_expression_dependencies sed
                JOIN sys.objects o ON sed.referencing_id = o.object_id
                WHERE
                    sed.referenced_server_name IS NOT NULL
                    AND o.is_ms_shipped = 0
                    AND SCHEMA_NAME(o.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                ORDER BY sed.referenced_server_name, SCHEMA_NAME(o.schema_id), o.name
                """, conn))
            {
                cmd.CommandTimeout = 10;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var obj = $"{reader["SchemaName"]}.{reader["ObjectName"]}";
                    var objType = FormatObjectType(reader["ObjectType"].ToString()!);
                    var server = reader["LinkedServer"];
                    var refDb = reader["ReferencedDatabase"] is DBNull ? "?" : reader["ReferencedDatabase"];
                    var refObj = reader["ReferencedObject"];
                    hits.Add($"{obj} ({objType}) → [{server}].{refDb}..{refObj} (four-part name)");
                }
            }

            // Scan definitions for OPENQUERY, OPENROWSET, OPENDATASOURCE
            await using (var cmd = new SqlCommand("""
                SELECT
                    SCHEMA_NAME(o.schema_id) AS SchemaName,
                    o.name                    AS ObjectName,
                    o.type_desc               AS ObjectType,
                    m.definition              AS ObjectDefinition
                FROM sys.sql_modules m
                JOIN sys.objects o ON m.object_id = o.object_id
                WHERE
                    o.is_ms_shipped = 0
                    AND SCHEMA_NAME(o.schema_id) NOT IN ('sys', 'INFORMATION_SCHEMA')
                    AND (
                        m.definition LIKE '%OPENQUERY%'
                        OR m.definition LIKE '%OPENROWSET%'
                        OR m.definition LIKE '%OPENDATASOURCE%'
                    )
                ORDER BY SCHEMA_NAME(o.schema_id), o.name
                """, conn))
            {
                cmd.CommandTimeout = 10;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var obj = $"{reader["SchemaName"]}.{reader["ObjectName"]}";
                    var objType = FormatObjectType(reader["ObjectType"].ToString()!);
                    var def = reader["ObjectDefinition"]?.ToString()?.ToUpperInvariant() ?? "";

                    var patterns = new List<string>();
                    if (def.Contains("OPENQUERY"))      patterns.Add("OPENQUERY");
                    if (def.Contains("OPENROWSET"))     patterns.Add("OPENROWSET");
                    if (def.Contains("OPENDATASOURCE")) patterns.Add("OPENDATASOURCE");

                    hits.Add($"{obj} ({objType}) — uses {string.Join(", ", patterns)}");
                }
            }

            sw.Stop();

            if (hits.Count == 0)
                return new TestResult("Linked Server Usage", Status.PASS,
                    "No linked server references found (no four-part names, OPENQUERY, OPENROWSET, or OPENDATASOURCE)",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", hits.Take(15));
            var more = hits.Count > 15 ? $"\n           ... and {hits.Count - 15} more" : "";
            return new TestResult("Linked Server Usage", Status.WARNING,
                $"Found {hits.Count} linked server reference(s) — not supported in Azure SQL DB:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("Linked Server Usage", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Finds SQL Agent jobs (msdb) that reference this DB; no Agent in Azure SQL DB.</summary>
    public static async Task<TestResult> CheckSqlAgentJobDependencies(string connStr)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var dbName = conn.Database;

            await using var cmd = new SqlCommand("""
                SELECT
                    j.name          AS JobName,
                    js.step_id      AS StepId,
                    js.step_name    AS StepName,
                    js.database_name AS StepDatabase,
                    js.subsystem    AS Subsystem,
                    CASE
                        WHEN j.enabled = 1 THEN 'ENABLED'
                        ELSE 'DISABLED'
                    END AS JobStatus
                FROM msdb.dbo.sysjobs j
                JOIN msdb.dbo.sysjobsteps js ON j.job_id = js.job_id
                WHERE
                    js.database_name = @DbName
                    OR js.command LIKE '%' + @DbName + '%'
                ORDER BY j.name, js.step_id
                """, conn);
            cmd.CommandTimeout = 10;
            cmd.Parameters.AddWithValue("@DbName", dbName);

            var jobs = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var jobName = reader["JobName"];
                var stepId = reader["StepId"];
                var stepName = reader["StepName"];
                var stepDb = reader["StepDatabase"];
                var status = reader["JobStatus"];
                jobs.Add($"{jobName} → step {stepId}: {stepName} (db: {stepDb}, {status})");
            }

            sw.Stop();

            if (jobs.Count == 0)
                return new TestResult("SQL Agent Job Dependencies", Status.PASS,
                    $"No SQL Agent jobs reference database [{dbName}]",
                    sw.ElapsedMilliseconds);

            var details = string.Join("\n           ", jobs.Take(15));
            var more = jobs.Count > 15 ? $"\n           ... and {jobs.Count - 15} more" : "";
            return new TestResult("SQL Agent Job Dependencies", Status.WARNING,
                $"Found {jobs.Count} SQL Agent job step(s) referencing [{dbName}]:\n           {details}{more}",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex) when (ex.Number == 208 || ex.Number == 229 || ex.Number == 916)
        {
            // 208/229/916: msdb missing or no access (e.g. Azure SQL DB)
            sw.Stop();
            return new TestResult("SQL Agent Job Dependencies", Status.PASS,
                $"Cannot query msdb ({ex.Number}) — SQL Agent may not be available (normal for Azure SQL DB)",
                sw.ElapsedMilliseconds);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestResult("SQL Agent Job Dependencies", Status.FAIL,
                $"Query failed | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    // ─── Helper: Formats sys.objects.type_desc into a short friendly name ─────

    private static string FormatObjectType(string typeDesc) => typeDesc switch
    {
        "SQL_STORED_PROCEDURE"      => "Proc",
        "SQL_TRIGGER"               => "Trigger",
        "SQL_SCALAR_FUNCTION"       => "Function",
        "SQL_TABLE_VALUED_FUNCTION" => "TVF",
        "SQL_INLINE_TABLE_VALUED_FUNCTION" => "Inline TVF",
        "VIEW"                      => "View",
        _                           => typeDesc
    };
}
