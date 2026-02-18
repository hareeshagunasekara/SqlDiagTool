# SQL Server Diagnostic Tool

A comprehensive .NET application for scanning and analyzing SQL Server databases. This tool dynamically connects to any configured database, runs a comprehensive set of structure and design checks, and generates detailed reports in both JSON and PDF formats.

## Introduction

The SQL Server Diagnostic Tool is designed to help database administrators and developers identify structural and design issues in SQL Server databases. Unlike tools that hardcode a single database connection, this application allows you to maintain a list of database targets and scan whichever one you select from a dropdown.

### Key Capabilities

- **Multi-Database Support**: Configure multiple database targets and switch between them seamlessly
- **Comprehensive Checks**: 13+ built-in checks covering keys, constraints, indexes, data types, and more
- **Rich Reporting**: Generate detailed reports in JSON and PDF formats
- **Demo Mode**: Includes demo databases with known issues for testing and learning
- **Web-Based UI**: Modern ASP.NET Core web interface for easy interaction

## Features

### Database Structure Checks

The tool performs comprehensive analysis across multiple categories:

#### Keys & Constraints
- **Missing Primary Keys**: Identifies tables without primary keys
- **Missing Foreign Keys**: Detects relationships without foreign key constraints
- **Missing Unique Constraints**: Finds columns that should have uniqueness constraints
- **Missing Check Constraints**: Identifies status-like columns without validation constraints
- **Orphan Records**: Detects child rows referencing non-existent parent records
- **Foreign Key Type Mismatches**: Finds type inconsistencies between FK and referenced columns

#### Index Analysis
- **Heap Tables**: Identifies tables without clustered indexes
- **Index Fragmentation**: Detects fragmented indexes that need maintenance
- **Unused Indexes**: Finds indexes that are never used (wasting space and slowing writes)
- **Missing Index Suggestions**: Reports SQL Server's index suggestions for query optimization

#### Data Quality & Design
- **Money Stored as Float**: Detects currency columns using float/real (precision issues)
- **Extreme Nullable Ratio**: Identifies tables with excessive nullable columns (>50%)
- **Junction Table Issues**: Finds many-to-many tables missing composite keys

#### Performance
- **Top Slow Queries**: Reports queries with high execution times

### Reporting Features

- **Categorized Results**: Results grouped by category for easy navigation
- **Severity Levels**: PASS, WARNING, and FAIL status for each check
- **Detailed Information**: Each issue includes:
  - What's wrong
  - Why it matters
  - What to do next
  - Specific details (table names, column names, etc.)
- **Export Options**:
  - JSON reports for programmatic processing
  - PDF reports for documentation and sharing
- **Report History**: Automatic cleanup of old reports (configurable retention)

### Demo Databases

The tool includes several pre-configured demo databases with known issues:

- **Demo – No primary keys**: Database with tables missing primary keys
- **Demo – Clean**: Well-structured database that should pass all checks
- **Demo – RetailOps Legacy**: Realistic legacy database scenario
- **Demo – MedTrack Clinical**: Healthcare database scenario

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
    "AutoCreateDemoDatabases": true,
    "ReportsDirectory": ""
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
- **ReportsDirectory**: Directory for saving reports (empty = default: `reports` folder)

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

2. **Open your browser** to the URL shown in the console (typically `https://localhost:5001` or `http://localhost:5000`)

3. **Select a database** from the dropdown (populated from your `DatabaseTargets` configuration)

4. **Click "Scan"** to run all diagnostic checks

5. **Review the results**:
   - Summary cards show counts of Pass/Warning/Fail checks
   - Results are grouped by category in expandable sections
   - Each check shows status, duration, and detailed information

6. **Export reports**:
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
│   │   └── [13+ check classes]    # Specific check implementations
│   ├── Runner/                     # DiagnosticsRunner - executes checks
│   ├── Reporting/                  # Report generation
│   │   ├── CategorizedReportBuilder.cs
│   │   ├── PdfReportGenerator.cs
│   │   ├── JsonReportWriter.cs
│   │   └── CheckFriendlyCopy.cs
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

The tool includes 13 built-in checks:

| Check | Category | Code | Description |
|-------|----------|------|-------------|
| Missing Primary Keys | Keys & Constraints | `MISSING_PK` | Finds tables without primary keys |
| Heap Tables | Index Analysis | `HEAP_TABLES` | Identifies tables without clustered indexes |
| Extreme Nullable Ratio | Data Quality | `EXTREME_NULLABLE_RATIO` | Tables with >50% nullable columns |
| Suspected Junction Missing Key | Keys & Constraints | `JUNCTION_MISSING_KEY` | Many-to-many tables without composite keys |
| Missing Check Constraints | Keys & Constraints | `MISSING_CHECK_CONSTRAINTS` | Status-like columns without validation |
| Missing Unique Constraints | Keys & Constraints | `MISSING_UNIQUE_CONSTRAINTS` | Columns that should be unique |
| Missing Foreign Keys | Keys & Constraints | `MISSING_FOREIGN_KEYS` | Relationships without FK constraints |
| Orphan Records | Data Quality | `ORPHAN_RECORDS` | Child rows with missing parents |
| Foreign Key Type Mismatch | Data Quality | `FK_TYPE_MISMATCH` | Type inconsistencies in FKs |
| Money Stored as Float | Data Quality | `MONEY_AS_FLOAT` | Currency columns using float/real |
| Missing Index Suggestions | Index Analysis | `MISSING_INDEX_SUGGESTIONS` | SQL Server index recommendations |
| Unused Indexes | Index Analysis | `UNUSED_INDEXES` | Indexes never used |
| Fragmentation | Index Analysis | `FRAGMENTATION` | Fragmented indexes needing maintenance |
| Top Slow Queries | Performance | `TOP_SLOW_QUERIES` | Queries with high execution times |

### Adding Custom Checks

To add a new check:

1. Create a class implementing `IStructureCheck`:
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

2. Register it in `CheckRegistry.All`:
   ```csharp
   public static IReadOnlyList<IStructureCheck> All => new IStructureCheck[]
   {
       // ... existing checks ...
       new MyCustomCheck()
   };
   ```

3. Add friendly copy in `CheckFriendlyCopy` (optional but recommended)

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

Reports are saved to the `reports` directory (or configured `ReportsDirectory`) with timestamps. Old reports are automatically cleaned up (default: keep last 10).

## Demo Features

The application includes demo database provisioning for testing and demonstration:

### Demo Databases

1. **Demo – No primary keys**: Simple database with tables missing primary keys
2. **Demo – Clean**: Well-structured database that should pass checks
3. **Demo – RetailOps Legacy**: Realistic legacy retail operations scenario
4. **Demo – MedTrack Clinical**: Healthcare database scenario

### Enabling Demo Mode

Demo databases are automatically created on startup if:
- `AutoCreateDemoDatabases` is `true` (default)
- `DemoServerConnectionString` is configured
- The SQL Server instance is accessible

Demo databases are prefixed with `SqlDiagTool_Demo_` or use the configured database name.

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

## Contributing

Contributions are welcome! Areas for improvement:

- Additional check implementations
- Performance optimizations
- UI/UX enhancements
- Documentation improvements
- Test coverage

## License

[Add your license information here]

## Support

For issues, questions, or contributions, please [open an issue](link-to-issues) or [create a pull request](link-to-prs).

---

**Note**: This tool focuses on database structure and design analysis. It does not include query performance tuning, deadlock analysis, or execution plan optimization (though these may be added in future versions).
