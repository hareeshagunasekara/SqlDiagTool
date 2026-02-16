using Microsoft.Data.SqlClient;

/// <summary>
/// Creates and tears down a realistic test database with intentional schema problems.
/// The test DB has tables that exercise every diagnostic check:
///   - Tables without primary keys
///   - Columns that look like FKs but have no constraint
///   - Orphaned records (via a disabled FK)
///   - Duplicate indexes
///   - Suspicious nullable columns (Email, Name, Status)
///   - Unconstrained columns (no CHECK, DEFAULT, FK, PK, or UNIQUE)
///   - Inconsistent data types (CustomerId is INT in one table, BIGINT in another)
/// </summary>
static class DatabaseSetup
{
    public const string TestDbName = "DiagnosticsTestDb";

    // ─── BuildAppConnStr: Swaps the Database in a connection string ───────────

    public static string BuildAppConnStr(string masterConnStr)
    {
        var builder = new SqlConnectionStringBuilder(masterConnStr)
        {
            InitialCatalog = TestDbName
        };
        return builder.ConnectionString;
    }

    // ─── CreateTestDatabase: Creates the DB, tables, data, and intentional problems ──

    public static async Task CreateTestDatabase(string masterConnStr)
    {
        Console.WriteLine($"  ℹ️  Creating test database [{TestDbName}] ...");

        await using var conn = new SqlConnection(masterConnStr);
        await conn.OpenAsync();

        // Drop if exists, then create fresh
        await Exec(conn, $"""
            IF DB_ID('{TestDbName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{TestDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{TestDbName}];
            END
            CREATE DATABASE [{TestDbName}];
            """);

        // Switch to the new database for all DDL/DML
        await conn.ChangeDatabaseAsync(TestDbName);

        // ── Good table: has PK, NOT NULL on important columns ────────────
        await Exec(conn, """
            CREATE TABLE dbo.Customers (
                Id          INT IDENTITY PRIMARY KEY,
                Name        NVARCHAR(100) NOT NULL,
                Email       NVARCHAR(200) NOT NULL,
                CreatedDate DATETIME      NOT NULL DEFAULT GETDATE()
            );
            """);

        // ── Good table: has PK + FK to Customers ─────────────────────────
        await Exec(conn, """
            CREATE TABLE dbo.Orders (
                Id          INT IDENTITY PRIMARY KEY,
                CustomerId  INT NOT NULL FOREIGN KEY REFERENCES dbo.Customers(Id),
                OrderDate   DATETIME      NOT NULL DEFAULT GETDATE(),
                Total       DECIMAL(18,2) NOT NULL
            );
            """);

        // ── Problem: table WITHOUT a primary key ─────────────────────────
        await Exec(conn, """
            CREATE TABLE dbo.AuditLog (
                LogDate     DATETIME,
                Action      NVARCHAR(100),
                UserId      INT,
                Details     NVARCHAR(MAX)
            );
            """);

        // ── Problem: table WITHOUT a primary key ─────────────────────────
        await Exec(conn, """
            CREATE TABLE dbo.ErrorLog (
                LogTimestamp DATETIME,
                ErrorCode    NVARCHAR(50),
                Message      NVARCHAR(MAX),
                StackTrace   NVARCHAR(MAX)
            );
            """);

        // ── Problem: columns look like FKs but have NO constraint ────────
        //    Also: nullable Email, Name, Status, Address (suspicious nullables)
        await Exec(conn, """
            CREATE TABLE dbo.Shipments (
                Id          INT IDENTITY PRIMARY KEY,
                OrderId     INT          NOT NULL,
                CustomerId  INT          NOT NULL,
                Address     NVARCHAR(500) NULL,
                Status      NVARCHAR(50)  NULL,
                ShippedDate DATETIME      NULL
            );
            """);

        // ── Problem: inconsistent data type for CustomerId (BIGINT vs INT) ──
        //    Also: unconstrained columns (Rating, Comment have nothing)
        await Exec(conn, """
            CREATE TABLE dbo.Reviews (
                Id          INT IDENTITY PRIMARY KEY,
                CustomerId  BIGINT       NOT NULL,
                OrderId     INT          NULL,
                Rating      INT,
                Comment     NVARCHAR(MAX)
            );
            """);

        // ── Problem: entirely unconstrained table (no PK, no defaults, no checks) ──
        //    Also: suspicious nullable Name, Email, Code
        await Exec(conn, """
            CREATE TABLE dbo.TempImport (
                Name        NVARCHAR(200) NULL,
                Email       NVARCHAR(200) NULL,
                Code        NVARCHAR(50)  NULL,
                Value       INT           NULL
            );
            """);

        // ── Problem: duplicate indexes on the same column ────────────────
        await Exec(conn, """
            CREATE NONCLUSTERED INDEX IX_Orders_CustomerId
                ON dbo.Orders(CustomerId);

            CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_v2
                ON dbo.Orders(CustomerId);
            """);

        // ── Problem: orphaned records via a disabled FK ──────────────────
        //    Create FK, then disable it, then insert orphan data
        await Exec(conn, """
            ALTER TABLE dbo.Shipments
                ADD CONSTRAINT FK_Shipments_Orders
                FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id);

            ALTER TABLE dbo.Shipments NOCHECK CONSTRAINT FK_Shipments_Orders;
            """);

        // ── Seed realistic data ──────────────────────────────────────────
        await Exec(conn, """
            INSERT INTO dbo.Customers (Name, Email) VALUES
                ('Alice Johnson', 'alice@example.com'),
                ('Bob Smith',     'bob@example.com'),
                ('Charlie Brown', 'charlie@example.com');

            INSERT INTO dbo.Orders (CustomerId, OrderDate, Total) VALUES
                (1, '2024-01-15', 99.99),
                (2, '2024-02-20', 149.50),
                (3, '2024-03-10', 249.00);

            INSERT INTO dbo.AuditLog (LogDate, Action, UserId, Details) VALUES
                (GETDATE(), 'LOGIN',  1, 'User logged in'),
                (GETDATE(), 'UPDATE', 2, 'Profile updated'),
                (GETDATE(), 'DELETE', 999, 'Deleted record');

            -- Orphaned shipments: OrderId 888 and 999 don't exist in Orders
            INSERT INTO dbo.Shipments (OrderId, CustomerId, Address, Status) VALUES
                (1,   1,   '123 Main St',  'Delivered'),
                (888, 2,   NULL,            NULL),
                (999, 999, NULL,            NULL);

            -- Reviews with BIGINT CustomerId (inconsistent with Customers.Id which is INT)
            INSERT INTO dbo.Reviews (CustomerId, OrderId, Rating, Comment) VALUES
                (1, 1, 5, 'Great product!'),
                (2, 2, 3, NULL),
                (99999999999, NULL, 1, 'Terrible');

            INSERT INTO dbo.TempImport (Name, Email, Code, Value) VALUES
                ('Import Row 1', NULL, NULL, NULL),
                (NULL, NULL, NULL, NULL);
            """);

        Console.WriteLine($"  ✅ Test database [{TestDbName}] created with intentional problems");
        Console.WriteLine();
    }

    // ─── DropTestDatabase: Cleans up the test database ───────────────────────

    public static async Task DropTestDatabase(string masterConnStr)
    {
        try
        {
            await using var conn = new SqlConnection(masterConnStr);
            await conn.OpenAsync();
            await Exec(conn, $"""
                IF DB_ID('{TestDbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{TestDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{TestDbName}];
                END
                """);
            Console.WriteLine($"  🗑️  Test database [{TestDbName}] dropped");
        }
        catch
        {
            Console.WriteLine($"  ⚠️  Could not drop [{TestDbName}] — clean up manually if needed");
        }
    }

    // ─── Exec: Helper to run a SQL command ───────────────────────────────────

    private static async Task Exec(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync();
    }
}
