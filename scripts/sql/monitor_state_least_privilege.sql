/*
  Monitor dedicated state database - runtime least-privilege role.

  Run this script in the dedicated Monitor state database AFTER
  scripts/sql/monitor_shared_state_v1.sql.

  This script intentionally does not create a login or embed a service-account
  name/password. Create/map the deployment principal through your approved
  identity process, then add that database user to dbo.MonitorStateRuntime.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MonitorSharedStateSchema', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MonitorSharedStateDocuments', N'U') IS NULL
BEGIN
    THROW 51010, 'Monitor shared-state schema v1 must be deployed before runtime permissions.', 1;
END;

IF DATABASE_PRINCIPAL_ID(N'MonitorStateRuntime') IS NULL
BEGIN
    CREATE ROLE MonitorStateRuntime AUTHORIZATION dbo;
END;

GRANT SELECT ON dbo.MonitorSharedStateSchema TO MonitorStateRuntime;
GRANT SELECT, INSERT, UPDATE ON dbo.MonitorSharedStateDocuments TO MonitorStateRuntime;

/*
  Deployment example after an administrator has created/mapped the identity:

  USE [MonitorState];
  CREATE USER [DOMAIN\svc-monitor] FOR LOGIN [DOMAIN\svc-monitor];
  ALTER ROLE MonitorStateRuntime ADD MEMBER [DOMAIN\svc-monitor];

  The runtime role deliberately receives no ALTER/CONTROL/DELETE/EXECUTE,
  no db_owner/db_datareader/db_datawriter membership, and no permission to
  create or migrate schema objects.
*/
