# SQL Server Diagnostic Tool

A comprehensive .NET application for scanning and analyzing SQL Server databases. This tool dynamically connects to any configured database, runs a comprehensive set of structure and design checks, and generates detailed reports in both JSON and PDF formats.

## Introduction

The SQL Server Diagnostic Tool is designed to help database administrators and developers identify structural and design issues in SQL Server databases. Unlike tools that hardcode a single database connection, this application allows you to maintain a list of database targets and scan whichever one you select from a dropdown.

### Key Capabilities

- **Multi-Database Support**: Configure multiple database targets and switch between them seamlessly
- **Comprehensive Checks**: 28 built-in checks covering keys, constraints, referential integrity, indexes, data types, and more
- **Rich Reporting**: Generate detailed reports in JSON and PDF formats
- **Demo Mode**: Includes demo databases with known issues for testing and learning
- **Web-Based UI**: Modern ASP.NET Core web interface for easy interaction

## Features

### Database Structure Checks

The tool performs comprehensive analysis across multiple categories:

#### Keys & Constraints
- **Missing Primary Keys**: Identifies tables without primary keys
- **Nullable or Disabled Primary Key**: Finds PK columns that are nullable or PK constraint disabled
- **Natural vs Surrogate Key Heuristic**: Flags tables with both Id-like and natural-key-like columns for review
- **Composite Primary Key Review**: Lists tables with composite PKs for consistency review
- **Duplicate Identity Candidates**: Tables with multiple single-column unique indexes (review intended PK)
- **Missing Foreign Keys**: Detects relationships without foreign key constraints
- **Referential Integrity**: FK target not unique, nullable FK columns, cascade rules review, circular FK dependencies, 1:1 missing unique on FK, polymorphic type+id pairs without FK
- **Missing Unique Constraints**: Finds columns that should have uniqueness constraints
- **Missing Check Constraints**: Identifies status-like columns without validation constraints
- **Orphan Records**: Detects child rows referencing non-existent parent records
- **Foreign Key Type Mismatches**: Finds type inconsistencies between FK and referenced columns

#### Index Health
- **Heap Tables**: Identifies tables without clustered indexes
- **Index Fragmentation**: Detects fragmented indexes that need maintenance
- **Unused Indexes**: Finds indexes that are never used (wasting space and slowing writes)
- **Missing Index Suggestions**: Reports SQL Server's index suggestions for query optimization

#### Data Quality & Design
- **Money Stored as Float**: Detects currency columns using float/real (precision issues)
- **Extreme Nullable Ratio**: Identifies tables with excessive nullable columns (>50%)
- **Junction Table Issues**: Finds many-to-many tables missing composite keys
- **Inconsistent Formats**: Flags inconsistent casing/whitespace in status-like columns
- **Broken Business Rules**: Detects date and amount logic violations
- **Status/Type Constraint Consistency**: Finds status/type columns where some tables enforce check constraints and others do not

### Reporting Features

- **Categorized Results**: Results grouped by category for easy navigation
- **Severity Levels**: PASS, WARNING, and FAIL status for each check
- **Detailed Information**: Each issue includes:
  - What's wrong
  - Why it matters
  - What to do next
  - Specific details (table names, column names, etc.)
- **Export Options**:
  - JSON reports for programmatic processing (download on demand)
  - PDF reports for documentation and sharing (download on demand)
- **Category Filter**: Run all checks or filter by category (Keys & Constraints, Data Quality, etc.)

### Demo Databases

The tool includes pre-configured demo databases with known issues:

- **Demo – RetailOps Legacy**: Realistic legacy retail operations database
- **Demo – MedTrack Clinical**: Healthcare database scenario
- **Demo – ManufacturingOps Industrial**: Manufacturing/plant-floor database scenario
- **Demo – LegalCase DB**: Legal/case-management scenario

## Installation

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- SQL Server instance (local or remote)
  - SQL Server 2012 or later
  - SQL Server Express is supported

### Quick Start with Docker

If you don't have SQL Server installed, you can run it in Docker:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" \
  -p 1433:1433 -d \
  --name sqlserver \
  mcr.microsoft.com/mssql/server:2022-latest
