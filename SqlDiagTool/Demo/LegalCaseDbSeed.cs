namespace SqlDiagTool.Demo;

public static class LegalCaseDbSeed
{
    public static string GetSeedSql() => """
        -- Drop circular FK first (Cases <-> Attorneys) so tables can be dropped
        IF OBJECT_ID('dbo.Attorneys', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Attorneys') AND name = 'FK_Attorneys_PrimaryCase')
                ALTER TABLE dbo.Attorneys DROP CONSTRAINT FK_Attorneys_PrimaryCase;
        END
        DROP TABLE IF EXISTS dbo.CaseNotes;
        DROP TABLE IF EXISTS dbo.Documents;
        DROP TABLE IF EXISTS dbo.CaseSummaries;
        DROP TABLE IF EXISTS dbo.ClientProfiles;
        DROP TABLE IF EXISTS dbo.CourtSessions;
        DROP TABLE IF EXISTS dbo.BillingEntries;
        DROP TABLE IF EXISTS dbo.Cases;
        DROP TABLE IF EXISTS dbo.Attorneys;
        DROP TABLE IF EXISTS dbo.Clients;
        DROP TABLE IF EXISTS dbo.Firms;

        -- ========== CORE TABLES (with FKs for referential-integrity checks) ==========
        CREATE TABLE dbo.Firms (FirmId INT PRIMARY KEY, FirmName NVARCHAR(200), Address NVARCHAR(500));

        CREATE TABLE dbo.Clients (ClientId INT PRIMARY KEY, ClientName NVARCHAR(200), ContactEmail NVARCHAR(255));

        -- Attorneys: FirmId FK. PrimaryCaseId added later to form circular FK with Cases.
        CREATE TABLE dbo.Attorneys (AttorneyId INT PRIMARY KEY, AttorneyName NVARCHAR(200), FirmId INT NOT NULL REFERENCES dbo.Firms(FirmId), BarNumber NVARCHAR(50));

        CREATE TABLE dbo.Cases (CaseId INT PRIMARY KEY, CaseNumber NVARCHAR(50), ClientId INT NOT NULL REFERENCES dbo.Clients(ClientId), LeadAttorneyId INT NOT NULL REFERENCES dbo.Attorneys(AttorneyId), Status NVARCHAR(50), FiledDate DATE);

        -- Circular FK: Attorneys.PrimaryCaseId -> Cases (cycle: Cases->Attorneys->Cases)
        ALTER TABLE dbo.Attorneys ADD PrimaryCaseId INT NULL;
        ALTER TABLE dbo.Attorneys ADD CONSTRAINT FK_Attorneys_PrimaryCase FOREIGN KEY (PrimaryCaseId) REFERENCES dbo.Cases(CaseId);

        -- CaseSummaries: 1:1 with Cases but CaseId has no UNIQUE (OneToOneMissingUnique)
        CREATE TABLE dbo.CaseSummaries (SummaryId INT PRIMARY KEY, CaseId INT NOT NULL REFERENCES dbo.Cases(CaseId), SummaryText NVARCHAR(MAX), LastUpdated DATETIME2);

        -- ClientProfiles: 1:1 with Clients but ClientId has no UNIQUE (OneToOneMissingUnique)
        CREATE TABLE dbo.ClientProfiles (ProfileId INT PRIMARY KEY, ClientId INT NOT NULL REFERENCES dbo.Clients(ClientId), RiskLevel NVARCHAR(20), Notes NVARCHAR(MAX));

        -- CourtSessions: CaseId FK
        CREATE TABLE dbo.CourtSessions (SessionId INT PRIMARY KEY, CaseId INT NOT NULL REFERENCES dbo.Cases(CaseId) ON DELETE CASCADE, SessionDate DATE, Outcome NVARCHAR(100));

        -- CaseNotes: Nullable FK (NullableForeignKeyColumns)
        CREATE TABLE dbo.CaseNotes (NoteId INT PRIMARY KEY, CaseId INT NULL REFERENCES dbo.Cases(CaseId), NoteText NVARCHAR(MAX), CreatedAt DATETIME2);

        -- Documents: Polymorphic (documentable_type, documentable_id) - no FK on documentable_id (PolymorphicRelationship)
        CREATE TABLE dbo.Documents (DocumentId INT PRIMARY KEY, DocumentName NVARCHAR(200), documentable_type NVARCHAR(100), documentable_id INT, UploadedAt DATETIME2);

        -- BillingEntries: CaseId FK
        CREATE TABLE dbo.BillingEntries (EntryId INT PRIMARY KEY, CaseId INT NOT NULL REFERENCES dbo.Cases(CaseId), AttorneyId INT NOT NULL REFERENCES dbo.Attorneys(AttorneyId), Hours DECIMAL(5,2), Amount DECIMAL(10,2), BilledDate DATE);
        -- __BATCH__
        -- ========== SEED DATA ==========
        INSERT INTO dbo.Firms (FirmId, FirmName, Address) SELECT 1, 'Smith & Associates', '100 Legal Plaza' WHERE NOT EXISTS (SELECT 1 FROM dbo.Firms WHERE FirmId = 1);
        INSERT INTO dbo.Firms (FirmId, FirmName, Address) SELECT 2, 'Johnson Legal Group', '200 Court Street' WHERE NOT EXISTS (SELECT 1 FROM dbo.Firms WHERE FirmId = 2);

        INSERT INTO dbo.Clients (ClientId, ClientName, ContactEmail) SELECT 1, 'Acme Corp', 'legal@acme.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE ClientId = 1);
        INSERT INTO dbo.Clients (ClientId, ClientName, ContactEmail) SELECT 2, 'Beta LLC', 'contact@beta.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE ClientId = 2);
        INSERT INTO dbo.Clients (ClientId, ClientName, ContactEmail) SELECT 3, 'Gamma Inc', 'info@gamma.com' WHERE NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE ClientId = 3);

        INSERT INTO dbo.Attorneys (AttorneyId, AttorneyName, FirmId, BarNumber) SELECT 1, 'Jane Smith', 1, 'BAR-1001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Attorneys WHERE AttorneyId = 1);
        INSERT INTO dbo.Attorneys (AttorneyId, AttorneyName, FirmId, BarNumber) SELECT 2, 'Robert Johnson', 1, 'BAR-1002' WHERE NOT EXISTS (SELECT 1 FROM dbo.Attorneys WHERE AttorneyId = 2);
        INSERT INTO dbo.Attorneys (AttorneyId, AttorneyName, FirmId, BarNumber) SELECT 3, 'Maria Garcia', 2, 'BAR-2001' WHERE NOT EXISTS (SELECT 1 FROM dbo.Attorneys WHERE AttorneyId = 3);

        INSERT INTO dbo.Cases (CaseId, CaseNumber, ClientId, LeadAttorneyId, Status, FiledDate) SELECT 1, 'CV-2024-001', 1, 1, 'Active', '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Cases WHERE CaseId = 1);
        INSERT INTO dbo.Cases (CaseId, CaseNumber, ClientId, LeadAttorneyId, Status, FiledDate) SELECT 2, 'CV-2024-002', 2, 2, 'Pending', '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.Cases WHERE CaseId = 2);
        INSERT INTO dbo.Cases (CaseId, CaseNumber, ClientId, LeadAttorneyId, Status, FiledDate) SELECT 3, 'CV-2024-003', 3, 3, 'Closed', '2024-01-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.Cases WHERE CaseId = 3);

        -- Set PrimaryCaseId to complete circular FK
        UPDATE dbo.Attorneys SET PrimaryCaseId = 1 WHERE AttorneyId = 1;
        UPDATE dbo.Attorneys SET PrimaryCaseId = 2 WHERE AttorneyId = 2;
        UPDATE dbo.Attorneys SET PrimaryCaseId = 3 WHERE AttorneyId = 3;

        INSERT INTO dbo.CaseSummaries (SummaryId, CaseId, SummaryText, LastUpdated) SELECT 1, 1, 'Contract dispute; discovery ongoing', '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseSummaries WHERE SummaryId = 1);
        INSERT INTO dbo.CaseSummaries (SummaryId, CaseId, SummaryText, LastUpdated) SELECT 2, 2, 'Employment matter; mediation scheduled', '2024-02-05' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseSummaries WHERE SummaryId = 2);
        INSERT INTO dbo.CaseSummaries (SummaryId, CaseId, SummaryText, LastUpdated) SELECT 3, 3, 'Settled out of court', '2024-02-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseSummaries WHERE SummaryId = 3);

        INSERT INTO dbo.ClientProfiles (ProfileId, ClientId, RiskLevel, Notes) SELECT 1, 1, 'Low', 'Corporate client' WHERE NOT EXISTS (SELECT 1 FROM dbo.ClientProfiles WHERE ProfileId = 1);
        INSERT INTO dbo.ClientProfiles (ProfileId, ClientId, RiskLevel, Notes) SELECT 2, 2, 'Medium', 'SME' WHERE NOT EXISTS (SELECT 1 FROM dbo.ClientProfiles WHERE ProfileId = 2);
        INSERT INTO dbo.ClientProfiles (ProfileId, ClientId, RiskLevel, Notes) SELECT 3, 3, 'Low', NULL WHERE NOT EXISTS (SELECT 1 FROM dbo.ClientProfiles WHERE ProfileId = 3);

        INSERT INTO dbo.CourtSessions (SessionId, CaseId, SessionDate, Outcome) SELECT 1, 1, '2024-02-15', 'Continued' WHERE NOT EXISTS (SELECT 1 FROM dbo.CourtSessions WHERE SessionId = 1);
        INSERT INTO dbo.CourtSessions (SessionId, CaseId, SessionDate, Outcome) SELECT 2, 2, '2024-02-20', 'Pending' WHERE NOT EXISTS (SELECT 1 FROM dbo.CourtSessions WHERE SessionId = 2);

        INSERT INTO dbo.CaseNotes (NoteId, CaseId, NoteText, CreatedAt) SELECT 1, 1, 'Client called re: discovery deadline', '2024-02-01 10:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseNotes WHERE NoteId = 1);
        INSERT INTO dbo.CaseNotes (NoteId, CaseId, NoteText, CreatedAt) SELECT 2, NULL, 'General firm note', '2024-02-02 09:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseNotes WHERE NoteId = 2);
        INSERT INTO dbo.CaseNotes (NoteId, CaseId, NoteText, CreatedAt) SELECT 3, 2, 'Mediation prep', '2024-02-05 14:00' WHERE NOT EXISTS (SELECT 1 FROM dbo.CaseNotes WHERE NoteId = 3);

        INSERT INTO dbo.Documents (DocumentId, DocumentName, documentable_type, documentable_id, UploadedAt) SELECT 1, 'Contract.pdf', 'Case', 1, '2024-01-20' WHERE NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE DocumentId = 1);
        INSERT INTO dbo.Documents (DocumentId, DocumentName, documentable_type, documentable_id, UploadedAt) SELECT 2, 'Retainer.pdf', 'Client', 1, '2024-01-15' WHERE NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE DocumentId = 2);
        INSERT INTO dbo.Documents (DocumentId, DocumentName, documentable_type, documentable_id, UploadedAt) SELECT 3, 'Complaint.pdf', 'Case', 2, '2024-02-02' WHERE NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE DocumentId = 3);

        INSERT INTO dbo.BillingEntries (EntryId, CaseId, AttorneyId, Hours, Amount, BilledDate) SELECT 1, 1, 1, 5.5, 1375.00, '2024-02-01' WHERE NOT EXISTS (SELECT 1 FROM dbo.BillingEntries WHERE EntryId = 1);
        INSERT INTO dbo.BillingEntries (EntryId, CaseId, AttorneyId, Hours, Amount, BilledDate) SELECT 2, 2, 2, 3.0, 750.00, '2024-02-05' WHERE NOT EXISTS (SELECT 1 FROM dbo.BillingEntries WHERE EntryId = 2);
        INSERT INTO dbo.BillingEntries (EntryId, CaseId, AttorneyId, Hours, Amount, BilledDate) SELECT 3, 3, 3, 10.0, 2500.00, '2024-02-10' WHERE NOT EXISTS (SELECT 1 FROM dbo.BillingEntries WHERE EntryId = 3);
        """;
}
