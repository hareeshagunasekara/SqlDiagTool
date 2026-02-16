# SQL Server Diagnostic Project

An **example** .NET 9 console application for **understanding how to detect database-related issues programmatically** with SQL Server. It creates and drops a temporary test database, runs **30 checks** against it (connection failures, schema health, legacy patterns, dependencies, encoding, etc.), and prints a report with severity and fix suggestions. The goal is to illustrate **concepts** — what to look for and how to query system catalogs and DMVs — rather than to be a production-ready product.

## Concepts You Can Learn Here

- **Migration readiness** — How to find deprecated types, cross-database references, linked server usage, and SQL Agent job dependencies that block moving to Azure SQL or a new platform.
- **Performance and design** — How to spot heaps, GUID clustered PKs, wide tables, and duplicate or unused indexes using system views and DMVs.
- **Data integrity** — How to detect missing PKs/FKs, orphaned rows, suspicious nullables, unconstrained columns, and inconsistent types.
- **Where logic lives** — How to inventory stored procedures, triggers, complex views, and dynamic SQL from definitions and dependencies.
- **Encoding and collation** — How to detect collation mismatches and non-Unicode columns that can cause silent corruption or query failures.
- **Reporting** — One way to structure results (PASS/WARN/FAIL, suggested fixes, .txt/.csv export).

The project creates a temporary test database with intentional schema problems, runs each check against it, prints results to the console, exports reports, then drops the database. You can also point the connection at an existing database to run the same checks against real schemas.

## What It Checks

### Connection & Authentication (Sections 1–6)

These tests use **deliberately bad inputs** to illustrate how to detect and classify each failure mode (timeouts, login errors, deadlocks, etc.) in code.

| # | Check | What it proves |
|---|---|---|
| 1 | **Basic Connectivity** | Detects unreachable hosts, wrong ports, DNS failures |
| 2 | **Login Credentials** | Detects wrong passwords and nonexistent SQL logins |
| 3 | **Database Access** | Detects invalid database names (error 4060) |
| 4 | **Command Timeouts** | Detects queries that exceed `CommandTimeout` |
| 5 | **Connection Timeouts** | Distinguishes `Connect Timeout` (network) from `CommandTimeout` (query) |
| 6 | **Deadlock Simulation** | Triggers a real deadlock between two transactions and verifies error 1205 |

### Schema Health (Sections 7–9)

| # | Check | What it finds |
|---|---|---|
| 7 | **Missing Primary Keys** | Tables with no PK defined |
| 8 | **Missing Foreign Keys** | Columns named `*Id` that have no FK constraint |
| 9 | **Orphaned Records** | Child rows referencing nonexistent parent records |

### Index Analysis (Sections 10–12)

| # | Check | What it finds |
|---|---|---|
| 10 | **Missing Indexes** | SQL Server DMV recommendations ranked by impact |
| 11 | **Unused Indexes** | Indexes with zero reads but active writes (dead weight) |
| 12 | **Duplicate Indexes** | Redundant indexes with identical key columns on the same table |

### Data Quality & Integrity (Sections 13–15)

| # | Check | What it finds |
|---|---|---|
| 13 | **Suspicious Nullable Columns** | Business-critical columns (Email, Name, Status, etc.) that allow NULL |
| 14 | **Unconstrained Columns** | Columns with no CHECK, DEFAULT, FK, PK, or UNIQUE constraint |
| 15 | **Inconsistent Data Types** | Same column name with different types across tables (e.g. `CustomerId` as INT vs BIGINT) |

### Deprecated & Legacy Patterns (Sections 16–18)

| # | Check | What it finds |
|---|---|---|
| 16 | **Deprecated Data Types** | Columns using `text`, `ntext`, `image`, or `timestamp` (replace with `nvarchar(max)`, `varbinary(max)`, `rowversion`) |
| 17 | **Heap Tables** | Tables with no clustered index (fragmentation, full scans, forwarding pointers) |
| 18 | **GUID Primary Keys** | Tables using `uniqueidentifier` as the clustered PK (random GUIDs cause page splits; prefer `NEWSEQUENTIALID()` or INT) |

### Stored Procedure & Trigger Audit (Sections 19–22)

| # | Check | What it finds |
|---|---|---|
| 19 | **Stored Procedure Inventory** | All stored procs grouped by schema (how much logic lives server-side) |
| 20 | **Trigger Inventory** | INSERT/UPDATE/DELETE triggers (hidden side-effects when migrating to a new ORM or API) |
| 21 | **Views with Logic** | Views that join 3+ tables or contain CASE/UNION/subqueries (embedded business rules) |
| 22 | **Dynamic SQL Detection** | Procs/views that use `EXEC(@sql)` or `sp_executesql` (security risk, hard to analyze) |

### Cross-Database & External Dependencies (Sections 23–25)

| # | Check | What it finds |
|---|---|---|
| 23 | **Cross-Database References** | Three-part names (e.g. `OtherDb.dbo.Table`) in procs/views — migration blockers |
| 24 | **Linked Server Usage** | Four-part names or `OPENQUERY`/`OPENROWSET` (not supported in Azure SQL DB) |
| 25 | **SQL Agent Job Dependencies** | Scheduled jobs that reference this database (must move to Azure Functions, Logic Apps, or cron) |

