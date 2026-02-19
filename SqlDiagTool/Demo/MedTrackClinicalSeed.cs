namespace SqlDiagTool.Demo;

public static class MedTrackClinicalSeed
{
    public static string GetSeedSql() => """
        -- Drop all tables first to ensure fresh schema (avoids "Invalid column name" when schema changed)
        DROP TABLE IF EXISTS dbo.DrugInteractions;
        DROP TABLE IF EXISTS dbo.PatientAllergies;
        DROP TABLE IF EXISTS dbo.ProcedureCatalog;
        DROP TABLE IF EXISTS dbo.LabTests;
        DROP TABLE IF EXISTS dbo.FeeSchedule;
        DROP TABLE IF EXISTS dbo.LegacyPatientNotes;
        DROP TABLE IF EXISTS dbo.PatientProcedureJunction;
        DROP TABLE IF EXISTS dbo.StagingPatientImport;
        DROP TABLE IF EXISTS dbo.AuditTrailRaw;
        DROP TABLE IF EXISTS dbo.EventLog;
        DROP TABLE IF EXISTS dbo.PatientReferrals;
        DROP TABLE IF EXISTS dbo.MedicationCatalog;
        DROP TABLE IF EXISTS dbo.PatientEmergencyContacts;
        DROP TABLE IF EXISTS dbo.InsurancePlans;
        DROP TABLE IF EXISTS dbo.SystemLogs;
        DROP TABLE IF EXISTS dbo.StaffUsers;
        DROP TABLE IF EXISTS dbo.WardAssignments;
        DROP TABLE IF EXISTS dbo.InsuranceClaims;
        DROP TABLE IF EXISTS dbo.Billing;
        DROP TABLE IF EXISTS dbo.MedicalRecords;
        DROP TABLE IF EXISTS dbo.Prescriptions;
        DROP TABLE IF EXISTS dbo.Treatments;
        DROP TABLE IF EXISTS dbo.Diagnoses;
        DROP TABLE IF EXISTS dbo.Appointments;
        DROP TABLE IF EXISTS dbo.Facilities;
        DROP TABLE IF EXISTS dbo.Departments;
        DROP TABLE IF EXISTS dbo.Doctors;
        DROP TABLE IF EXISTS dbo.Patients;

        -- ========== CORE ENTITY TABLES ==========
        CREATE TABLE dbo.Patients (PatientId INT PRIMARY KEY, NIC NVARCHAR(50), Gender NVARCHAR(50), FullName NVARCHAR(200), DateOfBirth DATE, Email NVARCHAR(255), Phone NVARCHAR(20), EmergencyContact NVARCHAR(200), BloodType NVARCHAR(10), InsuranceStatus NVARCHAR(50));

        CREATE TABLE dbo.Doctors (DoctorId INT PRIMARY KEY, Name NVARCHAR(200), Specialty NVARCHAR(100), LicenseNumber NVARCHAR(50), DepartmentId INT, Email NVARCHAR(255));

        CREATE TABLE dbo.Departments (DepartmentId INT PRIMARY KEY, DepartmentCode NVARCHAR(20), Name NVARCHAR(100), HeadDoctorId INT);

        CREATE TABLE dbo.Facilities (FacilityId INT PRIMARY KEY, FacilityCode NVARCHAR(20), Name NVARCHAR(200), Address NVARCHAR(500));

        CREATE TABLE dbo.Appointments (AppointmentId INT PRIMARY KEY, PatientId INT, DoctorId INT, FacilityId NVARCHAR(50), StartTime DATETIME2, EndTime DATETIME2, Status NVARCHAR(50), Date DATE, RoomNumber NVARCHAR(20));

        CREATE TABLE dbo.Diagnoses (DiagnosisId INT PRIMARY KEY, PatientId INT, DoctorId INT, DiagnosisCode NVARCHAR(20), Description NVARCHAR(500), DiagnosedDate DATETIME2, Severity NVARCHAR(20));

        CREATE TABLE dbo.Treatments (TreatmentId INT PRIMARY KEY, PatientId INT, DiagnosisId INT, Dosage NVARCHAR(100), Duration INT, Frequency NVARCHAR(50), StartDate DATETIME2, EndDate DATETIME2);

        CREATE TABLE dbo.Prescriptions (PrescriptionId INT PRIMARY KEY, PatientId INT, DoctorId INT, MedicationName NVARCHAR(200), Dosage NVARCHAR(100), ExpiryDate DATE, ActiveDate DATE, Status NVARCHAR(50));

        CREATE TABLE dbo.MedicalRecords (RecordId INT PRIMARY KEY, PatientId INT, Notes NVARCHAR(MAX), DiagnosisText NVARCHAR(MAX), LabResults NVARCHAR(MAX), ScanUrls NVARCHAR(MAX), Attachments NVARCHAR(MAX), CreatedAt DATETIME2);

        CREATE TABLE dbo.Billing (BillingId INT PRIMARY KEY, PatientId INT, AppointmentId INT, Amount FLOAT, PaidAmount FLOAT, TotalAmount FLOAT, Currency NVARCHAR(50), BillingDate DATETIME2);

        CREATE TABLE dbo.InsuranceClaims (ClaimId INT PRIMARY KEY, PatientId INT, TreatmentId INT, ClaimDate DATETIME2, Status NVARCHAR(50), Amount DECIMAL(18,2));

        CREATE TABLE dbo.WardAssignments (AssignmentId INT PRIMARY KEY, PatientId INT NULL, WardNumber NVARCHAR(20), AssignedDate DATETIME2, DischargedDate DATETIME2, Status NVARCHAR(50));

        CREATE TABLE dbo.StaffUsers (UserId INT PRIMARY KEY, Username NVARCHAR(100), FullName NVARCHAR(200), Role NVARCHAR(50), Email NVARCHAR(255));

        CREATE TABLE dbo.SystemLogs (LogId INT PRIMARY KEY, TableName NVARCHAR(128), Action NVARCHAR(20), ChangedAt DATETIME2, UserId INT);

        CREATE TABLE dbo.InsurancePlans (PlanId INT PRIMARY KEY, PlanCode NVARCHAR(20), Name NVARCHAR(200), Status NVARCHAR(50) CONSTRAINT CK_InsurancePlans_Status CHECK (Status IN ('Active','Inactive','Pending')));

        CREATE TABLE dbo.PatientEmergencyContacts (ContactId INT PRIMARY KEY, PatientId NVARCHAR(50), ContactName NVARCHAR(200), Phone NVARCHAR(20));

        CREATE TABLE dbo.MedicationCatalog (MedicationId INT PRIMARY KEY, Sku NVARCHAR(50), Name NVARCHAR(200), UnitPrice FLOAT);

        CREATE TABLE dbo.EventLog (SeqNum BIGINT IDENTITY, EventType NVARCHAR(50), OccurredAt DATETIME2, Payload NVARCHAR(MAX));

        CREATE TABLE dbo.PatientReferrals (ReferralId INT PRIMARY KEY, PatientId INT, DoctorId INT, Status NVARCHAR(50), ReferredDate DATE);

        CREATE TABLE dbo.AuditTrailRaw (SeqId BIGINT IDENTITY, TableName NVARCHAR(128), Action NVARCHAR(20), ChangedAt DATETIME2, UserId INT, Payload NVARCHAR(MAX));

        CREATE TABLE dbo.StagingPatientImport (ExternalId NVARCHAR(100), FullName NVARCHAR(200), DateOfBirth DATE, ImportBatch NVARCHAR(50), ProcessedAt DATETIME2);

        CREATE TABLE dbo.PatientProcedureJunction (RecordId INT PRIMARY KEY, PatientId INT, ProcedureId INT, AppointmentId INT);

        CREATE TABLE dbo.LegacyPatientNotes (NoteId INT PRIMARY KEY, PatientId INT NULL, NoteText NVARCHAR(MAX) NULL, AuthorId INT NULL, DeptId INT NULL, CreatedAt DATETIME2 NULL, ModifiedAt DATETIME2 NULL, SourceSystem NVARCHAR(100) NULL);

        CREATE TABLE dbo.FeeSchedule (FeeId INT PRIMARY KEY, ProcedureCode NVARCHAR(20), Amount FLOAT, EffectiveDate DATE);

        CREATE TABLE dbo.LabTests (LabTestId INT PRIMARY KEY, PatientId INT, DoctorId INT, TestCode NVARCHAR(20), ResultStatus NVARCHAR(50), TestDate DATETIME2, ResultValue FLOAT);

        CREATE TABLE dbo.ProcedureCatalog (ProcedureId INT PRIMARY KEY, ProcedureCode NVARCHAR(20), Name NVARCHAR(200), DefaultPrice FLOAT);

        CREATE TABLE dbo.PatientAllergies (AllergyId INT PRIMARY KEY, PatientId INT, AllergyCode NVARCHAR(20), Severity NVARCHAR(20), ReportedDate DATE);

        CREATE TABLE dbo.DrugInteractions (Drug1Id INT, Drug2Id INT, Severity NVARCHAR(20));
        -- __BATCH__
        -- ========== SEED DATA ==========
        -- Patients (duplicate emails, inconsistent Gender)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 1, '123456789V', 'Male', 'John Doe', '1980-01-15', 'john.doe@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 1);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 2, '987654321V', 'F', 'Jane Smith', '1975-05-22', 'jane.smith@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 2);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 3, '555555555V', 'female', 'Mary Johnson', '1990-03-10', 'mary.johnson@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 3);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 4, '444444444V', 'M', 'Robert Brown', '1985-07-20', 'robert.brown@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 4);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 5, '333333333V', 'male', 'Susan Davis', '1972-11-08', 'susan.davis@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 5);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 6, '222222222V', 'FEMALE', 'Michael Wilson', '1988-04-25', 'michael.wilson@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 6);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 7, '111111111V', 'M', 'Linda Martinez', '1965-09-12', 'linda.martinez@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 7);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 8, '666666666V', 'Female', 'James Anderson', '1995-02-28', 'duplicate@test.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 8);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 9, '777777777V', 'm', 'Patricia Thomas', '1982-06-14', 'duplicate@test.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 9);

        -- Departments
        INSERT INTO dbo.Departments (DepartmentId, DepartmentCode, Name) SELECT 1, 'CARD', 'Cardiology' WHERE NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = 1);
        INSERT INTO dbo.Departments (DepartmentId, DepartmentCode, Name) SELECT 2, 'GEN', 'General Practice' WHERE NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = 2);
        INSERT INTO dbo.Departments (DepartmentId, DepartmentCode, Name) SELECT 3, 'ORTH', 'Orthopedics' WHERE NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = 3);
        INSERT INTO dbo.Departments (DepartmentId, DepartmentCode, Name) SELECT 4, 'CARD', 'Cardio Unit' WHERE NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = 4);

        -- Facilities (duplicate FacilityCode for DUPLICATE_RECORDS)
        INSERT INTO dbo.Facilities (FacilityId, FacilityCode, Name, Address) SELECT 1, 'MT-01', 'MedTrack Main Campus', '100 Hospital Dr' WHERE NOT EXISTS (SELECT 1 FROM dbo.Facilities WHERE FacilityId = 1);
        INSERT INTO dbo.Facilities (FacilityId, FacilityCode, Name, Address) SELECT 2, 'MT-02', 'MedTrack North', '200 North Ave' WHERE NOT EXISTS (SELECT 1 FROM dbo.Facilities WHERE FacilityId = 2);
        INSERT INTO dbo.Facilities (FacilityId, FacilityCode, Name, Address) SELECT 3, 'MT-01', 'MedTrack Annex', '150 Hospital Dr' WHERE NOT EXISTS (SELECT 1 FROM dbo.Facilities WHERE FacilityId = 3);

        -- Doctors (duplicate LicenseNumber, inconsistent Specialty casing)
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId) SELECT 1, 'Dr. Alice Johnson', 'Cardiology', 'LIC-12345', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 1);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId) SELECT 2, 'Dr. Bob Williams', 'General Practice', 'LIC-67890', 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 2);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId) SELECT 3, 'Dr. Carol Brown', 'CARDIOLOGY', 'LIC-11111', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 3);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId) SELECT 4, 'Dr. David Lee', 'Cardio', 'LIC-22222', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 4);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId) SELECT 5, 'Dr. Emma White', 'Heart Specialist', 'LIC-12345', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 5);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId, Email) SELECT 6, 'Dr. Frank Miller', 'general practice', 'LIC-44444', 2, 'frank@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 6);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId, Email) SELECT 7, 'Dr. Grace Hill', 'Cardiology', 'LIC-55555', 1, 'alice@medtrack.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 7);
        INSERT INTO dbo.Doctors (DoctorId, Name, Specialty, LicenseNumber, DepartmentId, Email) SELECT 8, 'Dr. Henry Young', 'Cardiology', 'LIC-66666', 1, 'alice@medtrack.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Doctors WHERE DoctorId = 8);

        -- Appointments (add one with leading space in Status for INCONSISTENT_FORMATS) (orphans PatientId 99999, 88888; inconsistent Status; EndTime < StartTime; FacilityId type mismatch NVARCHAR vs INT)
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 1, 1, 1, '1', '2024-01-15 10:00:00', '2024-01-15 10:30:00', 'Completed', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 1);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 2, 99999, 1, '1', '2024-01-20 14:00:00', '2024-01-20 13:30:00', 'Scheduled', '2024-01-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 2);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 3, 1, 2, '2', '2024-01-25 09:00:00', '2024-01-25 09:45:00', 'INVALID_STATUS', '2024-01-25' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 3);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 4, 2, 1, '1', '2024-01-15 10:00:00', '2024-01-15 10:45:00', 'Scheduled', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 4);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 5, 3, 1, '1', '2024-01-15 10:15:00', '2024-01-15 11:00:00', 'Cancelled', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 5);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 6, 1, 1, '1', '2024-01-20 11:00:00', '2024-01-20 10:30:00', 'Pending', '2024-01-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 6);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 7, 88888, 2, '2', '2024-02-01 08:00:00', '2024-02-01 07:30:00', 'completed', '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 7);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 8, 4, 3, '1', '2024-02-05 14:00:00', '2024-02-05 14:45:00', 'CANCELLED', '2024-02-05' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 8);

        -- Diagnoses (orphan PatientId 99999)
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 1, 1, 1, 'E11.9', 'Type 2 diabetes mellitus without complications', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 1);
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 2, 2, 2, 'I10', 'Essential hypertension', '2024-01-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 2);
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 3, 99999, 1, 'E78.5', 'Hyperlipidemia', '2024-01-12' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 3);
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 4, 77777, 2, 'J00', 'Acute nasopharyngitis', '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 4);

        -- Treatments
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) SELECT 1, 1, 1, '10mg daily', 30, 'Once daily', '2024-01-15', '2024-02-14' WHERE NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 1);
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) SELECT 2, 2, 2, '5mg twice daily', 60, 'BID', '2024-01-10', '2024-03-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 2);
        INSERT INTO dbo.Treatments (TreatmentId, PatientId, DiagnosisId, Dosage, Duration, Frequency, StartDate, EndDate) SELECT 3, 3, 1, '20mg daily', 90, 'QD', '2024-01-20', '2024-04-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.Treatments WHERE TreatmentId = 3);

        -- Prescriptions (orphan DoctorId 99999, broken ActiveDate > ExpiryDate)
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate, Status) SELECT 1, 1, 1, 'Lisinopril', '10mg', '2024-12-31', '2024-01-15', 'Active' WHERE NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 1);
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate, Status) SELECT 2, 1, 99999, 'Metformin', '500mg', '2024-06-30', '2024-07-15', 'expired' WHERE NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 2);
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate, Status) SELECT 3, 2, 2, 'Amlodipine', '5mg', '2024-05-15', '2024-01-10', 'ACTIVE' WHERE NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 3);
        INSERT INTO dbo.Prescriptions (PrescriptionId, PatientId, DoctorId, MedicationName, Dosage, ExpiryDate, ActiveDate, Status) SELECT 4, 5, 2, 'Ibuprofen', '400mg', '2024-01-05', '2024-02-10', 'Active' WHERE NOT EXISTS (SELECT 1 FROM dbo.Prescriptions WHERE PrescriptionId = 4);

        -- MedicalRecords (orphans)
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 1, 1, 'Patient reports feeling well', 'Type 2 diabetes', 'HbA1c: 6.2%' WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 1);
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 2, 99999, 'Orphan record - patient does not exist', 'Unknown diagnosis', 'No results' WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 2);
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 3, 2, 'Follow-up visit', 'Hypertension', 'BP: 140/90' WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 3);
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 4, 88888, 'Another orphan record', 'Unknown', 'N/A' WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 4);

        -- Billing (orphan AppointmentId 99999; PaidAmount > TotalAmount; Amount as FLOAT)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 1, 1, 1, 150.00, 150.00, 150.00, 'USD', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 1);
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 2, 2, 99999, 200.00, 250.00, 200.00, 'dollars', '2024-01-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 2);
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 3, 3, 3, 175.00, 0, 175.00, 'US Dollars', '2024-01-25' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 3);
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 4, 1, 1, 100.00, 100.00, 100.00, 'EUR', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 4);

        -- InsuranceClaims
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) SELECT 1, 1, 1, '2024-01-16', 'Approved', 120.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 1);
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) SELECT 2, 2, 2, '2024-01-05', 'Pending', 180.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 2);
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) SELECT 3, 3, 3, '2024-01-15', 'Rejected', 200.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 3);
        INSERT INTO dbo.InsuranceClaims (ClaimId, PatientId, TreatmentId, ClaimDate, Status, Amount) SELECT 4, 99999, 1, '2024-02-01', 'pending', 50.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.InsuranceClaims WHERE ClaimId = 4);

        -- WardAssignments (NULL PatientId; DischargedDate < AssignedDate - broken business rule)
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate, Status) SELECT 1, 1, 'WARD-101', '2024-01-15', '2024-01-17', 'Discharged' WHERE NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 1);
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate, Status) SELECT 2, NULL, 'WARD-102', '2024-01-20', NULL, 'active' WHERE NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 2);
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate, Status) SELECT 3, 2, 'WARD-103', '2024-01-10', '2024-01-12', 'DISCHARGED' WHERE NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 3);
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate, Status) SELECT 4, 3, 'WARD-104', '2024-02-01', '2024-01-28', 'Discharged' WHERE NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 4);

        -- StaffUsers (inconsistent Role, duplicate Email)
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) SELECT 1, 'admin', 'System Administrator', 'Admin', 'admin@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 1);
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) SELECT 2, 'nurse1', 'Nurse Jane', 'Nurse', 'nurse1@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 2);
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) SELECT 3, 'doctor1', 'Dr. Test', 'Doctor', 'doctor1@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 3);
        INSERT INTO dbo.StaffUsers (UserId, Username, FullName, Role, Email) SELECT 4, 'reception', 'Receptionist', 'admin', 'admin@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.StaffUsers WHERE UserId = 4);

        -- SystemLogs (orphan UserId 99999, 88888, NULL)
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) SELECT 1, 'Patients', 'INSERT', '2024-01-15 10:00:00', 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 1);
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) SELECT 2, 'Appointments', 'UPDATE', '2024-01-15 11:00:00', 99999 WHERE NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 2);
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) SELECT 3, 'Prescriptions', 'DELETE', '2024-01-16 09:00:00', 88888 WHERE NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 3);
        INSERT INTO dbo.SystemLogs (LogId, TableName, Action, ChangedAt, UserId) SELECT 4, 'MedicalRecords', 'INSERT', '2024-01-17 14:00:00', NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.SystemLogs WHERE LogId = 4);

        -- AuditTrailRaw (no PK - heap)
        IF (SELECT COUNT(*) FROM dbo.AuditTrailRaw) = 0
        INSERT INTO dbo.AuditTrailRaw (TableName, Action, ChangedAt, UserId) VALUES ('Patients', 'INSERT', '2024-01-15', 1), ('Appointments', 'UPDATE', '2024-01-16', 2);

        -- StagingPatientImport (no PK)
        INSERT INTO dbo.StagingPatientImport (ExternalId, FullName, DateOfBirth, ImportBatch) SELECT 'EXT-001', 'Import Patient 1', '1990-01-01', 'BATCH-2024-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.StagingPatientImport WHERE ExternalId = 'EXT-001');

        -- PatientProcedureJunction (junction with 3 Id cols, single PK - triggers SuspectedJunctionMissingKey)
        INSERT INTO dbo.PatientProcedureJunction (RecordId, PatientId, ProcedureId, AppointmentId) SELECT 1, 1, 1, 1 WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientProcedureJunction WHERE RecordId = 1);
        INSERT INTO dbo.PatientProcedureJunction (RecordId, PatientId, ProcedureId, AppointmentId) SELECT 2, 2, 2, 4 WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientProcedureJunction WHERE RecordId = 2);
        INSERT INTO dbo.PatientProcedureJunction (RecordId, PatientId, ProcedureId, AppointmentId) SELECT 3, 99999, 1, 2 WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientProcedureJunction WHERE RecordId = 3);

        -- LegacyPatientNotes (extreme nullable)
        INSERT INTO dbo.LegacyPatientNotes (NoteId, PatientId, NoteText, AuthorId, DeptId, CreatedAt, ModifiedAt, SourceSystem) SELECT 1, 1, 'Initial note', 1, 1, '2024-01-01', NULL, 'Legacy' WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyPatientNotes WHERE NoteId = 1);
        INSERT INTO dbo.LegacyPatientNotes (NoteId, PatientId, NoteText, AuthorId, DeptId, CreatedAt, ModifiedAt, SourceSystem) SELECT 2, NULL, NULL, NULL, NULL, NULL, NULL, NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.LegacyPatientNotes WHERE NoteId = 2);

        -- FeeSchedule (Amount FLOAT)
        INSERT INTO dbo.FeeSchedule (FeeId, ProcedureCode, Amount, EffectiveDate) SELECT 1, 'CONSULT', 75.00, '2024-01-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.FeeSchedule WHERE FeeId = 1);
        INSERT INTO dbo.FeeSchedule (FeeId, ProcedureCode, Amount, EffectiveDate) SELECT 2, 'XRAY', 150.00, '2024-01-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.FeeSchedule WHERE FeeId = 2);
        INSERT INTO dbo.FeeSchedule (FeeId, ProcedureCode, Amount, EffectiveDate) SELECT 3, 'LAB-BASIC', 45.00, '2024-01-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.FeeSchedule WHERE FeeId = 3);

        -- LabTests (ResultValue FLOAT, inconsistent ResultStatus)
        INSERT INTO dbo.LabTests (LabTestId, PatientId, DoctorId, TestCode, ResultStatus, TestDate, ResultValue) SELECT 1, 1, 1, 'GLUCOSE', 'Normal', '2024-01-15', 95.5 WHERE NOT EXISTS (SELECT 1 FROM dbo.LabTests WHERE LabTestId = 1);
        INSERT INTO dbo.LabTests (LabTestId, PatientId, DoctorId, TestCode, ResultStatus, TestDate, ResultValue) SELECT 2, 2, 2, 'CHOL', 'normal', '2024-01-10', 210.0 WHERE NOT EXISTS (SELECT 1 FROM dbo.LabTests WHERE LabTestId = 2);
        INSERT INTO dbo.LabTests (LabTestId, PatientId, DoctorId, TestCode, ResultStatus, TestDate, ResultValue) SELECT 3, 99999, 1, 'CBC', 'ABNORMAL', '2024-01-20', 12.3 WHERE NOT EXISTS (SELECT 1 FROM dbo.LabTests WHERE LabTestId = 3);

        -- ProcedureCatalog (DefaultPrice FLOAT)
        INSERT INTO dbo.ProcedureCatalog (ProcedureId, ProcedureCode, Name, DefaultPrice) SELECT 1, 'CONSULT', 'General Consultation', 75.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcedureCatalog WHERE ProcedureId = 1);
        INSERT INTO dbo.ProcedureCatalog (ProcedureId, ProcedureCode, Name, DefaultPrice) SELECT 2, 'XRAY', 'X-Ray Imaging', 150.00 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcedureCatalog WHERE ProcedureId = 2);

        -- PatientAllergies
        INSERT INTO dbo.PatientAllergies (AllergyId, PatientId, AllergyCode, Severity, ReportedDate) SELECT 1, 1, 'PEN', 'High', '2020-01-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientAllergies WHERE AllergyId = 1);
        INSERT INTO dbo.PatientAllergies (AllergyId, PatientId, AllergyCode, Severity, ReportedDate) SELECT 2, 2, 'SULF', 'Medium', '2019-05-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientAllergies WHERE AllergyId = 2);
        INSERT INTO dbo.PatientAllergies (AllergyId, PatientId, AllergyCode, Severity, ReportedDate) SELECT 3, 88888, 'LATEX', 'Low', '2022-03-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientAllergies WHERE AllergyId = 3);

        -- DrugInteractions (no PK - junction)
        INSERT INTO dbo.DrugInteractions (Drug1Id, Drug2Id, Severity) SELECT 101, 102, 'High' WHERE NOT EXISTS (SELECT 1 FROM dbo.DrugInteractions WHERE Drug1Id = 101 AND Drug2Id = 102);
        INSERT INTO dbo.DrugInteractions (Drug1Id, Drug2Id, Severity) SELECT 102, 101, 'High' WHERE NOT EXISTS (SELECT 1 FROM dbo.DrugInteractions WHERE Drug1Id = 102 AND Drug2Id = 101);

        -- InsurancePlans (has check constraint - for StatusTypeConstraintConsistency; duplicate PlanCode for DUPLICATE_RECORDS)
        INSERT INTO dbo.InsurancePlans (PlanId, PlanCode, Name, Status) SELECT 1, 'PLAN-A', 'Premium Plan', 'Active' WHERE NOT EXISTS (SELECT 1 FROM dbo.InsurancePlans WHERE PlanId = 1);
        INSERT INTO dbo.InsurancePlans (PlanId, PlanCode, Name, Status) SELECT 2, 'PLAN-B', 'Basic Plan', 'Inactive' WHERE NOT EXISTS (SELECT 1 FROM dbo.InsurancePlans WHERE PlanId = 2);
        INSERT INTO dbo.InsurancePlans (PlanId, PlanCode, Name, Status) SELECT 3, 'PLAN-A', 'Premium Plus', 'Active' WHERE NOT EXISTS (SELECT 1 FROM dbo.InsurancePlans WHERE PlanId = 3);

        -- PatientEmergencyContacts (PatientId NVARCHAR - FK type mismatch with Patients.PatientId INT)
        INSERT INTO dbo.PatientEmergencyContacts (ContactId, PatientId, ContactName, Phone) SELECT 1, '1', 'Spouse', '555-0100' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientEmergencyContacts WHERE ContactId = 1);
        INSERT INTO dbo.PatientEmergencyContacts (ContactId, PatientId, ContactName, Phone) SELECT 2, '99999', 'Unknown', '555-9999' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientEmergencyContacts WHERE ContactId = 2);

        -- MedicationCatalog (duplicate Sku for DUPLICATE_RECORDS; UnitPrice FLOAT for MONEY_AS_FLOAT)
        INSERT INTO dbo.MedicationCatalog (MedicationId, Sku, Name, UnitPrice) SELECT 1, 'MED-001', 'Lisinopril', 0.15 WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicationCatalog WHERE MedicationId = 1);
        INSERT INTO dbo.MedicationCatalog (MedicationId, Sku, Name, UnitPrice) SELECT 2, 'MED-002', 'Metformin', 0.08 WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicationCatalog WHERE MedicationId = 2);
        INSERT INTO dbo.MedicationCatalog (MedicationId, Sku, Name, UnitPrice) SELECT 3, 'MED-001', 'Lisinopril Generic', 0.12 WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicationCatalog WHERE MedicationId = 3);

        -- EventLog (no PK - heap)
        IF (SELECT COUNT(*) FROM dbo.EventLog) = 0
        INSERT INTO dbo.EventLog (EventType, OccurredAt, Payload) VALUES ('Login', '2024-01-15', '{}'), ('Logout', '2024-01-15', '{}');

        -- PatientReferrals (Status without check; mixed casing for INCONSISTENT_FORMATS)
        INSERT INTO dbo.PatientReferrals (ReferralId, PatientId, DoctorId, Status, ReferredDate) SELECT 1, 1, 2, 'Pending', '2024-01-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientReferrals WHERE ReferralId = 1);
        INSERT INTO dbo.PatientReferrals (ReferralId, PatientId, DoctorId, Status, ReferredDate) SELECT 2, 2, 1, 'pending', '2024-01-22' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientReferrals WHERE ReferralId = 2);
        INSERT INTO dbo.PatientReferrals (ReferralId, PatientId, DoctorId, Status, ReferredDate) SELECT 3, 3, 2, 'COMPLETED', '2024-01-25' WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientReferrals WHERE ReferralId = 3);

        -- Additional patients for scale (batch 10-30)
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 10, '888888881V', 'M', 'Barbara Taylor', '1978-03-05', 'barbara.t@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 10);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 11, '888888882V', 'F', 'Daniel Garcia', '1992-08-19', 'daniel.g@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 11);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 12, '888888883V', 'male', 'Jennifer Lee', '1986-12-22', 'jennifer.l@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 12);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 13, '888888884V', 'Female', 'Christopher Harris', '1970-04-14', 'chris.h@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 13);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 14, '888888885V', 'm', 'Sarah Clark', '1995-09-30', 'sarah.c@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 14);
        INSERT INTO dbo.Patients (PatientId, NIC, Gender, FullName, DateOfBirth, Email) SELECT 15, '888888886V', 'f', 'Matthew Lewis', '1983-06-08', 'matt.l@example.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE PatientId = 15);

        -- Additional appointments
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 9, 5, 2, '1', '2024-02-10 09:00:00', '2024-02-10 09:45:00', 'Completed', '2024-02-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 9);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 10, 6, 3, '2', '2024-02-12 14:00:00', '2024-02-12 14:30:00', 'No-Show', '2024-02-12' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 10);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 11, 7, 1, '1', '2024-02-14 11:00:00', '2024-02-14 11:45:00', 'scheduled', '2024-02-14' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 11);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 12, 10, 4, '1', '2024-02-16 10:00:00', '2024-02-16 10:30:00', 'PENDING', '2024-02-16' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 12);
        INSERT INTO dbo.Appointments (AppointmentId, PatientId, DoctorId, FacilityId, StartTime, EndTime, Status, Date) SELECT 13, 11, 2, '1', '2024-02-18 09:00:00', '2024-02-18 09:30:00', ' Pending ', '2024-02-18' WHERE NOT EXISTS (SELECT 1 FROM dbo.Appointments WHERE AppointmentId = 13);

        -- Additional Diagnoses (duplicate DiagnosisCode for DUPLICATE_RECORDS)
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 5, 5, 2, 'M54.5', 'Low back pain', '2024-02-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 5);
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 6, 10, 4, 'G43.9', 'Migraine', '2024-02-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 6);
        INSERT INTO dbo.Diagnoses (DiagnosisId, PatientId, DoctorId, DiagnosisCode, Description, DiagnosedDate) SELECT 7, 5, 2, 'E11.9', 'Type 2 diabetes', '2024-02-12' WHERE NOT EXISTS (SELECT 1 FROM dbo.Diagnoses WHERE DiagnosisId = 7);
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 5, 5, 'Routine checkup', 'Healthy', NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 5);
        INSERT INTO dbo.MedicalRecords (RecordId, PatientId, Notes, DiagnosisText, LabResults) SELECT 6, 10, 'Migraine follow-up', 'Chronic migraine', 'MRI: unremarkable' WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicalRecords WHERE RecordId = 6);

        -- Additional Billing (more PaidAmount > TotalAmount; more WardAssignments with DischargedDate < AssignedDate)
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 5, 5, 9, 75.00, 100.00, 75.00, 'USD', '2024-02-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 5);
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 6, 10, 12, 200.00, 200.00, 200.00, 'USD', '2024-02-16' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 6);
        INSERT INTO dbo.Billing (BillingId, PatientId, AppointmentId, Amount, PaidAmount, TotalAmount, Currency, BillingDate) SELECT 7, 11, 13, 50.00, 75.00, 50.00, 'USD', '2024-02-18' WHERE NOT EXISTS (SELECT 1 FROM dbo.Billing WHERE BillingId = 7);
        INSERT INTO dbo.WardAssignments (AssignmentId, PatientId, WardNumber, AssignedDate, DischargedDate, Status) SELECT 5, 5, 'WARD-105', '2024-02-10', '2024-02-08', 'Discharged' WHERE NOT EXISTS (SELECT 1 FROM dbo.WardAssignments WHERE AssignmentId = 5);
        """;
}
