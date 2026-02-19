namespace SqlDiagTool.Demo;

public static class RetailOpsLegacySeed
{
    public static string GetSeedSql() => """
        -- Drop all tables first for clean schema
        DROP TABLE IF EXISTS dbo.OrderPromoJunction;
        DROP TABLE IF EXISTS dbo.PaymentAdjustments;
        DROP TABLE IF EXISTS dbo.RefundRequests;
        DROP TABLE IF EXISTS dbo.Promotions;
        DROP TABLE IF EXISTS dbo.PriceHistory;
        DROP TABLE IF EXISTS dbo.LegacyInventorySnapshot;
        DROP TABLE IF EXISTS dbo.StagingOrderImport;
        DROP TABLE IF EXISTS dbo.EventStream;
        DROP TABLE IF EXISTS dbo.AuditLogs;
        DROP TABLE IF EXISTS dbo.InventoryMovements;
        DROP TABLE IF EXISTS dbo.StockLevels;
        DROP TABLE IF EXISTS dbo.Shipments;
        DROP TABLE IF EXISTS dbo.Payments;
        DROP TABLE IF EXISTS dbo.OrderItems;
        DROP TABLE IF EXISTS dbo.Orders;
        DROP TABLE IF EXISTS dbo.Products;
        DROP TABLE IF EXISTS dbo.Categories;
        DROP TABLE IF EXISTS dbo.Suppliers;
        DROP TABLE IF EXISTS dbo.Warehouses;
        DROP TABLE IF EXISTS dbo.Addresses;
        DROP TABLE IF EXISTS dbo.Customers;
        DROP TABLE IF EXISTS dbo.Employees;

        -- ========== CORE ENTITY TABLES ==========
        CREATE TABLE dbo.Customers (CustomerId INT PRIMARY KEY, Email NVARCHAR(255), Phone NVARCHAR(20), FullName NVARCHAR(200), AddressLine1 NVARCHAR(255), AddressLine2 NVARCHAR(255), City NVARCHAR(100), Region NVARCHAR(100), PostalCode NVARCHAR(20));

        CREATE TABLE dbo.Addresses (AddressId INT PRIMARY KEY, CustomerId INT NULL, AddressLine1 NVARCHAR(255), City NVARCHAR(100), Region NVARCHAR(100), PostalCode NVARCHAR(20));

        CREATE TABLE dbo.Warehouses (WarehouseId INT PRIMARY KEY, WarehouseCode NVARCHAR(20), Name NVARCHAR(200), Region NVARCHAR(100));

        CREATE TABLE dbo.Suppliers (SupplierId INT PRIMARY KEY, SupplierCode NVARCHAR(50), Name NVARCHAR(200), ContactEmail NVARCHAR(255));

        CREATE TABLE dbo.Categories (CategoryId INT PRIMARY KEY, CategoryCode NVARCHAR(20), Name NVARCHAR(100));

        CREATE TABLE dbo.Products (ProductId INT PRIMARY KEY, Sku NVARCHAR(50), Name NVARCHAR(200), Price FLOAT, CategoryId INT, SupplierId INT);

        CREATE TABLE dbo.Employees (EmployeeId INT PRIMARY KEY, Name NVARCHAR(200), Email NVARCHAR(255), Department NVARCHAR(50));

        -- Orders: CustomerId NVARCHAR vs Customers.CustomerId INT (ForeignKeyTypeMismatch)
        CREATE TABLE dbo.Orders (OrderId INT PRIMARY KEY, OrderNumber NVARCHAR(50), CustomerId NVARCHAR(50), OrderStatus NVARCHAR(50), TotalAmount FLOAT, CreatedAt DATETIME2, ShipAddressLine1 NVARCHAR(255), ShipCity NVARCHAR(100), ShipPostalCode NVARCHAR(20));

        CREATE TABLE dbo.OrderItems (OrderItemId INT PRIMARY KEY, OrderId INT, ProductId INT, Quantity INT, UnitPrice FLOAT);

        CREATE TABLE dbo.Payments (PaymentId INT PRIMARY KEY, OrderNumber NVARCHAR(50), CardLast4 NVARCHAR(10), CardType NVARCHAR(50), CashAmount FLOAT, PaidAt DATETIME2);

        CREATE TABLE dbo.Shipments (ShipmentId INT PRIMARY KEY, OrderNumber NVARCHAR(50), ShippedAt DATETIME2, Carrier NVARCHAR(100), ShipmentStatus NVARCHAR(50));

        CREATE TABLE dbo.StockLevels (StockLevelId INT PRIMARY KEY, ProductId INT, WarehouseId INT, Quantity INT, LastUpdated DATETIME2);

        CREATE TABLE dbo.InventoryMovements (MovementId INT PRIMARY KEY, ProductId INT, Quantity INT, MovementType NVARCHAR(50), CreatedAt DATETIME2, WarehouseId INT);

        -- AuditLogs: heap (IDENTITY but no PK/clustered index)
        CREATE TABLE dbo.AuditLogs (AuditId INT IDENTITY(1,1), TableName NVARCHAR(128), Action NVARCHAR(20), ChangedAt DATETIME2, UserId INT);

        -- EventStream: heap
        CREATE TABLE dbo.EventStream (SeqNum BIGINT IDENTITY, EventType NVARCHAR(50), OccurredAt DATETIME2, Payload NVARCHAR(MAX));

        -- StagingOrderImport: heap, no PK
        CREATE TABLE dbo.StagingOrderImport (ExternalOrderId NVARCHAR(100), CustomerEmail NVARCHAR(255), OrderTotal FLOAT, ImportBatch NVARCHAR(50), ProcessedAt DATETIME2);

        -- LegacyInventorySnapshot: extreme nullable (>50% columns nullable)
        CREATE TABLE dbo.LegacyInventorySnapshot (SnapshotId INT PRIMARY KEY, ProductId INT NULL, WarehouseId INT NULL, QtyOnHand INT NULL, QtyReserved INT NULL, LastCountDate DATE NULL, Notes NVARCHAR(500) NULL, SourceSystem NVARCHAR(100) NULL);

        CREATE TABLE dbo.PriceHistory (PriceHistoryId INT PRIMARY KEY, ProductId INT, OldPrice FLOAT, NewPrice FLOAT, EffectiveDate DATE);

        -- Promotions: ActiveDate/ExpiryDate for BrokenBusinessRules (ExpiryDate < ActiveDate)
        CREATE TABLE dbo.Promotions (PromotionId INT PRIMARY KEY, PromoCode NVARCHAR(20), ActiveDate DATE, ExpiryDate DATE, DiscountPercent FLOAT);

        CREATE TABLE dbo.RefundRequests (RefundId INT PRIMARY KEY, OrderNumber NVARCHAR(50), RequestedAt DATETIME2, ProcessedAt DATETIME2, Status NVARCHAR(50));

        -- PaymentAdjustments: PaidAmount > TotalAmount for BrokenBusinessRules
        CREATE TABLE dbo.PaymentAdjustments (AdjustmentId INT PRIMARY KEY, OrderNumber NVARCHAR(50), PaidAmount FLOAT, TotalAmount FLOAT, Reason NVARCHAR(200), AdjustedAt DATETIME2);

        -- OrderPromoJunction: junction with OrderId+PromoId but only OrderPromoId as PK (SuspectedJunctionMissingKey)
        CREATE TABLE dbo.OrderPromoJunction (OrderPromoId INT PRIMARY KEY, OrderId INT, PromoId INT);
        -- __BATCH__
        -- ========== SEED DATA ==========
        -- Customers (duplicate Email for DuplicateRecords)
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 1, 'alice@retail.com', '555-0100', 'Alice Smith', 'New York', '10001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 1);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 2, 'bob@retail.com', '555-0101', 'Bob Jones', 'Chicago', '60601' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 2);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 3, 'carol@retail.com', '555-0102', 'Carol White', 'Los Angeles', '90001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 3);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 4, 'duplicate@retail.com', '555-0103', 'Dave Brown', 'Houston', '77001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 4);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 5, 'duplicate@retail.com', '555-0104', 'Eve Green', 'Phoenix', '85001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 5);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 6, 'frank@retail.com', '555-0105', 'Frank Lee', 'Philadelphia', '19101' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 6);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 7, 'grace@retail.com', '555-0106', 'Grace Kim', 'San Antonio', '78201' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 7);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 8, 'henry@retail.com', '555-0107', 'Henry Park', 'San Diego', '92101' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 8);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 9, 'ivy@retail.com', '555-0108', 'Ivy Chen', 'Dallas', '75201' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 9);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 10, 'jack@retail.com', '555-0109', 'Jack Wilson', 'San Jose', '95101' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 10);

        -- Categories (duplicate CategoryCode for DuplicateRecords)
        INSERT INTO dbo.Categories (CategoryId, CategoryCode, Name) SELECT 1, 'ELEC', 'Electronics' WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = 1);
        INSERT INTO dbo.Categories (CategoryId, CategoryCode, Name) SELECT 2, 'HOME', 'Home & Garden' WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = 2);
        INSERT INTO dbo.Categories (CategoryId, CategoryCode, Name) SELECT 3, 'ELEC', 'Consumer Electronics' WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = 3);
        INSERT INTO dbo.Categories (CategoryId, CategoryCode, Name) SELECT 4, 'APPR', 'Apparel' WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = 4);
        INSERT INTO dbo.Categories (CategoryId, CategoryCode, Name) SELECT 5, 'ELEC', 'Electronics Dept' WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = 5);

        -- Suppliers (duplicate SupplierCode)
        INSERT INTO dbo.Suppliers (SupplierId, SupplierCode, Name, ContactEmail) SELECT 1, 'SUP-001', 'Acme Supplies', 'acme@supply.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE SupplierId = 1);
        INSERT INTO dbo.Suppliers (SupplierId, SupplierCode, Name, ContactEmail) SELECT 2, 'SUP-002', 'Global Parts', 'parts@global.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE SupplierId = 2);
        INSERT INTO dbo.Suppliers (SupplierId, SupplierCode, Name, ContactEmail) SELECT 3, 'SUP-001', 'Acme Wholesale', 'wholesale@acme.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE SupplierId = 3);

        -- Warehouses
        INSERT INTO dbo.Warehouses (WarehouseId, WarehouseCode, Name, Region) SELECT 1, 'WH-NY', 'New York DC', 'Northeast' WHERE NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE WarehouseId = 1);
        INSERT INTO dbo.Warehouses (WarehouseId, WarehouseCode, Name, Region) SELECT 2, 'WH-CA', 'California DC', 'West' WHERE NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE WarehouseId = 2);
        INSERT INTO dbo.Warehouses (WarehouseId, WarehouseCode, Name, Region) SELECT 3, 'WH-TX', 'Texas DC', 'South' WHERE NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE WarehouseId = 3);

        -- Products (duplicate Sku, Price as FLOAT for MoneyStoredAsFloat)
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 1, 'SKU-001', 'Widget Pro', 29.99, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 1);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 2, 'SKU-002', 'Gadget Plus', 49.99, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 2);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 3, 'SKU-003', 'Tool Set', 79.99, 2, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 3);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 4, 'SKU-001', 'Widget Pro v2', 34.99, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 4);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 5, 'SKU-005', 'Shirt Blue', 19.99, 4, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 5);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 6, 'SKU-006', 'Lamp LED', 45.00, 2, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 6);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 7, 'SKU-007', 'Cable USB', 9.99, 1, 3 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 7);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 8, 'SKU-008', 'Desk Organizer', 24.99, 2, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 8);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 9, 'SKU-009', 'Monitor 24in', 199.99, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 9);
        INSERT INTO dbo.Products (ProductId, Sku, Name, Price, CategoryId, SupplierId) SELECT 10, 'SKU-010', 'Chair Ergo', 299.99, 2, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId = 10);

        -- Employees
        INSERT INTO dbo.Employees (EmployeeId, Name, Email, Department) SELECT 1, 'Admin User', 'admin@retail.com', 'IT' WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 1);
        INSERT INTO dbo.Employees (EmployeeId, Name, Email, Department) SELECT 2, 'Sales Rep 1', 'sales1@retail.com', 'Sales' WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 2);
        INSERT INTO dbo.Employees (EmployeeId, Name, Email, Department) SELECT 3, 'Warehouse Manager', 'wh@retail.com', 'Operations' WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeId = 3);

        -- Addresses (Nullable CustomerId for NullableForeignKeyColumns)
        INSERT INTO dbo.Addresses (AddressId, CustomerId, AddressLine1, City, Region, PostalCode) SELECT 1, 1, '100 Main St', 'New York', 'NY', '10001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Addresses WHERE AddressId = 1);
        INSERT INTO dbo.Addresses (AddressId, CustomerId, AddressLine1, City, Region, PostalCode) SELECT 2, NULL, '200 Oak Ave', 'Chicago', 'IL', '60601' WHERE NOT EXISTS (SELECT 1 FROM dbo.Addresses WHERE AddressId = 2);
        INSERT INTO dbo.Addresses (AddressId, CustomerId, AddressLine1, City, Region, PostalCode) SELECT 3, 2, '300 Pine Rd', 'Chicago', 'IL', '60602' WHERE NOT EXISTS (SELECT 1 FROM dbo.Addresses WHERE AddressId = 3);
        INSERT INTO dbo.Addresses (AddressId, CustomerId, AddressLine1, City, Region, PostalCode) SELECT 4, 99999, 'Orphan Address', 'Nowhere', 'XX', '00000' WHERE NOT EXISTS (SELECT 1 FROM dbo.Addresses WHERE AddressId = 4);

        -- Orders (CustomerId NVARCHAR - orphan 'NoSuchCustomer', '99999'; OrderStatus inconsistent: Pending, pending, SHIPPED, shipped)
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 1, 'ORD-001', '1', 'Pending', 79.98, '2024-01-10 10:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 1);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 2, 'ORD-002', '2', 'pending', 129.98, '2024-01-12 14:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 2);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 3, 'ORD-003', 'NoSuchCustomer', 'SHIPPED', 49.99, '2024-01-15 09:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 3);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 4, 'ORD-004', '3', 'shipped', 199.99, '2024-01-18 11:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 4);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 5, 'ORD-005', '99999', 'Delivered', 29.99, '2024-01-20 08:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 5);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 6, 'ORD-006', '4', ' Pending ', 89.97, '2024-01-22 16:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 6);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 7, 'ORD-007', '5', 'CANCELLED', 0, '2024-01-25 10:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 7);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 8, 'ORD-008', '6', 'Processing', 159.98, '2024-02-01 09:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 8);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 9, 'ORD-009', '7', 'processing', 74.99, '2024-02-05 13:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 9);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 10, 'ORD-010', '8', 'Shipped', 299.99, '2024-02-08 11:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 10);

        -- OrderItems (orphan ProductId 99999, 88888)
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 1, 1, 1, 2, 29.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 1);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 2, 1, 2, 1, 19.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 2);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 3, 2, 99999, 1, 49.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 3);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 4, 2, 3, 1, 79.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 4);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 5, 3, 2, 1, 49.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 5);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 6, 4, 9, 1, 199.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 6);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 7, 5, 88888, 1, 29.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 7);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 8, 6, 1, 3, 29.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 8);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 9, 8, 2, 2, 49.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 9);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 10, 8, 6, 1, 45.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 10);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 11, 9, 7, 5, 9.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 11);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 12, 10, 10, 1, 299.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 12);

        -- Payments (orphan OrderNumber ORD-missing)
        INSERT INTO dbo.Payments (PaymentId, OrderNumber, CardLast4, CardType, CashAmount, PaidAt) SELECT 1, 'ORD-001', '4242', 'Visa', 79.98, '2024-01-10 10:05:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentId = 1);
        INSERT INTO dbo.Payments (PaymentId, OrderNumber, CardLast4, CardType, CashAmount, PaidAt) SELECT 2, 'ORD-002', '1234', 'Mastercard', 129.98, '2024-01-12 14:10:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentId = 2);
        INSERT INTO dbo.Payments (PaymentId, OrderNumber, CardLast4, CardType, CashAmount, PaidAt) SELECT 3, 'ORD-missing', '9999', 'Amex', 500.00, '2024-01-20 12:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentId = 3);
        INSERT INTO dbo.Payments (PaymentId, OrderNumber, CardLast4, CardType, CashAmount, PaidAt) SELECT 4, 'ORD-004', '5678', 'Visa', 199.99, '2024-01-18 11:30:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentId = 4);
        INSERT INTO dbo.Payments (PaymentId, OrderNumber, CardLast4, CardType, CashAmount, PaidAt) SELECT 5, 'ORD-008', '2468', 'Discover', 159.98, '2024-02-01 09:15:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentId = 5);

        -- Shipments (orphan OrderNumber ORD-missing; inconsistent Carrier: UPS, ups, FedEx; ShipmentStatus mixed case)
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 1, 'ORD-001', '2024-01-11 08:00:00', 'UPS', 'Delivered' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 1);
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 2, 'ORD-002', '2024-01-13 09:00:00', 'ups', 'delivered' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 2);
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 3, 'ORD-003', '2024-01-16 10:00:00', 'FedEx', 'In Transit' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 3);
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 4, 'ORD-004', '2024-01-19 11:00:00', 'fedex', 'IN_TRANSIT' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 4);
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 5, 'ORD-missing', '2024-02-01 08:00:00', 'USPS', 'Pending' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 5);

        -- StockLevels
        INSERT INTO dbo.StockLevels (StockLevelId, ProductId, WarehouseId, Quantity, LastUpdated) SELECT 1, 1, 1, 500, '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels WHERE StockLevelId = 1);
        INSERT INTO dbo.StockLevels (StockLevelId, ProductId, WarehouseId, Quantity, LastUpdated) SELECT 2, 2, 1, 300, '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels WHERE StockLevelId = 2);
        INSERT INTO dbo.StockLevels (StockLevelId, ProductId, WarehouseId, Quantity, LastUpdated) SELECT 3, 1, 2, 200, '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels WHERE StockLevelId = 3);
        INSERT INTO dbo.StockLevels (StockLevelId, ProductId, WarehouseId, Quantity, LastUpdated) SELECT 4, 3, 1, 150, '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StockLevels WHERE StockLevelId = 4);

        -- InventoryMovements
        INSERT INTO dbo.InventoryMovements (MovementId, ProductId, Quantity, MovementType, CreatedAt, WarehouseId) SELECT 1, 1, 100, 'IN', '2024-01-05 10:00:00', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryMovements WHERE MovementId = 1);
        INSERT INTO dbo.InventoryMovements (MovementId, ProductId, Quantity, MovementType, CreatedAt, WarehouseId) SELECT 2, 1, -5, 'OUT', '2024-01-10 11:00:00', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryMovements WHERE MovementId = 2);
        INSERT INTO dbo.InventoryMovements (MovementId, ProductId, Quantity, MovementType, CreatedAt, WarehouseId) SELECT 3, 2, 50, 'IN', '2024-01-08 09:00:00', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryMovements WHERE MovementId = 3);
        INSERT INTO dbo.InventoryMovements (MovementId, ProductId, Quantity, MovementType, CreatedAt, WarehouseId) SELECT 4, 3, 75, 'TRANSFER', '2024-01-12 14:00:00', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryMovements WHERE MovementId = 4);

        -- AuditLogs (heap)
        IF (SELECT COUNT(*) FROM dbo.AuditLogs) = 0
        INSERT INTO dbo.AuditLogs (TableName, Action, ChangedAt, UserId) VALUES ('Orders', 'INSERT', '2024-01-10', 1), ('Products', 'UPDATE', '2024-01-11', 2), ('Customers', 'INSERT', '2024-01-12', 99999);

        -- EventStream (heap)
        IF (SELECT COUNT(*) FROM dbo.EventStream) = 0
        INSERT INTO dbo.EventStream (EventType, OccurredAt, Payload) VALUES ('OrderCreated', '2024-01-10', '{}'), ('ShipmentSent', '2024-01-11', '{}');

        -- StagingOrderImport (heap)
        INSERT INTO dbo.StagingOrderImport (ExternalOrderId, CustomerEmail, OrderTotal, ImportBatch) SELECT 'EXT-001', 'import@test.com', 99.99, 'BATCH-2024-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StagingOrderImport WHERE ExternalOrderId = 'EXT-001' AND ImportBatch = 'BATCH-2024-01');

        -- LegacyInventorySnapshot (extreme nullable - 7 cols, 6 nullable)
        INSERT INTO dbo.LegacyInventorySnapshot (SnapshotId, ProductId, WarehouseId, QtyOnHand, QtyReserved, LastCountDate, Notes, SourceSystem) SELECT 1, 1, 1, 500, 10, '2024-01-31', 'Counted', 'Legacy' WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyInventorySnapshot WHERE SnapshotId = 1);
        INSERT INTO dbo.LegacyInventorySnapshot (SnapshotId, ProductId, WarehouseId, QtyOnHand, QtyReserved, LastCountDate, Notes, SourceSystem) SELECT 2, NULL, NULL, NULL, NULL, NULL, NULL, NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyInventorySnapshot WHERE SnapshotId = 2);

        -- PriceHistory
        INSERT INTO dbo.PriceHistory (PriceHistoryId, ProductId, OldPrice, NewPrice, EffectiveDate) SELECT 1, 1, 27.99, 29.99, '2024-01-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.PriceHistory WHERE PriceHistoryId = 1);
        INSERT INTO dbo.PriceHistory (PriceHistoryId, ProductId, OldPrice, NewPrice, EffectiveDate) SELECT 2, 2, 45.99, 49.99, '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.PriceHistory WHERE PriceHistoryId = 2);

        -- Promotions (ExpiryDate < ActiveDate for BrokenBusinessRules)
        INSERT INTO dbo.Promotions (PromotionId, PromoCode, ActiveDate, ExpiryDate, DiscountPercent) SELECT 1, 'SAVE10', '2024-01-15', '2024-01-31', 10.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.Promotions WHERE PromotionId = 1);
        INSERT INTO dbo.Promotions (PromotionId, PromoCode, ActiveDate, ExpiryDate, DiscountPercent) SELECT 2, 'BROKEN', '2024-02-15', '2024-02-01', 15.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.Promotions WHERE PromotionId = 2);
        INSERT INTO dbo.Promotions (PromotionId, PromoCode, ActiveDate, ExpiryDate, DiscountPercent) SELECT 3, 'SPRING', '2024-03-01', '2024-03-31', 20.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.Promotions WHERE PromotionId = 3);
        INSERT INTO dbo.Promotions (PromotionId, PromoCode, ActiveDate, ExpiryDate, DiscountPercent) SELECT 4, 'BAD', '2024-04-10', '2024-04-05', 5.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.Promotions WHERE PromotionId = 4);

        -- RefundRequests (no date pair for BrokenBusinessRules - different pattern)
        INSERT INTO dbo.RefundRequests (RefundId, OrderNumber, RequestedAt, ProcessedAt, Status) SELECT 1, 'ORD-001', '2024-01-25', '2024-01-26', 'Processed' WHERE NOT EXISTS (SELECT 1 FROM dbo.RefundRequests WHERE RefundId = 1);

        -- PaymentAdjustments (PaidAmount > TotalAmount for BrokenBusinessRules)
        INSERT INTO dbo.PaymentAdjustments (AdjustmentId, OrderNumber, PaidAmount, TotalAmount, Reason, AdjustedAt) SELECT 1, 'ORD-001', 79.98, 79.98, 'Correct', '2024-01-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentAdjustments WHERE AdjustmentId = 1);
        INSERT INTO dbo.PaymentAdjustments (AdjustmentId, OrderNumber, PaidAmount, TotalAmount, Reason, AdjustedAt) SELECT 2, 'ORD-002', 150.00, 129.98, 'Overpayment', '2024-01-12' WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentAdjustments WHERE AdjustmentId = 2);
        INSERT INTO dbo.PaymentAdjustments (AdjustmentId, OrderNumber, PaidAmount, TotalAmount, Reason, AdjustedAt) SELECT 3, 'ORD-006', 100.00, 89.97, 'Rounding error', '2024-01-22' WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentAdjustments WHERE AdjustmentId = 3);

        -- OrderPromoJunction (junction with OrderId+PromoId, single PK - SuspectedJunctionMissingKey)
        INSERT INTO dbo.OrderPromoJunction (OrderPromoId, OrderId, PromoId) SELECT 1, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderPromoJunction WHERE OrderPromoId = 1);
        INSERT INTO dbo.OrderPromoJunction (OrderPromoId, OrderId, PromoId) SELECT 2, 2, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderPromoJunction WHERE OrderPromoId = 2);
        INSERT INTO dbo.OrderPromoJunction (OrderPromoId, OrderId, PromoId) SELECT 3, 6, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderPromoJunction WHERE OrderPromoId = 3);
        INSERT INTO dbo.OrderPromoJunction (OrderPromoId, OrderId, PromoId) SELECT 4, 99999, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderPromoJunction WHERE OrderPromoId = 4);

        -- Additional scale: more customers, orders, products
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 11, 'kate@retail.com', '555-0110', 'Kate Moore', 'Austin', '78701' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 11);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 12, 'leo@retail.com', '555-0111', 'Leo Martinez', 'Jacksonville', '32099' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 12);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 13, 'mia@retail.com', '555-0112', 'Mia Taylor', 'Fort Worth', '76101' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 13);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 14, 'noah@retail.com', '555-0113', 'Noah Anderson', 'Columbus', '43201' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 14);
        INSERT INTO dbo.Customers (CustomerId, Email, Phone, FullName, City, PostalCode) SELECT 15, 'olivia@retail.com', '555-0114', 'Olivia Garcia', 'Charlotte', '28201' WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerId = 15);

        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 11, 'ORD-011', '9', 'Delivered', 54.98, '2024-02-10 10:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 11);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 12, 'ORD-012', '10', 'Returned', 199.99, '2024-02-12 14:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 12);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 13, 'ORD-013', '11', 'returned', 74.99, '2024-02-14 09:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 13);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 14, 'ORD-014', '12', 'DELIVERED', 129.97, '2024-02-16 11:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 14);
        INSERT INTO dbo.Orders (OrderId, OrderNumber, CustomerId, OrderStatus, TotalAmount, CreatedAt) SELECT 15, 'ORD-015', '13', 'On Hold', 45.00, '2024-02-18 08:00:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = 15);

        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 13, 11, 2, 1, 49.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 13);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 14, 11, 7, 1, 4.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 14);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 15, 12, 9, 1, 199.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 15);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 16, 13, 5, 2, 19.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 16);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 17, 14, 1, 2, 29.99 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 17);
        INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) SELECT 18, 14, 6, 1, 45.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems WHERE OrderItemId = 18);

        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 6, 'ORD-010', '2024-02-09 08:00:00', 'DHL', 'Delivered' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 6);
        INSERT INTO dbo.Shipments (ShipmentId, OrderNumber, ShippedAt, Carrier, ShipmentStatus) SELECT 7, 'ORD-011', '2024-02-11 09:00:00', 'dhl', 'delivered' WHERE NOT EXISTS (SELECT 1 FROM dbo.Shipments WHERE ShipmentId = 7);

        INSERT INTO dbo.PaymentAdjustments (AdjustmentId, OrderNumber, PaidAmount, TotalAmount, Reason, AdjustedAt) SELECT 4, 'ORD-012', 250.00, 199.99, 'Double charge refund', '2024-02-13' WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentAdjustments WHERE AdjustmentId = 4);
        """;
}