### Table Structure & Sizing (Sections 26–28)

| # | Check | What it finds |
|---|---|---|
| 26 | **Table Size Inventory** | Row count and disk size (data + index) per table, largest first |
| 27 | **Wide Tables** | Tables with 20+ columns or row size approaching the 8060-byte page limit |
| 28 | **Unused Tables** | Tables with zero rows or never referenced by any proc, view, or FK |

### Collation & Encoding (Sections 29–30)

| # | Check | What it finds |
|---|---|---|
| 29 | **Collation Mismatches** | Columns using a different collation than the database default (causes JOIN/WHERE failures) |
| 30 | **Non-Unicode Columns** | `char`/`varchar` columns storing text (data loss risk for international characters; should be `nchar`/`nvarchar`) |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A running SQL Server instance (local or remote)
  - The easiest way is Docker: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
- The SQL login needs `sysadmin` or `dbcreator` role (the example creates and drops a temporary database)

## Configuration

Connection settings are loaded in this order (later sources override earlier ones):

1. **`appsettings.json`** — default values, safe to commit (password is empty)
2. **`appsettings.Development.json`** — local overrides with real credentials (gitignored)
3. **Environment variables** — override everything, useful for CI/production

### Setting up local credentials

Create `SqlDiagTool/appsettings.Development.json` (this file is gitignored):

```json
{
  "SqlConnection": {
    "Password": "YourPasswordHere"
  }
}
```

Any key from `appsettings.json` can be overridden here:

```json
{
  "SqlConnection": {
    "Server": "remote-server.example.com,1433",
    "UserId": "diagnostics_user",
    "Password": "YourPasswordHere"
  }
}
```

### Using environment variables

Environment variables use the `__` (double underscore) separator for nested keys:

```bash
export SqlConnection__Server="localhost,1433"
export SqlConnection__Password="YourPasswordHere"
```

## Running

```bash
cd SqlDiagTool
dotnet run
```

When you run it, the project will:

1. Run connection and authentication diagnostics (sections 1–6)
2. Create a temporary database `DiagnosticsTestDb` with intentional problems
3. Run schema, index, data quality, legacy, code audit, dependency, table structure, and encoding checks (sections 7–30)
4. Print a grouped report to the console (Critical / Warning / OK)
5. Export timestamped `.txt` and `.csv` reports to the `reports/` directory
6. Clean up old reports (keeps the last 5 sets)
7. Drop the temporary database

## Report Output

Each run produces two files in `SqlDiagTool/reports/`:

- `diagnostic-report_YYYY-MM-DD_HHmmss.txt` — human-readable report with fix suggestions
- `diagnostic-report_YYYY-MM-DD_HHmmss.csv` — spreadsheet-friendly format with columns: Severity, TestName, Status, ElapsedMs, Message, SuggestedFix

Only the 5 most recent report sets are kept on disk. Older reports are automatically deleted.

## Project Structure

```
SqlDiagTool/
├── Program.cs              # Entry point, connection tests, deadlock simulation, orchestration
├── DatabaseSetup.cs        # Creates/drops the test database with intentional problems
├── SchemaChecks.cs         # Missing PKs, missing FKs, orphaned records (7–9)
├── IndexChecks.cs          # Missing, unused, duplicate indexes (10–12)
├── DataQualityChecks.cs    # Nullable, unconstrained, inconsistent types (13–15)
├── LegacyPatternChecks.cs  # Deprecated types, heaps, GUID PKs (16–18)
├── CodeAuditChecks.cs      # Proc/trigger inventory, complex views, dynamic SQL (19–22)
├── DependencyChecks.cs     # Cross-database refs, linked server, SQL Agent jobs (23–25)
├── TableStructureChecks.cs # Table size, wide tables, unused tables (26–28)
├── EncodingChecks.cs       # Collation mismatches, non-Unicode columns (29–30)
├── ReportGenerator.cs      # Console report, .txt/.csv export, report cleanup
├── appsettings.json        # Default connection settings (no secrets)
├── appsettings.Development.json  # Local credentials (gitignored)
└── reports/                # Generated reports (gitignored)
```

## Understanding the Output

- **FAIL in sections 1–6** is expected — these tests intentionally use bad inputs to verify error detection.
- **WARN in sections 7–30** is often expected when using the built-in test database — it is created with intentional problems (missing PKs, deprecated types, heaps, triggers, cross-db refs, etc.) so each check has something to find.
- **PASS** means the check ran and found no issues (or the error was correctly detected in negative tests).

Each non-passing result includes a **suggested fix** (e.g. concrete SQL or migration steps) so you can see how findings might be addressed in a real scenario.

## Running Against an Existing Database

To run the same checks (sections 7–30) against an **existing database** instead of the generated test database, point the connection settings at your target database:

```json
{
  "SqlConnection": {
    "Server": "your-server,1433",
    "Database": "YourDatabase",
    "UserId": "your_user",
    "Password": "your_password"
  }
}
```

The connection/authentication tests (sections 1–6) always run against the configured server. The remaining checks (sections 7–30) run against the database in config — by default the temporary `DiagnosticsTestDb` is created and then dropped. To run against a real database without creating/dropping a test DB, set `Database` to your database name and change `Program.cs` so it uses that connection string for sections 7–30 and skips `CreateTestDatabase` / `DropTestDatabase`.
