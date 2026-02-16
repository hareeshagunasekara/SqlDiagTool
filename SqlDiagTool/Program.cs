using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

// ─── Configuration: appsettings.json → appsettings.Development.json → env vars ──

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var sqlConfig = config.GetSection("SqlConnection");

// ─── BuildConnStr: Constructs a connection string, allowing per-test overrides ──

static string BuildConnStr(
    IConfigurationSection cfg,
    string? server   = null,
    string? database = null,
    string? userId   = null,
    string? password = null,
    int? connectTimeout = null)
{
    return $"Server={server ?? cfg["Server"]};" +
           $"Database={database ?? cfg["Database"]};" +
           $"User Id={userId ?? cfg["UserId"]};" +
           $"Password={password ?? cfg["Password"]};" +
           $"TrustServerCertificate={cfg["TrustServerCertificate"] ?? "True"};" +
           $"Connect Timeout={connectTimeout ?? int.Parse(cfg["ConnectTimeout"] ?? "5")};";
}

// ─── Classify: Maps SqlException.Number to a human-readable error category ───

static string Classify(SqlException ex) => ex.Number switch
{
    // Timeouts
    -2            => "TIMEOUT – server slow / blocked / unreachable (firewall drop)",

    // Connection failures (Windows error codes)
    -1            => "CANNOT CONNECT – network error / server unreachable",
    2             => "CANNOT CONNECT – server not found or not accessible",
    53            => "CANNOT CONNECT – server down / wrong host or port",
    40            => "CANNOT CONNECT – could not open connection",
    11001         => "CANNOT CONNECT – DNS lookup failed / host not found",
    10061         => "CANNOT CONNECT – connection refused (wrong port / service down)",

    // TLS / encryption handshake failure (common on macOS with v6.x driver)
    -2146893019   => "TLS/SSL HANDSHAKE FAILED – encrypt mismatch (add Encrypt=Optional on macOS)",

    // Authentication failures (server-side, consistent across platforms)
    18456         => "LOGIN FAILED – wrong username or password",
    18452         => "LOGIN FAILED – login not associated with trusted connection",

    // Database access failures (server-side, consistent across platforms)
    4060          => "CANNOT OPEN DATABASE – database name wrong or no access",
    4064          => "CANNOT OPEN DATABASE – user's default database unavailable",

    // Deadlock (server-side, consistent across platforms)
    1205          => "DEADLOCK – SQL killed your query as victim",

    // macOS fallback: error code 0 with message-based classification
    0 when ex.Message.Contains("network-related", StringComparison.OrdinalIgnoreCase)
                  => "CANNOT CONNECT – network error (server unreachable / wrong host or port)",
    0 when ex.Message.Contains("pre-login handshake", StringComparison.OrdinalIgnoreCase)
                  => "TLS/SSL HANDSHAKE FAILED – check Encrypt setting or driver version",

    // Catch-all for unmapped error codes
    _             => $"OTHER SQL ERROR (Number: {ex.Number})"
};

// ─── RunTest: Opens a connection, executes a query, returns a classified result ──

