namespace SqlDiagTool.Demo;

// Full T-SQL seed for RetailOps_Legacy: tables plus minimal rows and intentional orphans (single batch, no GO).
public static class RetailOpsLegacySeed
{
    public static string GetSeedSql() => """
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Customers')
        CREATE TABLE dbo.Customers (CustomerId INT PRIMARY KEY, Email NVARCHAR(255), Phone INT, FullName NVARCHAR(200), AddressLine1 NVARCHAR(255), AddressLine2 NVARCHAR(255), City NVARCHAR(100), Region NVARCHAR(100), PostalCode NVARCHAR(20));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Addresses')
        CREATE TABLE dbo.Addresses (AddressId INT PRIMARY KEY, CustomerId INT NULL, AddressLine1 NVARCHAR(255), City NVARCHAR(100), Region NVARCHAR(100), PostalCode NVARCHAR(20));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Products')
        CREATE TABLE dbo.Products (ProductId INT PRIMARY KEY, Sku NVARCHAR(50), Name NVARCHAR(200), Price FLOAT);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Orders')
        CREATE TABLE dbo.Orders (OrderNumber NVARCHAR(50) PRIMARY KEY, CustomerId NVARCHAR(50), OrderStatus NVARCHAR(50), TotalAmount FLOAT, CreatedAt DATETIME2, ShipAddressLine1 NVARCHAR(255), ShipCity NVARCHAR(100), ShipPostalCode NVARCHAR(20));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'OrderItems')
        CREATE TABLE dbo.OrderItems (OrderNumber NVARCHAR(50), ProductId INT, Quantity INT, UnitPrice FLOAT);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Payments')
        CREATE TABLE dbo.Payments (PaymentId INT PRIMARY KEY, OrderNumber NVARCHAR(50), CardLast4 NVARCHAR(10), CardType NVARCHAR(50), BankAccountLast4 NVARCHAR(10), BankName NVARCHAR(100), CashAmount FLOAT);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Shipments')
        CREATE TABLE dbo.Shipments (ShipmentId INT PRIMARY KEY, OrderNumber NVARCHAR(50), ShippedAt DATETIME2, Carrier NVARCHAR(100));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Employees')
        CREATE TABLE dbo.Employees (EmployeeId INT PRIMARY KEY, Name NVARCHAR(200), Email NVARCHAR(255));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'AuditLogs')
        CREATE TABLE dbo.AuditLogs (AuditId INT IDENTITY(1,1), TableName NVARCHAR(128), Action NVARCHAR(20), ChangedAt DATETIME2, UserId INT);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'InventoryMovements')
        CREATE TABLE dbo.InventoryMovements (MovementId INT PRIMARY KEY, ProductId INT, Quantity INT, MovementType NVARCHAR(50), CreatedAt DATETIME2);
        IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 1)
        INSERT INTO dbo.Customers (CustomerId, Email, FullName, City, PostalCode) VALUES (1, 'a@example.com', 'Alice', 'City1', '10001');
        IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 1)
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price) VALUES (1, 'SKU001', 'Widget', 9.99);
        IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 1)
        INSERT INTO dbo.Employees (EmployeeId, Name, Email) VALUES (1, 'Bob', 'bob@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderNumber = 'ORD-valid')
        INSERT INTO dbo.Orders (OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) VALUES ('ORD-valid', '1', 'Pending', 19.98, GETUTCDATE());
        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderNumber = 'ORD-orphan')
        INSERT INTO dbo.Orders (OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) VALUES ('ORD-orphan', 'NoSuchCustomer', 'Pending', 0, GETUTCDATE());
        IF NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderNumber = 'ORD-valid' AND ProductId = 1)
        INSERT INTO dbo.OrderItems (OrderNumber, ProductId, Quantity, UnitPrice) VALUES ('ORD-valid', 1, 2, 9.99);
        IF NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderNumber = 'ORD-valid' AND ProductId = 99999)
        INSERT INTO dbo.OrderItems (OrderNumber, ProductId, Quantity, UnitPrice) VALUES ('ORD-valid', 99999, 1, 0);
        IF NOT EXISTS (SELECT 1 FROM dbo.AuditLogs WHERE AuditId = 1)
        INSERT INTO dbo.AuditLogs (TableName, Action, ChangedAt, UserId) VALUES ('Orders', 'INSERT', GETUTCDATE(), 1);
        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryMovements WHERE MovementId = 1)
        INSERT INTO dbo.InventoryMovements (MovementId, ProductId, Quantity, MovementType, CreatedAt) VALUES (1, 1, 10, 'IN', GETUTCDATE());
        """;
}
