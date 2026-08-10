SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.MonitorSharedStateSchema', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MonitorSharedStateSchema
    (
        Id tinyint NOT NULL CONSTRAINT PK_MonitorSharedStateSchema PRIMARY KEY,
        SchemaVersion int NOT NULL,
        InstalledAtUtc datetime2(7) NOT NULL CONSTRAINT DF_MonitorSharedStateSchema_InstalledAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_MonitorSharedStateSchema_Id CHECK (Id = 1)
    );
END;

IF OBJECT_ID(N'dbo.MonitorSharedStateDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MonitorSharedStateDocuments
    (
        DocumentKey nvarchar(128) NOT NULL CONSTRAINT PK_MonitorSharedStateDocuments PRIMARY KEY,
        Version bigint NOT NULL,
        PayloadJson nvarchar(max) NOT NULL,
        UpdatedAtUtc datetime2(7) NOT NULL,
        CONSTRAINT CK_MonitorSharedStateDocuments_Version CHECK (Version >= 1),
        CONSTRAINT CK_MonitorSharedStateDocuments_PayloadJson CHECK (ISJSON(PayloadJson) = 1)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MonitorSharedStateSchema WHERE Id = 1)
BEGIN
    INSERT dbo.MonitorSharedStateSchema (Id, SchemaVersion)
    VALUES (1, 1);
END
ELSE IF EXISTS (SELECT 1 FROM dbo.MonitorSharedStateSchema WHERE Id = 1 AND SchemaVersion <> 1)
BEGIN
    THROW 51000, 'Monitor shared-state schema version differs from v1. Apply the supported migration path instead of overwriting it.', 1;
END;

COMMIT TRANSACTION;