```

### Clone and Build

```bash
git clone <repository-url>
cd ConsoleApp1
dotnet restore
dotnet build
```

## Configuration

### Database Targets

Configure your database targets in `appsettings.json` or `appsettings.Development.json`:

```json
{
  "SqlDiag": {
    "DatabaseTargets": [
      {
        "Id": "customer-db",
        "DisplayName": "CustomerDB – Production",
        "ConnectionString": "Server=localhost;Database=CustomerDB;Integrated Security=true;TrustServerCertificate=true;",
        "Description": "Main customer database",
        "Tags": ["production", "customer"]
      },
      {
        "Id": "staging-db",
        "DisplayName": "Staging Database",
        "ConnectionString": "Server=localhost;Database=StagingDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;",
        "Description": "Staging environment",
        "Tags": ["staging"]
      }
    ],
    "DemoServerConnectionString": "Server=localhost;Integrated Security=true;",
    "AutoCreateDemoDatabases": true
  }
}
```

### Configuration Options

- **DatabaseTargets**: Array of database configurations
  - `Id`: Unique identifier (used internally)
  - `DisplayName`: Name shown in the dropdown
  - `ConnectionString`: SQL Server connection string
  - `Description`: Optional description
  - `Tags`: Optional array of tags for categorization

- **DemoServerConnectionString**: Connection string for demo database server (optional)
- **AutoCreateDemoDatabases**: Automatically create demo databases on startup (default: true)

### Security Notes

- **Never commit passwords** to version control
- Use `appsettings.Development.json` for local development (typically gitignored)
- Consider using Windows Authentication (`Integrated Security=true`) when possible
- For production, use secure configuration providers (Azure Key Vault, environment variables, etc.)

## Usage

### Web Application

1. **Start the application**:
   ```bash
   cd "SqlDiagTool.Web "
   dotnet run
   ```

2. **Open your browser** to the URL shown in the console (typically `https://localhost:5001` or `http://localhost:5000`). The app redirects to `/diagnostics`.

3. **Select a database** from the dropdown (populated from your `DatabaseTargets` configuration)

4. **(Optional)** Choose a **category** to run a subset of checks, or leave as "All categories" for a full scan.

5. **Click "Run Diagnostics"** to execute the checks.

6. **Review the results**:
   - Summary cards show counts of Pass/Warning/Fail checks
   - Results are grouped by category in expandable sections
   - Each check shows status, duration, and detailed information

7. **Export reports**:
   - Click "Download PDF Report" for a formatted PDF document
   - Click "Download JSON Report" for machine-readable JSON data

### Console Application

The console application (`SqlDiagTool`) currently serves as a library. The main entry point is the web application.

## Architecture

The solution is organized into three projects with clear separation of concerns:

### Project Structure

```
SqlDiagTool.sln
├── SqlDiagTool.Shared/
│   └── Models/
│       ├── DatabaseTarget.cs       # Database target configuration
│       ├── TestResult.cs           # Check execution result
│       └── Status.cs               # PASS, WARNING, FAIL enum
│
├── SqlDiagTool/
│   ├── Program.cs                  # Minimal entry point
│   ├── appsettings.json            # Configuration
│   ├── Configuration/              # Configuration binding
│   ├── Checks/                     # Individual check implementations
│   │   ├── IStructureCheck.cs     # Check interface
│   │   ├── CheckRegistry.cs       # Registry of all checks
│   │   ├── SqlHelper.cs           # Shared connection/query helpers
│   │   ├── ReferentialIntegrityQueries.cs  # Shared SQL for FK checks
│   │   ├── DataQuality/           # Duplicate records, inconsistent formats
│   │   ├── DataTypeConsistency/   # FK type mismatch, money as float, business rules
│   │   ├── IndexHealth/           # Missing/unused indexes, fragmentation
│   │   ├── KeysAndConstraints/    # PK, unique, check constraints
│   │   ├── ReferentialIntegrity/  # Missing FKs, orphans, cascade, circular
│   │   ├── SchemaAndStructure/    # Heaps, nullable ratio, junction
│   │   └── SchemaOverview/        # Schema summary
│   ├── Runner/                     # DiagnosticsRunner - executes checks
│   ├── Reporting/                  # Report generation
│   │   ├── CategorizedReportBuilder.cs
│   │   ├── PdfReportGenerator.cs
│   │   ├── CheckFriendlyCopy.cs
│   │   ├── ReportDisplayNames.cs
│   │   ├── FileHelper.cs
│   │   └── ScanReportDtos.cs
│   └── Demo/                       # Demo database provisioning
│
└── SqlDiagTool.Web/
    ├── Program.cs                  # ASP.NET Core startup
    ├── appsettings.json            # Web app configuration
    ├── Pages/
    │   └── Index.cshtml            # Main UI page
    ├── Components/                 # Blazor components
    └── Services/
        ├── DatabaseTargetService.cs
        └── DemoProvisionHostedService.cs
```

