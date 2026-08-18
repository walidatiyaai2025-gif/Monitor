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

/*
  Fail closed before stamping schema version 1 when a pre-existing table does
  not match the core contract consumed by the v1 SQL backend. Keep this core
  fingerprint aligned with SqlServerSharedStateSqlBackend readiness checks.
*/
DECLARE @DocumentsObjectId int = OBJECT_ID(N'dbo.MonitorSharedStateDocuments', N'U');
IF @DocumentsObjectId IS NULL
   OR (SELECT COUNT(*) FROM sys.columns WHERE object_id = @DocumentsObjectId) <> 4
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = @DocumentsObjectId
         AND name = N'DocumentKey'
         AND system_type_id = 231
         AND user_type_id = 231
         AND max_length = 256
         AND is_nullable = 0
         AND is_computed = 0
   )
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = @DocumentsObjectId
         AND name = N'Version'
         AND system_type_id = 127
         AND user_type_id = 127
         AND max_length = 8
         AND is_nullable = 0
         AND is_computed = 0
   )
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = @DocumentsObjectId
         AND name = N'PayloadJson'
         AND system_type_id = 231
         AND user_type_id = 231
         AND max_length = -1
         AND is_nullable = 0
         AND is_computed = 0
   )
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = @DocumentsObjectId
         AND name = N'UpdatedAtUtc'
         AND system_type_id = 42
         AND user_type_id = 42
         AND scale = 7
         AND is_nullable = 0
         AND is_computed = 0
   )
   OR NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes AS indexes
       WHERE indexes.object_id = @DocumentsObjectId
         AND indexes.is_primary_key = 1
         AND indexes.is_unique = 1
         AND (
             SELECT COUNT(*)
             FROM sys.index_columns
             WHERE object_id = indexes.object_id
               AND index_id = indexes.index_id
               AND key_ordinal > 0
         ) = 1
         AND EXISTS
         (
             SELECT 1
             FROM sys.index_columns AS index_columns
             INNER JOIN sys.columns AS columns
                 ON columns.object_id = index_columns.object_id
                AND columns.column_id = index_columns.column_id
             WHERE index_columns.object_id = indexes.object_id
               AND index_columns.index_id = indexes.index_id
               AND index_columns.key_ordinal = 1
               AND index_columns.is_included_column = 0
               AND columns.name = N'DocumentKey'
         )
   )
BEGIN
    THROW 51001, 'Monitor shared-state v1 document table differs from the supported core schema. Apply the supported migration path instead of stamping v1.', 1;
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
