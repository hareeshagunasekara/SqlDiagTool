# SQL Server Diagnostic Tool

A .NET 9 console application that runs 15 automated checks against a SQL Server instance and produces an actionable report with severity ratings and fix suggestions.

The tool creates a temporary test database with intentional schema problems, runs every diagnostic against it, prints results to the console, exports `.txt` and `.csv` reports, then cleans up after itself.

## What It Checks

### Connection & Authentication (Sections 1–6)

These tests use **deliberately bad inputs** to verify the tool can detect and classify each failure mode correctly.

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

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A running SQL Server instance (local or remote)
  - The easiest way is Docker: `docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
- The SQL login needs `sysadmin` or `dbcreator` role (the tool creates and drops a temporary database)

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

The tool will:

1. Run connection and authentication diagnostics (sections 1–6)
2. Create a temporary database `DiagnosticsTestDb` with intentional problems
3. Run schema, index, and data quality checks (sections 7–15)
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
├── Program.cs              # Entry point, connection tests, deadlock simulation
├── DatabaseSetup.cs        # Creates/drops the test database with intentional problems
├── SchemaChecks.cs         # Missing PKs, missing FKs, orphaned records
├── IndexChecks.cs          # Missing, unused, and duplicate index detection
├── DataQualityChecks.cs    # Nullable columns, unconstrained columns, type mismatches
├── ReportGenerator.cs      # Console report, .txt export, .csv export, report cleanup
├── appsettings.json        # Default connection settings (no secrets)
├── appsettings.Development.json  # Local credentials (gitignored)
└── reports/                # Generated reports (gitignored)
```

## Understanding the Output

- **FAIL in sections 1–6** is expected — these tests intentionally use bad inputs to verify error detection
- **WARN in sections 7–15** is expected — the test database is created with intentional problems so the checks have something to find
- **PASS** means the check ran and found no issues (or the error was correctly detected in negative tests)

Each non-passing result includes a **suggested fix** with a concrete SQL statement or action.

## Adapting for Your Own Database

To run the schema, index, and data quality checks against an existing database instead of the generated test database, point the connection settings at your target database:

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

The connection/authentication tests (sections 1–6) always run against the configured server. The schema/index/data checks (sections 7–15) currently run against the temporary `DiagnosticsTestDb` — to target a different database, modify the `appConnStr` assignment in `Program.cs`.