### Dependencies

- **SqlDiagTool.Shared**: No dependencies (pure models)
- **SqlDiagTool**: Depends on Shared only
  - `Microsoft.Data.SqlClient` for database access
  - `QuestPDF` for PDF generation
  - `Microsoft.Extensions.Configuration` for config
- **SqlDiagTool.Web**: Depends on Shared and SqlDiagTool
  - ASP.NET Core Razor Pages
  - Blazor Server components

### Design Principles

- **No Circular Dependencies**: Clean dependency graph (Web → SqlDiagTool → Shared)
- **Single Source of Truth**: Same `DatabaseTargets` configuration for all entry points
- **Extensible**: Easy to add new checks by implementing `IStructureCheck` and registering in `CheckRegistry`
- **Testable**: Clear interfaces and separation of concerns

## Available Checks

The tool includes 28 built-in checks:

| Check | Category | Code | Description |
|-------|----------|------|-------------|
| Missing Primary Keys | Keys & Constraints | `MISSING_PK` | Finds tables without primary keys |
| Heap Tables | Schema & Structure | `HEAP_TABLES` | Identifies tables without clustered indexes |
| Extreme Nullable Ratio | Schema & Structure | `EXTREME_NULLABLE_RATIO` | Tables with >50% nullable columns |
| Suspected Junction Missing Key | Schema & Structure | `JUNCTION_MISSING_KEY` | Many-to-many tables without composite keys |
| Missing Check Constraints | Keys & Constraints | `MISSING_CHECK_CONSTRAINTS` | Status-like columns without validation |
| Missing Unique Constraints | Keys & Constraints | `MISSING_UNIQUE_CONSTRAINTS` | Columns that should be unique |
| Missing Foreign Keys | Referential Integrity | `MISSING_FOREIGN_KEYS` | Relationships without FK (missing FK / app-managed) |
| Orphan Records | Referential Integrity | `ORPHAN_RECORDS` | Child rows with missing parents |
| Foreign Key Type Mismatch | Data Type Consistency | `FK_TYPE_MISMATCH` | Type inconsistencies in FKs |
| Money Stored as Float | Data Type Consistency | `MONEY_AS_FLOAT` | Currency columns using float/real |
| Missing Index Suggestions | Index Health | `MISSING_INDEX_SUGGESTIONS` | SQL Server index recommendations |
| Unused Indexes | Index Health | `UNUSED_INDEXES` | Indexes never used |
| Fragmentation | Index Health | `FRAGMENTATION` | Fragmented indexes needing maintenance |
| Schema Summary | Schema Overview | `SCHEMA_SUMMARY` | Overview of tables, columns, relationships |
| Duplicate Records | Data Quality | `DUPLICATE_RECORDS` | Duplicate values in key-like columns |
| Nullable or Disabled Primary Key | Keys & Constraints | `NULLABLE_OR_DISABLED_PK` | PK columns nullable or constraint disabled |
| Natural vs Surrogate Key Heuristic | Keys & Constraints | `NATURAL_SURROGATE_HEURISTIC` | Tables with both Id-like and natural-key-like columns |
| Composite Primary Key Review | Keys & Constraints | `COMPOSITE_PK_REVIEW` | Tables with composite PKs; flag for review |
| Multiple Single-Column Unique Indexes | Keys & Constraints | `DUPLICATE_IDENTITY_CANDIDATES` | Multiple single-column unique indexes; review intended PK |
| FK Target Not Unique | Referential Integrity | `FK_TARGET_NOT_UNIQUE` | FKs referencing non-unique/non-PK column |
| Nullable FK Columns | Referential Integrity | `NULLABLE_FK_COLUMNS` | FK columns that allow NULL; review if required |
| FK Cascade Rules | Referential Integrity | `FK_CASCADE_RULES` | Delete/update action per FK for review |
| Circular FK Dependencies | Referential Integrity | `CIRCULAR_FK` | Cycles in FK graph |
| 1:1 Missing Unique on FK | Referential Integrity | `ONE_TO_ONE_MISSING_UNIQUE` | FK column may represent 1:1; add UNIQUE if so |
| Polymorphic Relationship (No FK) | Referential Integrity | `POLYMORPHIC_RELATIONSHIP` | Polymorphic-style type+id columns with no FK |
| Inconsistent Formats | Data Quality | `INCONSISTENT_FORMATS` | Inconsistent casing/whitespace in status-like columns |
| Broken Business Rules | Data Type Consistency | `BROKEN_BUSINESS_RULES` | Date and amount logic violations |
| Status/Type Constraint Consistency | Data Type Consistency | `STATUS_TYPE_CONSTRAINT_CONSISTENCY` | Inconsistent status/type constraint enforcement across tables |