static async Task<TestResult> RunTest(string testName, string connStr, string sql = "SELECT 1", int cmdTimeoutSeconds = 3)
{
    var sw = Stopwatch.StartNew();
    try
    {
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = cmdTimeoutSeconds;

        var result = await cmd.ExecuteScalarAsync();
        sw.Stop();

        return new TestResult(testName, Status.PASS, $"Result: {result}", sw.ElapsedMilliseconds);
    }
    catch (SqlException ex)
    {
        sw.Stop();
        return new TestResult(testName, Status.FAIL, $"{Classify(ex)} | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return new TestResult(testName, Status.WARNING, $"Non-SQL error: {ex.GetType().Name} – {ex.Message}", sw.ElapsedMilliseconds);
    }
}

// ─── RunDeadlockTest: Triggers a real deadlock using two concurrent transactions ──
//
// How it works:
//   1. Create two tables in tempdb (DeadlockTestA and DeadlockTestB)
//   2. Task A: locks TableA → waits 2s → tries to lock TableB
//   3. Task B: locks TableB → waits 2s → tries to lock TableA
//   4. SQL Server detects the circular wait and kills one as the deadlock victim (error 1205)
//   5. We inspect both tasks to find which one was the victim and return PASS if 1205 was caught

static async Task<TestResult> RunDeadlockTest(string testName, string connStr)
{
    var sw = Stopwatch.StartNew();
    try
    {
        // Create two test tables in tempdb with one row each
        await using (var setup = new SqlConnection(connStr))
        {
            await setup.OpenAsync();
            await using var cmd = new SqlCommand("""
                IF OBJECT_ID('tempdb.dbo.DeadlockTestA') IS NOT NULL DROP TABLE tempdb.dbo.DeadlockTestA;
                IF OBJECT_ID('tempdb.dbo.DeadlockTestB') IS NOT NULL DROP TABLE tempdb.dbo.DeadlockTestB;
                CREATE TABLE tempdb.dbo.DeadlockTestA (Id INT PRIMARY KEY, Val INT);
                CREATE TABLE tempdb.dbo.DeadlockTestB (Id INT PRIMARY KEY, Val INT);
                INSERT INTO tempdb.dbo.DeadlockTestA VALUES (1, 100);
                INSERT INTO tempdb.dbo.DeadlockTestB VALUES (1, 200);
                """, setup);
            await cmd.ExecuteNonQueryAsync();
        }

        // Task A locks TableA first, then tries to lock TableB
        var taskA = Task.Run(async () =>
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("""
                BEGIN TRAN;
                    UPDATE tempdb.dbo.DeadlockTestA SET Val = Val + 1 WHERE Id = 1;
                    WAITFOR DELAY '00:00:02';
                    UPDATE tempdb.dbo.DeadlockTestB SET Val = Val + 1 WHERE Id = 1;
                COMMIT;
                """, conn);
            cmd.CommandTimeout = 15;
            await cmd.ExecuteNonQueryAsync();
        });

        // Task B locks TableB first, then tries to lock TableA (opposite order)
        var taskB = Task.Run(async () =>
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("""
                BEGIN TRAN;
                    UPDATE tempdb.dbo.DeadlockTestB SET Val = Val + 1 WHERE Id = 1;
                    WAITFOR DELAY '00:00:02';
                    UPDATE tempdb.dbo.DeadlockTestA SET Val = Val + 1 WHERE Id = 1;
                COMMIT;
                """, conn);
            cmd.CommandTimeout = 15;
            await cmd.ExecuteNonQueryAsync();
        });

        // Wait for both tasks — one will throw a deadlock exception
        try
        {
            await Task.WhenAll(taskA, taskB);
        }
        catch
        {
            // Expected: await Task.WhenAll rethrows the first faulted task's exception
        }

        // Find the deadlock victim and check for error 1205
        SqlException? deadlockEx = null;
        if (taskA.IsFaulted)
            deadlockEx = taskA.Exception?.InnerExceptions.OfType<SqlException>().FirstOrDefault();
        if (taskB.IsFaulted)
            deadlockEx = taskB.Exception?.InnerExceptions.OfType<SqlException>().FirstOrDefault();

        sw.Stop();

        if (deadlockEx != null && deadlockEx.Number == 1205)
            return new TestResult(testName, Status.PASS,
                $"Deadlock detected correctly | Code: {deadlockEx.Number} | {deadlockEx.Message}", sw.ElapsedMilliseconds);

        if (deadlockEx != null)
            return new TestResult(testName, Status.WARNING,
                $"Got SqlException but not 1205 | Code: {deadlockEx.Number} | {deadlockEx.Message}", sw.ElapsedMilliseconds);

        return new TestResult(testName, Status.WARNING,
            "No deadlock occurred — both tasks completed (race condition, retry may help)", sw.ElapsedMilliseconds);
    }
    catch (SqlException ex)
    {
        sw.Stop();
        return new TestResult(testName, Status.FAIL,
            $"{Classify(ex)} | Code: {ex.Number} | {ex.Message}", sw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return new TestResult(testName, Status.WARNING,
            $"Non-SQL error: {ex.GetType().Name} – {ex.Message}", sw.ElapsedMilliseconds);
    }
    finally
    {
        // Best-effort cleanup: drop the test tables regardless of outcome
        try
        {
            await using var cleanup = new SqlConnection(connStr);
            await cleanup.OpenAsync();
            await using var cmd = new SqlCommand("""
                IF OBJECT_ID('tempdb.dbo.DeadlockTestA') IS NOT NULL DROP TABLE tempdb.dbo.DeadlockTestA;
                IF OBJECT_ID('tempdb.dbo.DeadlockTestB') IS NOT NULL DROP TABLE tempdb.dbo.DeadlockTestB;
                """, cleanup);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* cleanup is best-effort; don't mask the real error */ }
    }
}

// ─── PrintResult: Displays a single test result with pass/fail/warning icon ──

static void PrintResult(TestResult r)
{
    var icon = r.Status switch
    {
        Status.PASS    => "✅ PASS",
        Status.FAIL    => "❌ FAIL",
        Status.WARNING => "⚠️  WARN",
        _              => "❓ UNKNOWN"
    };
    Console.WriteLine($"  {icon}  {r.TestName}  ({r.ElapsedMs}ms)");
    Console.WriteLine($"         {r.Message}");
    Console.WriteLine();
}

// ─── PrintSection: Prints a section header with separator lines ──────────────

static void PrintSection(string title)
{
    Console.WriteLine(new string('─', 60));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Connection & Authentication Diagnostics
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Connection & Authentication Diagnostics");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 1–6 use INTENTIONALLY bad inputs (wrong host, wrong password, etc.)");
Console.WriteLine("     to verify that the tool detects and classifies each failure correctly.");
Console.WriteLine("     ❌ FAIL in these sections means the error was caught as expected.");
Console.WriteLine();

var results = new List<TestResult>();

// Known-good connection string — credentials come from env vars (SQL_SERVER, SQL_USER, SQL_PASSWORD, SQL_DATABASE)
// or fall back to defaults for local Docker-based dev
var goodConnStr = BuildConnStr(sqlConfig);

// ─── Basic Connectivity — Can we reach the server at all? ─────────────────

PrintSection("1. Basic Connectivity — Can we reach the server?");

Console.WriteLine("  ℹ️  Tests a valid host, a nonexistent host, and a wrong port.");
Console.WriteLine("     Expect: 1 PASS, 2 intentional FAILs (network errors).");
Console.WriteLine();

// Correct connection string — should pass 
var r = await RunTest("Baseline (correct connection)", goodConnStr);
PrintResult(r);
results.Add(r);

// Wrong host + wrong port are independent — run in parallel to save ~5s
var wrongHostTask = RunTest(
    "Wrong host",
    BuildConnStr(sqlConfig, server: "doesnotexist.local,1433", connectTimeout: 3));
var wrongPortTask = RunTest(
    "Wrong port",
    BuildConnStr(sqlConfig, server: "localhost,9999", connectTimeout: 3));
await Task.WhenAll(wrongHostTask, wrongPortTask);

PrintResult(wrongHostTask.Result);  results.Add(wrongHostTask.Result);
PrintResult(wrongPortTask.Result);  results.Add(wrongPortTask.Result);

// ─── Login Credentials — Is the username/password correct? ────────────────

PrintSection("2. Login Credentials — Is the username/password correct?");

Console.WriteLine("  ℹ️  Tests a wrong password and a nonexistent user.");
Console.WriteLine("     Expect: 2 intentional FAILs (login errors).");
Console.WriteLine();

// Both login tests are independent — run in parallel
var wrongPwTask = RunTest(
    "Wrong password",
    BuildConnStr(sqlConfig, password: "WRONG_PASSWORD"));
var noUserTask = RunTest(
    "Nonexistent user",
    BuildConnStr(sqlConfig, userId: "nobody_here", password: "whatever"));
await Task.WhenAll(wrongPwTask, noUserTask);

PrintResult(wrongPwTask.Result);  results.Add(wrongPwTask.Result);
PrintResult(noUserTask.Result);   results.Add(noUserTask.Result);

// ─── Database Access — Can we open the target database? ───────────────────

PrintSection("3. Database Access — Can we open the target database?");

Console.WriteLine("  ℹ️  Tests a nonexistent database name and a valid one.");
Console.WriteLine("     Expect: 1 intentional FAIL (wrong DB), 1 PASS.");
Console.WriteLine();

// Both database tests are independent 
var wrongDbTask = RunTest(
    "Wrong database name",
    BuildConnStr(sqlConfig, database: "NoSuchDatabase"));
var correctDbTask = RunTest(
    "Correct database (master)",
    goodConnStr);
await Task.WhenAll(wrongDbTask, correctDbTask);

PrintResult(wrongDbTask.Result);    results.Add(wrongDbTask.Result);
PrintResult(correctDbTask.Result);  results.Add(correctDbTask.Result);

// ─── Command Timeouts — Slow queries that exceed CommandTimeout ───────────

PrintSection("4. Command Timeouts — Slow queries that exceed CommandTimeout");

Console.WriteLine("  ℹ️  Tests a deliberately slow query (WAITFOR 10s) against a short timeout.");
Console.WriteLine("     Expect: 1 intentional FAIL (timeout), 1 PASS (fast query).");
Console.WriteLine();

// Both command-timeout tests are independent — run in parallel
var slowQueryTask = RunTest(
    "WAITFOR 10s with CommandTimeout=2s (expect timeout)",
    goodConnStr,
    sql: "WAITFOR DELAY '00:00:10'; SELECT 1;",
    cmdTimeoutSeconds: 2);
var fastQueryTask = RunTest(
    "Fast query with CommandTimeout=5s (expect pass)",
    goodConnStr,
    sql: "SELECT 1;",
    cmdTimeoutSeconds: 5);
await Task.WhenAll(slowQueryTask, fastQueryTask);

PrintResult(slowQueryTask.Result);  results.Add(slowQueryTask.Result);
PrintResult(fastQueryTask.Result);  results.Add(fastQueryTask.Result);

// ─── Connection Timeouts — Connect Timeout vs CommandTimeout ──────────────
//
// Connect Timeout = max seconds to OPEN the TCP connection to the server
// CommandTimeout  = max seconds to EXECUTE a SQL command after connection is open

PrintSection("5. Connection Timeouts — Connect Timeout vs CommandTimeout");

Console.WriteLine("  ℹ️  Connect Timeout = max seconds to OPEN the connection");
Console.WriteLine("     CommandTimeout   = max seconds to EXECUTE a query");
Console.WriteLine("     Expect: 2 intentional FAILs (one connection timeout, one command timeout).");
Console.WriteLine();

// Connection timeout + command timeout are independent 
var connTimeoutTask = RunTest(
    "Bad host, Connect Timeout=3s (connection-level timeout)",
    BuildConnStr(sqlConfig, server: "192.0.2.1,1433", password: "x", connectTimeout: 3));
var cmdTimeoutTask = RunTest(
    "Good host, WAITFOR 10s, CommandTimeout=2s (command-level timeout)",
    goodConnStr,
    sql: "WAITFOR DELAY '00:00:10';",
    cmdTimeoutSeconds: 2);
await Task.WhenAll(connTimeoutTask, cmdTimeoutTask);

PrintResult(connTimeoutTask.Result);  results.Add(connTimeoutTask.Result);
PrintResult(cmdTimeoutTask.Result);   results.Add(cmdTimeoutTask.Result);

// ─── Deadlock Simulation — Two connections locking in opposite order ──────

PrintSection("6. Deadlock Simulation — Two connections locking in opposite order");

Console.WriteLine("  ℹ️  Task A: lock TableA → wait → lock TableB");
Console.WriteLine("     Task B: lock TableB → wait → lock TableA");
Console.WriteLine("     SQL Server detects the cycle and kills one (error 1205)");
Console.WriteLine("     Expect: PASS if deadlock is detected correctly.");
Console.WriteLine();

// Should PASS when SQL Server detects the deadlock and returns error 1205
r = await RunDeadlockTest("Deadlock between two concurrent transactions", goodConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  App Database Setup — Create a realistic test DB with intentional problems
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
await DatabaseSetup.CreateTestDatabase(goodConnStr);

// Connection string pointing to the app database 
var appConnStr = DatabaseSetup.BuildAppConnStr(goodConnStr);

// ═══════════════════════════════════════════════════════════════════════════════
//  Schema Health Checks  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine("  Schema Health Checks  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 7–30 run against a test database created with INTENTIONAL problems");
Console.WriteLine("     (missing PKs, orphaned rows, duplicate indexes, deprecated types, etc.).");
Console.WriteLine("     ⚠️  WARN results here confirm that each problem was detected correctly.");
Console.WriteLine();

// ─── Missing Primary Keys — Tables that have no PK defined ───────────────

PrintSection("7. Missing Primary Keys — Tables with no PK defined");

r = await SchemaChecks.CheckMissingPrimaryKeys(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Missing Foreign Keys — Columns that look like FKs but aren't ────────

PrintSection("8. Missing Foreign Keys — Id-like columns with no FK constraint");

r = await SchemaChecks.CheckMissingForeignKeys(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Orphaned Records — Child rows referencing nonexistent parents ───────

PrintSection("9. Orphaned Records — Rows referencing nonexistent parent records");

r = await SchemaChecks.CheckOrphanedRecords(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Index Analysis  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Index Analysis  (database: DiagnosticsTestDb)");
Console.WriteLine();

// ─── Missing Indexes — What SQL Server recommends you add ────────────────

PrintSection("10. Missing Indexes — SQL Server recommendations from DMVs");

r = await IndexChecks.CheckMissingIndexes(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Unused Indexes — Indexes that cost writes but are never read ────────

PrintSection("11. Unused Indexes — Zero reads, active writes (dead weight)");

r = await IndexChecks.CheckUnusedIndexes(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Duplicate Indexes — Redundant indexes with identical key columns ────

PrintSection("12. Duplicate Indexes — Same key columns on the same table");

r = await IndexChecks.CheckDuplicateIndexes(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Data Quality & Integrity  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Data Quality & Integrity  (database: DiagnosticsTestDb)");
Console.WriteLine();

// ─── Suspicious Nullable Columns — Business-critical names that allow NULL ──

PrintSection("13. Suspicious Nullable Columns — Email, Name, Status, etc. that allow NULL");

r = await DataQualityChecks.CheckSuspiciousNullables(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Unconstrained Columns — No CHECK, DEFAULT, FK, PK, or UNIQUE ───────

PrintSection("14. Unconstrained Columns — No constraints at all (wide-open data)");

r = await DataQualityChecks.CheckUnconstrainedColumns(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Inconsistent Data Types — Same column name, different types ─────────

PrintSection("15. Inconsistent Data Types — Same name, different types across tables");

r = await DataQualityChecks.CheckInconsistentDataTypes(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Deprecated & Legacy Patterns  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Deprecated & Legacy Patterns  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 16–18 detect outdated features that block cloud migration");
Console.WriteLine("     or cause performance problems at scale.");
Console.WriteLine();

// ─── Deprecated Data Types — text, ntext, image, timestamp ───────────

PrintSection("16. Deprecated Data Types — text, ntext, image, timestamp");

r = await LegacyPatternChecks.CheckDeprecatedDataTypes(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Heap Tables — Tables with no clustered index ────────────────────

PrintSection("17. Heap Tables — No clustered index (fragmentation + full scans)");

r = await LegacyPatternChecks.CheckHeapTables(appConnStr);
PrintResult(r);
results.Add(r);

// ─── GUID Primary Keys — uniqueidentifier as clustered PK ───────────

PrintSection("18. GUID Primary Keys — Random GUID clustered PK (page splits)");

r = await LegacyPatternChecks.CheckGuidPrimaryKeys(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Stored Procedure & Trigger Audit  
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Stored Procedure & Trigger Audit  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 19–22 map where business logic lives in the database.");
Console.WriteLine("     Modernization = pulling this logic into application code.");
Console.WriteLine();

// ─── Stored Procedure Inventory — Count and list all procs ───────────

PrintSection("19. Stored Procedure Inventory — Server-side logic by schema");

r = await CodeAuditChecks.CheckStoredProcedureInventory(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Trigger Inventory — Hidden side-effects on DML ──────────────────

PrintSection("20. Trigger Inventory — INSERT/UPDATE/DELETE triggers on tables");

r = await CodeAuditChecks.CheckTriggerInventory(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Views with Logic — Complex views masking business rules ─────────

PrintSection("21. Views with Logic — 3+ JOINs, CASE, UNION, or subqueries");

r = await CodeAuditChecks.CheckViewsWithLogic(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Dynamic SQL Detection — EXEC(@sql) / sp_executesql in procs ─────

PrintSection("22. Dynamic SQL Detection — Security risk + impossible to analyze statically");

r = await CodeAuditChecks.CheckDynamicSql(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Cross-Database & External Dependencies  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Cross-Database & External Dependencies  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 23–25 find anything tying this database to other databases");
Console.WriteLine("     or servers — these are migration blockers.");
Console.WriteLine();

// ─── Cross-Database References — Three-part names in procs/views ─────

PrintSection("23. Cross-Database References — OtherDb.dbo.Table in procs/views");

r = await DependencyChecks.CheckCrossDbReferences(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Linked Server Usage — OPENQUERY / four-part names ───────────────

PrintSection("24. Linked Server Usage — OPENQUERY, OPENROWSET, four-part names");

r = await DependencyChecks.CheckLinkedServerUsage(appConnStr);
PrintResult(r);
results.Add(r);

// ─── SQL Agent Job Dependencies — Jobs referencing this database ─────

PrintSection("25. SQL Agent Job Dependencies — Scheduled jobs targeting this DB");

r = await DependencyChecks.CheckSqlAgentJobDependencies(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Table Structure & Sizing  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Table Structure & Sizing  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 26–28 help prioritize what to migrate first and spot design problems.");
Console.WriteLine();

// ─── Table Size Inventory — Row count + disk size, largest first ───────

PrintSection("26. Table Size Inventory — Row count + disk size (data + index), largest first");

r = await TableStructureChecks.CheckTableSizeInventory(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Wide Tables — 20+ columns or row size near 8060 bytes ─────────────

PrintSection("27. Wide Tables — 20+ columns or row size approaching 8060-byte limit");

r = await TableStructureChecks.CheckWideTables(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Unused Tables — Zero rows or never referenced ─────────────────────

PrintSection("28. Unused Tables — Zero rows or never referenced by proc/view/FK");

r = await TableStructureChecks.CheckUnusedTables(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Collation & Encoding  (against app database)
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("  Collation & Encoding  (database: DiagnosticsTestDb)");
Console.WriteLine();
Console.WriteLine("  ℹ️  Sections 29–30 catch encoding mismatches that cause silent corruption or query failures.");
Console.WriteLine();

// ─── Collation Mismatches — Columns with non-default collation ─────────

PrintSection("29. Collation Mismatches — Columns/tables using different collation than DB default");

r = await EncodingChecks.CheckCollationMismatches(appConnStr);
PrintResult(r);
results.Add(r);

// ─── Non-Unicode Columns — char/varchar storing text ──────────────────

PrintSection("30. Non-Unicode Columns — char/varchar that should be nchar/nvarchar");

r = await EncodingChecks.CheckNonUnicodeColumns(appConnStr);
PrintResult(r);
results.Add(r);

// ═══════════════════════════════════════════════════════════════════════════════
//  Final Report — Grouped by severity with fix suggestions
// ═══════════════════════════════════════════════════════════════════════════════

Console.WriteLine();
ReportGenerator.PrintFullReport(results);

// ─── Export to files for team sharing ────────────────────────────────────

Console.WriteLine();
var reportDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "reports");
Directory.CreateDirectory(reportDir);

var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
ReportGenerator.ExportToTextFile(results, Path.Combine(reportDir, $"diagnostic-report_{timestamp}.txt"));
ReportGenerator.ExportToCsv(results, Path.Combine(reportDir, $"diagnostic-report_{timestamp}.csv"));

// Keep only the last 5 report sets; delete older ones to avoid unbounded growth
ReportGenerator.CleanupOldReports(reportDir, keepCount: 5);
Console.WriteLine();

// ─── Cleanup: Drop the test database ─────────────────────────────────────

await DatabaseSetup.DropTestDatabase(goodConnStr);
Console.WriteLine();

// ─── Types: Declared after top-level statements (C# requirement) ─────────────

enum Status { PASS, FAIL, WARNING }

record TestResult(string TestName, Status Status, string Message, long ElapsedMs);
