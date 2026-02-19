namespace SqlDiagTool.Demo;

public static class ManufacturingOpsIndustrialSeed
{
    public static string GetSeedSql() => """
        -- Drop all tables first for clean schema
        DROP TABLE IF EXISTS dbo.WorkOrderAssignments;
        DROP TABLE IF EXISTS dbo.CostOverruns;
        DROP TABLE IF EXISTS dbo.MachineDowntimeEvents;
        DROP TABLE IF EXISTS dbo.LegacyMachineReadings;
        DROP TABLE IF EXISTS dbo.StagingProductionImport;
        DROP TABLE IF EXISTS dbo.ManufacturingEventLog;
        DROP TABLE IF EXISTS dbo.QualityInspections;
        DROP TABLE IF EXISTS dbo.ProductionRuns;
        DROP TABLE IF EXISTS dbo.MaintenanceRecords;
        DROP TABLE IF EXISTS dbo.WorkOrders;
        DROP TABLE IF EXISTS dbo.BillOfMaterials;
        DROP TABLE IF EXISTS dbo.Parts;
        DROP TABLE IF EXISTS dbo.PartCategories;
        DROP TABLE IF EXISTS dbo.Machines;
        DROP TABLE IF EXISTS dbo.WorkCenters;
        DROP TABLE IF EXISTS dbo.Operators;

        -- ========== CORE ENTITY TABLES ==========
        CREATE TABLE dbo.WorkCenters (WorkCenterId INT PRIMARY KEY, WorkCenterCode NVARCHAR(20), Name NVARCHAR(200), Location NVARCHAR(100));

        CREATE TABLE dbo.Machines (MachineId INT PRIMARY KEY, SerialNumber NVARCHAR(50), MachineType NVARCHAR(100), MachineStatus NVARCHAR(50), WorkCenterId INT, LastCalibration DATE);

        CREATE TABLE dbo.Operators (OperatorId INT PRIMARY KEY, Name NVARCHAR(200), BadgeNumber NVARCHAR(20), Shift NVARCHAR(20));

        CREATE TABLE dbo.PartCategories (CategoryId INT PRIMARY KEY, CategoryCode NVARCHAR(20), Name NVARCHAR(100));

        CREATE TABLE dbo.Parts (PartId INT PRIMARY KEY, PartNumber NVARCHAR(50), Name NVARCHAR(200), UnitCost FLOAT, CategoryId INT);

        -- BillOfMaterials: junction with ParentPartId+ChildPartId, single PK (SuspectedJunctionMissingKey)
        CREATE TABLE dbo.BillOfMaterials (BomId INT PRIMARY KEY, ParentPartId INT, ChildPartId INT, QuantityPerAssembly FLOAT);

        -- WorkOrders: MachineId NVARCHAR vs Machines.MachineId INT (ForeignKeyTypeMismatch)
        CREATE TABLE dbo.WorkOrders (WorkOrderId INT PRIMARY KEY, JobCode NVARCHAR(50), PartId INT, MachineId NVARCHAR(50), Quantity INT, WorkOrderStatus NVARCHAR(50), StartDate DATE, EndDate DATE, ActualStartDate DATETIME2, ActualEndDate DATETIME2, LaborCost FLOAT);

        CREATE TABLE dbo.MaintenanceRecords (MaintenanceId INT PRIMARY KEY, MachineId INT, StartDate DATE, EndDate DATE, EstimatedHours FLOAT, ActualHours FLOAT, Notes NVARCHAR(500));

        CREATE TABLE dbo.ProductionRuns (RunId INT PRIMARY KEY, WorkOrderId INT, QuantityProduced INT, RunDate DATE, ScrapCount INT);

        CREATE TABLE dbo.QualityInspections (InspectionId INT PRIMARY KEY, WorkOrderId INT NULL, QualityGrade NVARCHAR(10), InspectedAt DATETIME2, PassFail NVARCHAR(20));

        -- MachineDowntimeEvents: StartTime/EndTime for BrokenBusinessRules (EndTime < StartTime)
        CREATE TABLE dbo.MachineDowntimeEvents (EventId INT PRIMARY KEY, MachineId INT, StartTime DATETIME2, EndTime DATETIME2, ReasonCode NVARCHAR(50));

        -- CostOverruns: PaidAmount > TotalAmount for BrokenBusinessRules
        CREATE TABLE dbo.CostOverruns (OverrunId INT PRIMARY KEY, WorkOrderId INT, PaidAmount FLOAT, TotalAmount FLOAT, VarianceReason NVARCHAR(200), RecordedAt DATETIME2);

        -- ManufacturingEventLog: heap
        CREATE TABLE dbo.ManufacturingEventLog (SeqNum BIGINT IDENTITY, EventType NVARCHAR(50), OccurredAt DATETIME2, MachineId INT, Payload NVARCHAR(MAX));

        -- StagingProductionImport: heap
        CREATE TABLE dbo.StagingProductionImport (ExternalRunId NVARCHAR(100), PartNumber NVARCHAR(50), Quantity INT, ImportBatch NVARCHAR(50), ProcessedAt DATETIME2);

        -- LegacyMachineReadings: extreme nullable (>50% columns)
        CREATE TABLE dbo.LegacyMachineReadings (ReadingId INT PRIMARY KEY, MachineId INT NULL, TempReading FLOAT NULL, PressureReading FLOAT NULL, VibrationLevel FLOAT NULL, ReadAt DATETIME2 NULL, SensorId NVARCHAR(50) NULL, Notes NVARCHAR(500) NULL);

        -- WorkOrderAssignments: junction with WorkOrderId+OperatorId, single PK (SuspectedJunctionMissingKey)
        CREATE TABLE dbo.WorkOrderAssignments (AssignmentId INT PRIMARY KEY, WorkOrderId INT, OperatorId INT, AssignedAt DATETIME2);
        -- __BATCH__
        -- ========== SEED DATA ==========
        -- WorkCenters
        INSERT INTO dbo.WorkCenters (WorkCenterId, WorkCenterCode, Name, Location) SELECT 1, 'WC-A01', 'Assembly Line A', 'Building 1' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkCenters WHERE WorkCenterId = 1);
        INSERT INTO dbo.WorkCenters (WorkCenterId, WorkCenterCode, Name, Location) SELECT 2, 'WC-B02', 'CNC Machining', 'Building 2' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkCenters WHERE WorkCenterId = 2);
        INSERT INTO dbo.WorkCenters (WorkCenterId, WorkCenterCode, Name, Location) SELECT 3, 'WC-A01', 'Assembly Line A2', 'Building 1' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkCenters WHERE WorkCenterId = 3);

        -- Machines (duplicate SerialNumber; inconsistent MachineStatus)
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 1, 'SN-1001', 'CNC Lathe', 'Running', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 1);
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 2, 'SN-1002', 'Milling Machine', 'running', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 2);
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 3, 'SN-1003', 'Press', 'STOPPED', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 3);
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 4, 'SN-1001', 'CNC Duplicate', 'Idle', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 4);
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 5, 'SN-1005', 'Grinder', 'idle', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 5);
        INSERT INTO dbo.Machines (MachineId, SerialNumber, MachineType, MachineStatus, WorkCenterId) SELECT 6, 'SN-1006', 'Conveyor', 'MAINTENANCE', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Machines WHERE MachineId = 6);

        -- Operators
        INSERT INTO dbo.Operators (OperatorId, Name, BadgeNumber, Shift) SELECT 1, 'Carlos Mendez', 'OP-001', 'Day' WHERE NOT EXISTS (SELECT 1 FROM dbo.Operators WHERE OperatorId = 1);
        INSERT INTO dbo.Operators (OperatorId, Name, BadgeNumber, Shift) SELECT 2, 'Sarah Chen', 'OP-002', 'Night' WHERE NOT EXISTS (SELECT 1 FROM dbo.Operators WHERE OperatorId = 2);
        INSERT INTO dbo.Operators (OperatorId, Name, BadgeNumber, Shift) SELECT 3, 'Mike Johnson', 'OP-003', 'day' WHERE NOT EXISTS (SELECT 1 FROM dbo.Operators WHERE OperatorId = 3);
        INSERT INTO dbo.Operators (OperatorId, Name, BadgeNumber, Shift) SELECT 4, 'Lisa Park', 'OP-004', 'NIGHT' WHERE NOT EXISTS (SELECT 1 FROM dbo.Operators WHERE OperatorId = 4);

        -- PartCategories (duplicate CategoryCode)
        INSERT INTO dbo.PartCategories (CategoryId, CategoryCode, Name) SELECT 1, 'RAW', 'Raw Materials' WHERE NOT EXISTS (SELECT 1 FROM dbo.PartCategories WHERE CategoryId = 1);
        INSERT INTO dbo.PartCategories (CategoryId, CategoryCode, Name) SELECT 2, 'WIP', 'Work in Progress' WHERE NOT EXISTS (SELECT 1 FROM dbo.PartCategories WHERE CategoryId = 2);
        INSERT INTO dbo.PartCategories (CategoryId, CategoryCode, Name) SELECT 3, 'RAW', 'Raw Stock' WHERE NOT EXISTS (SELECT 1 FROM dbo.PartCategories WHERE CategoryId = 3);
        INSERT INTO dbo.PartCategories (CategoryId, CategoryCode, Name) SELECT 4, 'FG', 'Finished Goods' WHERE NOT EXISTS (SELECT 1 FROM dbo.PartCategories WHERE CategoryId = 4);

        -- Parts (duplicate PartNumber; UnitCost FLOAT)
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 1, 'PN-1001', 'Shaft Assembly', 45.50, 4 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 1);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 2, 'PN-1002', 'Bearing Housing', 28.00, 4 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 2);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 3, 'PN-1003', 'Steel Rod 2m', 12.50, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 3);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 4, 'PN-1001', 'Shaft Assy Rev2', 48.00, 4 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 4);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 5, 'PN-1005', 'Gear Blank', 35.75, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 5);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 6, 'PN-1006', 'Seal Kit', 8.25, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 6);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 7, 'PN-1007', 'Motor Mount', 22.00, 4 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 7);
        INSERT INTO dbo.Parts (PartId, PartNumber, Name, UnitCost, CategoryId) SELECT 8, 'PN-1008', 'Bolt M8x40', 0.45, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Parts WHERE PartId = 8);

        -- BillOfMaterials (junction)
        INSERT INTO dbo.BillOfMaterials (BomId, ParentPartId, ChildPartId, QuantityPerAssembly) SELECT 1, 1, 2, 1.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterials WHERE BomId = 1);
        INSERT INTO dbo.BillOfMaterials (BomId, ParentPartId, ChildPartId, QuantityPerAssembly) SELECT 2, 1, 3, 2.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterials WHERE BomId = 2);
        INSERT INTO dbo.BillOfMaterials (BomId, ParentPartId, ChildPartId, QuantityPerAssembly) SELECT 3, 1, 6, 4.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterials WHERE BomId = 3);
        INSERT INTO dbo.BillOfMaterials (BomId, ParentPartId, ChildPartId, QuantityPerAssembly) SELECT 4, 2, 5, 1.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterials WHERE BomId = 4);
        INSERT INTO dbo.BillOfMaterials (BomId, ParentPartId, ChildPartId, QuantityPerAssembly) SELECT 5, 99999, 1, 1.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterials WHERE BomId = 5);

        -- WorkOrders (orphan MachineId; JobCode duplicate; inconsistent WorkOrderStatus; EndDate < StartDate; ActualEndDate < ActualStartDate)
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 1, 'WO-2024-001', 1, '1', 100, 'Completed', '2024-01-10', '2024-01-15', '2024-01-10 08:00', '2024-01-15 16:00', 1200.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 1);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 2, 'WO-2024-002', 2, '2', 50, 'completed', '2024-01-12', '2024-01-18', '2024-01-12 07:00', '2024-01-18 15:00', 800.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 2);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 3, 'WO-2024-003', 1, '99999', 25, 'IN_PROGRESS', '2024-01-20', '2024-01-15', '2024-01-20 09:00', '2024-01-19 14:00', 450.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 3);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 4, 'WO-2024-001', 3, '1', 200, 'Queued', '2024-01-25', '2024-01-30', NULL, NULL, 0 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 4);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 5, 'WO-2024-005', 4, '3', 75, 'CANCELLED', '2024-02-01', '2024-02-05', '2024-02-01 08:00', '2024-01-31 12:00', 600.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 5);

        -- MaintenanceRecords (EndDate < StartDate)
        INSERT INTO dbo.MaintenanceRecords (MaintenanceId, MachineId, StartDate, EndDate, EstimatedHours, ActualHours, Notes) SELECT 1, 1, '2024-01-05', '2024-01-06', 4.0, 3.5, 'Routine service' WHERE NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WHERE MaintenanceId = 1);
        INSERT INTO dbo.MaintenanceRecords (MaintenanceId, MachineId, StartDate, EndDate, EstimatedHours, ActualHours, Notes) SELECT 2, 2, '2024-01-15', '2024-01-10', 2.0, 2.5, 'Bearings replaced' WHERE NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WHERE MaintenanceId = 2);
        INSERT INTO dbo.MaintenanceRecords (MaintenanceId, MachineId, StartDate, EndDate, EstimatedHours, ActualHours, Notes) SELECT 3, 99999, '2024-01-20', '2024-01-22', 8.0, 10.0, 'Orphan machine' WHERE NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WHERE MaintenanceId = 3);
        INSERT INTO dbo.MaintenanceRecords (MaintenanceId, MachineId, StartDate, EndDate, EstimatedHours, ActualHours, Notes) SELECT 4, 3, '2024-02-01', '2024-01-28', 1.0, 1.0, 'Calibration' WHERE NOT EXISTS (SELECT 1 FROM dbo.MaintenanceRecords WHERE MaintenanceId = 4);

        -- ProductionRuns
        INSERT INTO dbo.ProductionRuns (RunId, WorkOrderId, QuantityProduced, RunDate, ScrapCount) SELECT 1, 1, 98, '2024-01-15', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductionRuns WHERE RunId = 1);
        INSERT INTO dbo.ProductionRuns (RunId, WorkOrderId, QuantityProduced, RunDate, ScrapCount) SELECT 2, 2, 50, '2024-01-18', 0 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductionRuns WHERE RunId = 2);
        INSERT INTO dbo.ProductionRuns (RunId, WorkOrderId, QuantityProduced, RunDate, ScrapCount) SELECT 3, 99999, 10, '2024-01-20', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductionRuns WHERE RunId = 3);

        -- QualityInspections (inconsistent QualityGrade, PassFail; nullable WorkOrderId)
        INSERT INTO dbo.QualityInspections (InspectionId, WorkOrderId, QualityGrade, InspectedAt, PassFail) SELECT 1, 1, 'A', '2024-01-15 17:00', 'Pass' WHERE NOT EXISTS (SELECT 1 FROM dbo.QualityInspections WHERE InspectionId = 1);
        INSERT INTO dbo.QualityInspections (InspectionId, WorkOrderId, QualityGrade, InspectedAt, PassFail) SELECT 2, 2, 'a', '2024-01-18 16:00', 'pass' WHERE NOT EXISTS (SELECT 1 FROM dbo.QualityInspections WHERE InspectionId = 2);
        INSERT INTO dbo.QualityInspections (InspectionId, WorkOrderId, QualityGrade, InspectedAt, PassFail) SELECT 3, NULL, 'B+', '2024-01-20 10:00', 'FAIL' WHERE NOT EXISTS (SELECT 1 FROM dbo.QualityInspections WHERE InspectionId = 3);
        INSERT INTO dbo.QualityInspections (InspectionId, WorkOrderId, QualityGrade, InspectedAt, PassFail) SELECT 4, 1, 'A-', '2024-01-16 09:00', ' Pass ' WHERE NOT EXISTS (SELECT 1 FROM dbo.QualityInspections WHERE InspectionId = 4);

        -- MachineDowntimeEvents (EndTime < StartTime)
        INSERT INTO dbo.MachineDowntimeEvents (EventId, MachineId, StartTime, EndTime, ReasonCode) SELECT 1, 1, '2024-01-05 08:00:00', '2024-01-05 12:00:00', 'MAINT' WHERE NOT EXISTS (SELECT 1 FROM dbo.MachineDowntimeEvents WHERE EventId = 1);
        INSERT INTO dbo.MachineDowntimeEvents (EventId, MachineId, StartTime, EndTime, ReasonCode) SELECT 2, 2, '2024-01-12 14:00:00', '2024-01-12 13:00:00', 'BREAKDOWN' WHERE NOT EXISTS (SELECT 1 FROM dbo.MachineDowntimeEvents WHERE EventId = 2);
        INSERT INTO dbo.MachineDowntimeEvents (EventId, MachineId, StartTime, EndTime, ReasonCode) SELECT 3, 3, '2024-01-20 09:00:00', '2024-01-20 08:30:00', 'SETUP' WHERE NOT EXISTS (SELECT 1 FROM dbo.MachineDowntimeEvents WHERE EventId = 3);

        -- CostOverruns (PaidAmount > TotalAmount)
        INSERT INTO dbo.CostOverruns (OverrunId, WorkOrderId, PaidAmount, TotalAmount, VarianceReason, RecordedAt) SELECT 1, 1, 1200.00, 1200.00, 'On budget', '2024-01-16' WHERE NOT EXISTS (SELECT 1 FROM dbo.CostOverruns WHERE OverrunId = 1);
        INSERT INTO dbo.CostOverruns (OverrunId, WorkOrderId, PaidAmount, TotalAmount, VarianceReason, RecordedAt) SELECT 2, 2, 950.00, 800.00, 'Overtime labor', '2024-01-19' WHERE NOT EXISTS (SELECT 1 FROM dbo.CostOverruns WHERE OverrunId = 2);
        INSERT INTO dbo.CostOverruns (OverrunId, WorkOrderId, PaidAmount, TotalAmount, VarianceReason, RecordedAt) SELECT 3, 3, 600.00, 450.00, 'Rework costs', '2024-01-21' WHERE NOT EXISTS (SELECT 1 FROM dbo.CostOverruns WHERE OverrunId = 3);

        -- ManufacturingEventLog (heap)
        IF (SELECT COUNT(*) FROM dbo.ManufacturingEventLog) = 0
        INSERT INTO dbo.ManufacturingEventLog (EventType, OccurredAt, MachineId) VALUES ('StartCycle', '2024-01-10 08:00', 1), ('EndCycle', '2024-01-10 16:00', 1), ('Alarm', '2024-01-12 14:00', 2);

        -- StagingProductionImport (heap)
        INSERT INTO dbo.StagingProductionImport (ExternalRunId, PartNumber, Quantity, ImportBatch) SELECT 'EXT-RUN-001', 'PN-1001', 50, 'BATCH-2024-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StagingProductionImport WHERE ExternalRunId = 'EXT-RUN-001' AND ImportBatch = 'BATCH-2024-01');

        -- LegacyMachineReadings (extreme nullable)
        INSERT INTO dbo.LegacyMachineReadings (ReadingId, MachineId, TempReading, PressureReading, VibrationLevel, ReadAt, SensorId, Notes) SELECT 1, 1, 72.5, 14.2, 0.02, '2024-01-15 10:00', 'TEMP-01', 'Normal' WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyMachineReadings WHERE ReadingId = 1);
        INSERT INTO dbo.LegacyMachineReadings (ReadingId, MachineId, TempReading, PressureReading, VibrationLevel, ReadAt, SensorId, Notes) SELECT 2, NULL, NULL, NULL, NULL, NULL, NULL, NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyMachineReadings WHERE ReadingId = 2);

        -- WorkOrderAssignments (junction)
        INSERT INTO dbo.WorkOrderAssignments (AssignmentId, WorkOrderId, OperatorId, AssignedAt) SELECT 1, 1, 1, '2024-01-10 07:30' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrderAssignments WHERE AssignmentId = 1);
        INSERT INTO dbo.WorkOrderAssignments (AssignmentId, WorkOrderId, OperatorId, AssignedAt) SELECT 2, 1, 2, '2024-01-10 15:30' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrderAssignments WHERE AssignmentId = 2);
        INSERT INTO dbo.WorkOrderAssignments (AssignmentId, WorkOrderId, OperatorId, AssignedAt) SELECT 3, 2, 3, '2024-01-12 06:30' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrderAssignments WHERE AssignmentId = 3);
        INSERT INTO dbo.WorkOrderAssignments (AssignmentId, WorkOrderId, OperatorId, AssignedAt) SELECT 4, 99999, 1, '2024-01-20 08:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrderAssignments WHERE AssignmentId = 4);

        -- Additional scale
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 6, 'WO-2024-006', 5, '2', 120, 'Completed', '2024-02-05', '2024-02-10', '2024-02-05 08:00', '2024-02-10 16:00', 900.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 6);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 7, 'WO-2024-007', 6, '1', 500, 'In Progress', '2024-02-12', '2024-02-15', '2024-02-12 07:00', NULL, 0 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 7);
        INSERT INTO dbo.WorkOrders (WorkOrderId, JobCode, PartId, MachineId, Quantity, WorkOrderStatus, StartDate, EndDate, ActualStartDate, ActualEndDate, LaborCost) SELECT 8, 'WO-2024-008', 7, '4', 80, 'in progress', '2024-02-14', '2024-02-18', NULL, NULL, 0 WHERE NOT EXISTS (SELECT 1 FROM dbo.WorkOrders WHERE WorkOrderId = 8);
        INSERT INTO dbo.CostOverruns (OverrunId, WorkOrderId, PaidAmount, TotalAmount, VarianceReason, RecordedAt) SELECT 4, 6, 1100.00, 900.00, 'Material price increase', '2024-02-11' WHERE NOT EXISTS (SELECT 1 FROM dbo.CostOverruns WHERE OverrunId = 4);
        """;
}
