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
///   - Deprecated data types (text, ntext, image, timestamp)
///   - Heap tables (no clustered index)
///   - GUID clustered primary keys (random uniqueidentifier)
///   - Stored procedures with server-side business logic
///   - DML triggers (hidden side-effects)
///   - Complex views with embedded business rules
///   - Dynamic SQL in stored procedures (security + migration risk)
///   - Cross-database references (three-part names)
///   - Linked server usage (OPENQUERY/OPENROWSET patterns)
///   - SQL Agent job dependencies (scheduled jobs referencing this DB)
///   - Wide tables (20+ columns)
///   - Unused tables (zero rows or never referenced)
///   - Collation mismatches (column collation differs from DB default)
///   - Non-Unicode columns (char/varchar for text)
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

        // ── Problem: wide table (20+ columns) for check 27 ─────────────────
        await Exec(conn, """
            CREATE TABLE dbo.WideStaging (
                Id INT IDENTITY PRIMARY KEY,
                Col01 NVARCHAR(100), Col02 NVARCHAR(100), Col03 NVARCHAR(100), Col04 NVARCHAR(100), Col05 NVARCHAR(100),
                Col06 NVARCHAR(100), Col07 NVARCHAR(100), Col08 NVARCHAR(100), Col09 NVARCHAR(100), Col10 NVARCHAR(100),
                Col11 NVARCHAR(100), Col12 NVARCHAR(100), Col13 NVARCHAR(100), Col14 NVARCHAR(100), Col15 NVARCHAR(100),
                Col16 NVARCHAR(100), Col17 NVARCHAR(100), Col18 NVARCHAR(100), Col19 NVARCHAR(100), Col20 NVARCHAR(100),
                Col21 NVARCHAR(100), Col22 NVARCHAR(100), Col23 NVARCHAR(100), Col24 NVARCHAR(100), Col25 NVARCHAR(100)
            );
            """);

        // ── Problem: unused table (0 rows, never referenced by proc/view/FK) for check 28 ──
        await Exec(conn, """
            CREATE TABLE dbo.DeprecatedCache (
                Id INT IDENTITY PRIMARY KEY,
                CacheKey NVARCHAR(200) NOT NULL,
                ExpiresAt DATETIME NOT NULL
            );
            """);

        // ── Problem: collation mismatch (column differs from DB default) for check 29 ──
        await Exec(conn, """
            CREATE TABLE dbo.CollationTest (
                Id   INT PRIMARY KEY,
                Name NVARCHAR(100) COLLATE Latin1_General_BIN2
            );
            """);

        // ── Problem: non-Unicode columns (char/varchar for text) for check 30 ──
        await Exec(conn, """
            CREATE TABLE dbo.LegacyContacts (
                Id        INT IDENTITY PRIMARY KEY,
                FirstName VARCHAR(50)  NOT NULL,
                LastName  VARCHAR(100) NOT NULL,
                Title     CHAR(10)     NULL
            );
            """);

        // ── Problem: duplicate indexes on the same column ────────────────
        await Exec(conn, """
            CREATE NONCLUSTERED INDEX IX_Orders_CustomerId
                ON dbo.Orders(CustomerId);

            CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_v2
                ON dbo.Orders(CustomerId);
            """);

        // ── Problem: deprecated data types (text, ntext, image, timestamp) ──
        await Exec(conn, """
            CREATE TABLE dbo.LegacyDocuments (
                Id              INT IDENTITY PRIMARY KEY,
                Title           NVARCHAR(200)   NOT NULL,
                Body            text            NOT NULL,
                Summary         ntext           NULL,
                Thumbnail       image           NULL,
                RowVer          timestamp
            );
            """);

        // ── Problem: heap table — has rows but NO clustered index at all ─────
        //    (AuditLog and ErrorLog above are also heaps, but this one is explicit)
        await Exec(conn, """
            CREATE TABLE dbo.EventStream (
                EventId         INT IDENTITY,
                EventType       NVARCHAR(100)   NOT NULL,
                Payload         NVARCHAR(MAX)   NULL,
                CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
            );
            """);

        // ── Problem: GUID clustered primary key using random NEWID() ────────
        //    Random GUIDs cause massive page splits and fragmentation
        await Exec(conn, """
            CREATE TABLE dbo.Tickets (
                Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                Subject         NVARCHAR(200)    NOT NULL,
                Priority        INT              NOT NULL DEFAULT 1,
                CreatedDate     DATETIME         NOT NULL DEFAULT GETDATE(),
                CONSTRAINT PK_Tickets PRIMARY KEY CLUSTERED (Id)
            );
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

            -- Deprecated data types: seed a few rows so the table isn't empty
            SET IDENTITY_INSERT dbo.LegacyDocuments OFF;
            INSERT INTO dbo.LegacyDocuments (Title, Body, Summary)
            VALUES
                ('Old Report', 'This body uses the deprecated text type', N'Summary in ntext'),
                ('Archive Doc', 'Another text blob from legacy import',   NULL);

            -- Heap table: seed events (no clustered index on EventStream)
            INSERT INTO dbo.EventStream (EventType, Payload) VALUES
                ('UserSignup',  '{"userId":1}'),
                ('OrderPlaced', '{"orderId":1}'),
                ('PageView',    '{"url":"/home"}');

            -- GUID PK table: seed tickets with random GUIDs
            INSERT INTO dbo.Tickets (Id, Subject, Priority) VALUES
                (NEWID(), 'Login page broken',       3),
                (NEWID(), 'Dashboard loads slowly',  2),
                (NEWID(), 'Feature request: export', 1);
            """);

        // ── Problem: stored procedures with server-side business logic ────
        //    Create a second schema so we exercise the "grouped by schema" output
        await Exec(conn, "CREATE SCHEMA reporting;");

        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_GetCustomerOrders
                @CustomerId INT
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT o.Id, o.OrderDate, o.Total, c.Name, c.Email
                FROM dbo.Orders o
                JOIN dbo.Customers c ON o.CustomerId = c.Id
                WHERE o.CustomerId = @CustomerId
                ORDER BY o.OrderDate DESC;
            END
            """);

        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_ApplyDiscount
                @OrderId INT,
                @DiscountPct DECIMAL(5,2)
            AS
            BEGIN
                SET NOCOUNT ON;
                IF @DiscountPct < 0 OR @DiscountPct > 50
                BEGIN
                    RAISERROR('Discount must be between 0 and 50%%', 16, 1);
                    RETURN;
                END
                UPDATE dbo.Orders
                SET Total = Total * (1 - @DiscountPct / 100.0)
                WHERE Id = @OrderId;
            END
            """);

        await Exec(conn, """
            CREATE PROCEDURE reporting.usp_MonthlySummary
                @Year  INT,
                @Month INT
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT
                    COUNT(*)        AS OrderCount,
                    SUM(Total)      AS Revenue,
                    AVG(Total)      AS AvgOrderValue
                FROM dbo.Orders
                WHERE YEAR(OrderDate) = @Year
                  AND MONTH(OrderDate) = @Month;
            END
            """);

        // ── Problem: DML triggers — hidden side-effects on INSERT/UPDATE ──
        await Exec(conn, """
            CREATE TRIGGER dbo.trg_Orders_AfterInsert
            ON dbo.Orders
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                INSERT INTO dbo.AuditLog (LogDate, Action, UserId, Details)
                SELECT GETDATE(), 'ORDER_CREATED', i.CustomerId,
                       'Order ' + CAST(i.Id AS NVARCHAR(20)) + ' created, total $' + CAST(i.Total AS NVARCHAR(20))
                FROM inserted i;
            END
            """);

        await Exec(conn, """
            CREATE TRIGGER dbo.trg_Customers_AfterUpdate
            ON dbo.Customers
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                INSERT INTO dbo.AuditLog (LogDate, Action, UserId, Details)
                SELECT GETDATE(), 'CUSTOMER_UPDATED', i.Id,
                       'Customer ' + i.Name + ' updated'
                FROM inserted i;
            END
            """);

        await Exec(conn, """
            CREATE TRIGGER dbo.trg_Orders_AfterDelete
            ON dbo.Orders
            AFTER DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                INSERT INTO dbo.AuditLog (LogDate, Action, UserId, Details)
                SELECT GETDATE(), 'ORDER_DELETED', d.CustomerId,
                       'Order ' + CAST(d.Id AS NVARCHAR(20)) + ' deleted'
                FROM deleted d;
            END
            """);

        // ── Problem: complex views with embedded business rules ───────────
        //    This view joins 3+ tables and uses CASE expressions
        await Exec(conn, """
            CREATE VIEW dbo.vw_CustomerOrderSummary
            AS
            SELECT
                c.Id            AS CustomerId,
                c.Name          AS CustomerName,
                c.Email         AS CustomerEmail,
                COUNT(o.Id)     AS TotalOrders,
                ISNULL(SUM(o.Total), 0) AS TotalSpent,
                CASE
                    WHEN SUM(o.Total) >= 500 THEN 'VIP'
                    WHEN SUM(o.Total) >= 100 THEN 'Regular'
                    ELSE 'New'
                END AS CustomerTier,
                MAX(sh.ShippedDate) AS LastShipmentDate
            FROM dbo.Customers c
            LEFT JOIN dbo.Orders o    ON c.Id = o.CustomerId
            LEFT JOIN dbo.Shipments sh ON o.Id = sh.OrderId
            LEFT JOIN dbo.Reviews r   ON c.Id = r.CustomerId
            GROUP BY c.Id, c.Name, c.Email;
            """);

        // ── Problem: dynamic SQL in a stored procedure ───────────────────
        //    Uses sp_executesql and EXEC(@sql) — security risk + unmigrateable
        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_DynamicSearch
                @TableName  NVARCHAR(128),
                @ColumnName NVARCHAR(128),
                @SearchTerm NVARCHAR(256)
            AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'SELECT * FROM ' + QUOTENAME(@TableName)
                         + N' WHERE ' + QUOTENAME(@ColumnName) + N' LIKE @term';
                EXEC sp_executesql @sql, N'@term NVARCHAR(256)', @term = @SearchTerm;
            END
            """);

        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_PurgeOldRecords
                @TableName NVARCHAR(128),
                @DaysOld   INT
            AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @sql NVARCHAR(MAX);
                SET @sql = N'DELETE FROM ' + QUOTENAME(@TableName)
                         + N' WHERE CreatedDate < DATEADD(DAY, -' + CAST(@DaysOld AS NVARCHAR(10)) + N', GETDATE())';
                EXEC(@sql);
            END
            """);

        // ── Problem: cross-database references (three-part names) ────────
        //    These procs query master and msdb — migration blockers
        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_ListAllDatabases
            AS
            BEGIN
                SET NOCOUNT ON;
                SELECT name, state_desc, recovery_model_desc
                FROM master.sys.databases
                WHERE database_id > 4;
            END
            """);

        await Exec(conn, """
            CREATE VIEW dbo.vw_RecentBackups
            AS
            SELECT
                bs.database_name,
                bs.backup_finish_date,
                bs.type AS BackupType,
                bmf.physical_device_name
            FROM msdb.dbo.backupset bs
            JOIN msdb.dbo.backupmediafamily bmf
                ON bs.media_set_id = bmf.media_set_id;
            """);

        // ── Problem: linked server / distributed query patterns ──────────
        //    OPENQUERY and OPENROWSET are blocked in Azure SQL DB.
        //    The patterns are stored as dynamic SQL strings inside the proc so
        //    SQL Server doesn't try to resolve the linked server at CREATE time.
        //    Check 24 scans sys.sql_modules definitions with LIKE, so the
        //    keywords OPENQUERY / OPENROWSET in the body are still detected.
        await Exec(conn, """
            CREATE PROCEDURE dbo.usp_FetchRemoteData
            AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @sql NVARCHAR(MAX);

                -- Linked server pattern: OPENQUERY
                SET @sql = N'SELECT * FROM OPENQUERY([RemoteServer], ''SELECT Id, Name FROM Products'')';
                EXEC sp_executesql @sql;

                -- Distributed query pattern: OPENROWSET
                SET @sql = N'SELECT * FROM OPENROWSET(''SQLNCLI'', ''Server=RemoteHost;Trusted_Connection=yes;'',
                             ''SELECT OrderId, Total FROM RemoteDb.dbo.Orders'')';
                EXEC sp_executesql @sql;
            END
            """);

        // ── Problem: SQL Agent job referencing this database ─────────────
        //    Best-effort: requires msdb permissions (SA in Docker has them)
        try
        {
            // Switch back to master to create the job in msdb
            await conn.ChangeDatabaseAsync("msdb");

            await Exec(conn, $"""
                IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = 'DiagTool_TestJob')
                BEGIN
                    EXEC msdb.dbo.sp_add_job
                        @job_name = N'DiagTool_TestJob',
                        @enabled = 0,
                        @description = N'Test job created by SqlDiagTool — safe to delete';

                    EXEC msdb.dbo.sp_add_jobstep
                        @job_name = N'DiagTool_TestJob',
                        @step_name = N'Cleanup old audit logs',
                        @subsystem = N'TSQL',
                        @database_name = N'{TestDbName}',
                        @command = N'DELETE FROM dbo.AuditLog WHERE LogDate < DATEADD(DAY, -90, GETDATE())';

                    EXEC msdb.dbo.sp_add_jobserver
                        @job_name = N'DiagTool_TestJob',
                        @server_name = N'(LOCAL)';
                END
                """);

            // Switch back to the test database
            await conn.ChangeDatabaseAsync(TestDbName);
        }
        catch
        {
            // Permissions insufficient for msdb — skip silently; check 25 will return PASS
            try { await conn.ChangeDatabaseAsync(TestDbName); } catch { /* best-effort */ }
        }

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

            // Best-effort: remove the SQL Agent test job created for check 25
            try
            {
                await Exec(conn, """
                    IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = 'DiagTool_TestJob')
                        EXEC msdb.dbo.sp_delete_job @job_name = N'DiagTool_TestJob', @delete_unused_schedule = 1;
                    """);
            }
            catch { /* msdb access may not be available — skip silently */ }

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