**Manual / policy:** Primary key stability (business meaning of the key does not change over time) cannot be inferred from schema; review with the team where it matters. Many-to-many without a junction table is design/semantic and hard to auto-detect—treat as manual review or an optional heuristic if implemented.

### Adding Custom Checks

To add a new check:

1. Create a class implementing `IStructureCheck` in the appropriate category folder under `SqlDiagTool/Checks/` (e.g. `ReferentialIntegrity/`, `KeysAndConstraints/`), or in `Checks/` for shared/uncategorized code. Keep the namespace `SqlDiagTool.Checks`.
   ```csharp
   public sealed class MyCustomCheck : IStructureCheck
   {
       public int Id => 99;
       public string Name => "My Custom Check";
       public string Category => "Custom";
       public string Code => "MY_CUSTOM_CHECK";
       
       public async Task<TestResult> RunAsync(string connectionString)
       {
           // Your check logic here
           return new TestResult(Name, Status.PASS, "All good", 0, Id, Category, Code);
       }
   }
   ```

2. Register it in `CheckRegistry.CreateAll`:
   ```csharp
   return new IStructureCheck[]
   {
       // ... existing checks ...
       new MyCustomCheck()
   };
   ```

3. Add user-friendly copy in `CheckFriendlyCopy`

## Reporting

### Report Structure

Reports are organized into categories, with each check showing:

- **Status**: PASS, WARNING, or FAIL
- **Title**: Check name
- **Message**: Summary message
- **Duration**: Execution time in milliseconds
- **Item Count**: Number of issues found
- **Details**: Specific items (table names, column names, etc.)
- **Guidance**: What's wrong, why it matters, and what to do next

### PDF Reports

PDF reports include:
- Professional formatting with color-coded status badges
- Summary statistics
- Categorized results
- Detailed issue listings
- Page numbers and timestamps

### JSON Reports

JSON reports provide:
- Machine-readable format
- Complete check results
- Structured data for integration
- Easy parsing for automation

Reports are generated in-memory and available for download after a scan. JSON and PDF exports are served on demand; the report is cached for 30 minutes after the scan.

## Demo Features

The application includes demo database provisioning for testing and demonstration:

### Demo Databases

1. **Demo – RetailOps Legacy**: Realistic legacy retail operations scenario
2. **Demo – MedTrack Clinical**: Healthcare database scenario
3. **Demo – ManufacturingOps Industrial**: Manufacturing/plant-floor scenario
4. **Demo – LegalCase DB**: Legal/case-management (1:1, circular FK, polymorphic)

### Enabling Demo Mode

Demo databases are automatically created on startup if:
- `AutoCreateDemoDatabases` is `true` (default)
- `DemoServerConnectionString` is configured
- The SQL Server instance is accessible

Demo databases use `RetailOps_Legacy`, `MedTrack_Clinical`, `ManufacturingOps_Industrial`, and `LegalCase_Db` (or the configured database names).

## Development

### Building from Source

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests (if available)
dotnet test
```

### Running in Development Mode

```bash
cd "SqlDiagTool.Web "
dotnet run --environment Development
```

Development mode enables:
- Detailed error pages
- Hot reload
- Demo database auto-provisioning

### Project Dependencies

- **.NET 9.0**: Target framework
- **Microsoft.Data.SqlClient 5.2.2**: SQL Server connectivity
- **QuestPDF 2024.10.1**: PDF generation
- **Microsoft.Extensions.Configuration**: Configuration management
- **ASP.NET Core**: Web framework

## Support

For issues, questions, or contributions, please [open an issue](link-to-issues) or [create a pull request](link-to-prs).

---

**Note**: This tool focuses on database structure and design analysis. It does not include query performance tuning, deadlock analysis, or execution plan optimization.
