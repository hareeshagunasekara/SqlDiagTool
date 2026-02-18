namespace SqlDiagTool.Demo;

// Full T-SQL seed for MedTrack_Clinical: 12 tables with intentional design issues (single batch, no GO).
public static class MedTrackClinicalSeed
{
    public static string GetSeedSql() => """
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Patients')
        CREATE TABLE dbo.Patients (PatientId INT PRIMARY KEY, NIC NVARCHAR(50), Gender NVARCHAR(50), FullName NVARCHAR(200), DateOfBirth DATE, Email NVARCHAR(255), Phone NVARCHAR(20));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Doctors')
        CREATE TABLE dbo.Doctors (DoctorId INT PRIMARY KEY, Name NVARCHAR(200), Specialty NVARCHAR(100), LicenseNumber NVARCHAR(50));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Appointments')
        CREATE TABLE dbo.Appointments (AppointmentId INT PRIMARY KEY, PatientId INT, DoctorId INT, StartTime DATETIME2, EndTime DATETIME2, Status NVARCHAR(50), Date DATE);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Diagnoses')
        CREATE TABLE dbo.Diagnoses (DiagnosisId INT PRIMARY KEY, PatientId INT, DoctorId INT, DiagnosisCode NVARCHAR(20), Description NVARCHAR(500), DiagnosedDate DATETIME2);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Treatments')
        CREATE TABLE dbo.Treatments (TreatmentId INT PRIMARY KEY, PatientId INT, DiagnosisId INT, Dosage NVARCHAR(100), Duration INT, Frequency NVARCHAR(50), StartDate DATETIME2, EndDate DATETIME2);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Prescriptions')
        CREATE TABLE dbo.Prescriptions (PrescriptionId INT PRIMARY KEY, PatientId INT, DoctorId INT, MedicationName NVARCHAR(200), Dosage NVARCHAR(100), ExpiryDate DATE, ActiveDate DATE);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'MedicalRecords')
        CREATE TABLE dbo.MedicalRecords (RecordId INT PRIMARY KEY, PatientId INT, Notes NVARCHAR(MAX), DiagnosisText NVARCHAR(MAX), LabResults NVARCHAR(MAX), ScanUrls NVARCHAR(MAX), Attachments NVARCHAR(MAX));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'Billing')
        CREATE TABLE dbo.Billing (BillingId INT PRIMARY KEY, PatientId INT, AppointmentId INT, Amount DECIMAL(18,2), Currency NVARCHAR(50), BillingDate DATETIME2);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'InsuranceClaims')
        CREATE TABLE dbo.InsuranceClaims (ClaimId INT PRIMARY KEY, PatientId INT, TreatmentId INT, ClaimDate DATETIME2, Status NVARCHAR(50), Amount DECIMAL(18,2));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'WardAssignments')
        CREATE TABLE dbo.WardAssignments (AssignmentId INT PRIMARY KEY, PatientId INT NULL, WardNumber NVARCHAR(20), AssignedDate DATETIME2, DischargedDate DATETIME2);
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'StaffUsers')
        CREATE TABLE dbo.StaffUsers (UserId INT PRIMARY KEY, Username NVARCHAR(100), FullName NVARCHAR(200), Role NVARCHAR(50), Email NVARCHAR(255));
        IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'SystemLogs')
        CREATE TABLE dbo.SystemLogs (LogId INT PRIMARY KEY, TableName NVARCHAR(128), Action NVARCHAR(20), ChangedAt DATETIME2, UserId INT);
        IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 1)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) VALUES (1, '123456789V', 'Male', 'John Doe', '1980-01-15', 'john.doe@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 2)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) VALUES (2, '987654321V', 'F', 'Jane Smith', '1975-05-22', 'jane.smith@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 3)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) VALUES (3, '555555555V', 'female', 'Mary Johnson', '1990-03-10', 'mary.johnson@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 4)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) VALUES (4, '444444444V', 'M', 'Robert Brown', '1985-07-20', 'robert.brown@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 1)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber) VALUES (1, 'Dr. Alice Johnson', 'Cardiology', 'LIC-12345');
        IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 2)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber) VALUES (2, 'Dr. Bob Williams', 'General Practice', 'LIC-67890');
        IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 3)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber) VALUES (3, 'Dr. Carol Brown', 'CARDIOLOGY', 'LIC-11111');
        IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 4)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber) VALUES (4, 'Dr. David Lee', 'Cardio', 'LIC-22222');
        IF NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 5)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber) VALUES (5, 'Dr. Emma White', 'Heart Specialist', 'LIC-33333');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 1)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (1, 1, 1, '2024-01-15 10:00:00', '2024-01-15 10:30:00', 'Completed', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 2)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (2, 99999, 1, '2024-01-20 14:00:00', '2024-01-20 13:30:00', 'Scheduled', '2024-01-20');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 3)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (3, 1, 2, '2024-01-25 09:00:00', '2024-01-25 09:45:00', 'INVALID_STATUS', '2024-01-25');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 4)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (4, 2, 1, '2024-01-15 10:00:00', '2024-01-15 10:45:00', 'Scheduled', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 5)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (5, 3, 1, '2024-01-15 10:15:00', '2024-01-15 11:00:00', 'Cancelled', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 6)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, StartTime, EndTime, Status, Date) VALUES (6, 1, 1, '2024-01-20 11:00:00', '2024-01-20 10:30:00', 'Pending', '2024-01-20');
        IF NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 1)
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) VALUES (1, 1, 1, 'E11.9', 'Type 2 diabetes mellitus without complications', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 2)
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) VALUES (2, 2, 2, 'I10', 'Essential hypertension', '2024-01-10');
        IF NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 3)
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) VALUES (3, 99999, 1, 'E78.5', 'Hyperlipidemia', '2024-01-12');
        IF NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 1)
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) VALUES (1, 1, 1, '10mg daily', 30, 'Once daily', '2024-01-15', '2024-02-14');
        IF NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 2)
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) VALUES (2, 2, 2, '5mg twice daily', 60, 'BID', '2024-01-10', '2024-03-10');
        IF NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 3)
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) VALUES (3, 3, 1, '20mg daily', 90, 'QD', '2024-01-20', '2024-04-20');
        IF NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 1)
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate) VALUES (1, 1, 1, 'Lisinopril', '10mg', '2024-12-31', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 2)
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate) VALUES (2, 1, 99999, 'Metformin', '500mg', '2024-06-30', '2024-07-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 3)
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate) VALUES (3, 2, 2, 'Amlodipine', '5mg', '2024-05-15', '2024-01-10');
        IF NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 1)
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) VALUES (1, 1, 'Patient reports feeling well', 'Type 2 diabetes', 'HbA1c: 6.2%');
        IF NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 2)
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) VALUES (2, 99999, 'Orphan record - patient does not exist', 'Unknown diagnosis', 'No results');
        IF NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 3)
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults, ScanUrls, Attachments) VALUES (3, 2, 'Follow-up visit', 'Hypertension', 'BP: 140/90', 'https://scans.example.com/scan001.pdf', 'attachment001.pdf');
        IF NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 4)
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) VALUES (4, 88888, 'Another orphan record', 'Unknown', 'N/A');
        IF NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 1)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, Currency, BillingDate) VALUES (1, 1, 1, 150.00, 'USD', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 2)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, Currency, BillingDate) VALUES (2, 2, 99999, 200.00, 'dollars', '2024-01-10');
        IF NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 3)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, Currency, BillingDate) VALUES (3, 3, 3, 175.00, 'US Dollars', '2024-01-25');
        IF NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 4)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, Currency, BillingDate) VALUES (4, 1, 1, 100.00, 'EUR', '2024-01-15');
        IF NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 1)
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) VALUES (1, 1, 1, '2024-01-16', 'Approved', 120.00);
        IF NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 2)
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) VALUES (2, 2, 2, '2024-01-05', 'Pending', 180.00);
        IF NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 3)
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) VALUES (3, 3, 3, '2024-01-15', 'Rejected', 200.00);
        IF NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 1)
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate) VALUES (1, 1, 'WARD-101', '2024-01-15', '2024-01-17');
        IF NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 2)
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate) VALUES (2, NULL, 'WARD-102', '2024-01-20', NULL);
        IF NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 3)
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate) VALUES (3, 2, 'WARD-103', '2024-01-10', '2024-01-12');
        IF NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 1)
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) VALUES (1, 'admin', 'System Administrator', 'Admin', 'admin@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 2)
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) VALUES (2, 'nurse1', 'Nurse Jane', 'Nurse', 'nurse1@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 3)
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) VALUES (3, 'doctor1', 'Dr. Test', 'Doctor', 'doctor1@example.com');
        IF NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 1)
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) VALUES (1, 'Patients', 'INSERT', '2024-01-15 10:00:00', 1);
        IF NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 2)
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) VALUES (2, 'Appointments', 'UPDATE', '2024-01-15 11:00:00', 99999);
        IF NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 3)
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) VALUES (3, 'Prescriptions', 'DELETE', '2024-01-16 09:00:00', 88888);
        IF NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 4)
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) VALUES (4, 'MedicalRecords', 'INSERT', '2024-01-17 14:00:00', NULL);
        """;
}
