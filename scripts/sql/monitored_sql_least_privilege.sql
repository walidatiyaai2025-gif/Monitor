/*
  Monitor target SQL Server - read-only least-privilege baseline.

  Required usage with sqlcmd:
    sqlcmd -S <server> -E -v MonitorLogin="DOMAIN\svc-monitor" -i monitored_sql_least_privilege.sql

  `MonitorLogin` MUST be supplied with `-v`; the script deliberately has no
  internal default because an in-file :setvar would override the deployment
  value. The login MUST already exist. This script creates no password/login and
  grants no DML/DDL/sysadmin rights. Review against your SQL Server version and
  security policy before production deployment.

  Metadata note:
  The collector reads sys.master_files and bounded server-level configuration /
  performance metadata (sys.configurations, sys.dm_os_performance_counters and
  sys.dm_os_memory_clerks). SQL Server metadata visibility rules can otherwise
  hide rows even when SELECT on a catalog view is granted. The server role
  therefore receives VIEW ANY DEFINITION, while VIEW SERVER PERFORMANCE STATE
  (SQL Server 2022+) or VIEW SERVER STATE (older versions) supplies the existing
  read-only DMV boundary. These grants do not permit data mutation.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MonitorLogin sysname = N'$(MonitorLogin)';
IF NULLIF(LTRIM(RTRIM(@MonitorLogin)), N'') IS NULL OR SUSER_ID(@MonitorLogin) IS NULL
BEGIN
    THROW 51020, 'Supply an existing Monitor service login through -v MonitorLogin=...', 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'MonitorObserverServerRole' AND type = 'R')
BEGIN
    CREATE SERVER ROLE MonitorObserverServerRole AUTHORIZATION sysadmin;
END;

DECLARE @major int = TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));
IF @major IS NULL
BEGIN
    THROW 51021, 'Unable to determine SQL Server major version.', 1;
END;

IF @major >= 16
BEGIN
    GRANT VIEW SERVER PERFORMANCE STATE TO MonitorObserverServerRole;
END
ELSE
BEGIN
    GRANT VIEW SERVER STATE TO MonitorObserverServerRole;
END;

GRANT VIEW ANY DATABASE TO MonitorObserverServerRole;
GRANT VIEW ANY DEFINITION TO MonitorObserverServerRole;

DECLARE @sql nvarchar(max) = N'ALTER SERVER ROLE MonitorObserverServerRole ADD MEMBER ' + QUOTENAME(@MonitorLogin) + N';';
EXEC sys.sp_executesql @sql;

USE master;

IF DATABASE_PRINCIPAL_ID(N'MonitorObserverMasterRole') IS NULL
BEGIN
    CREATE ROLE MonitorObserverMasterRole AUTHORIZATION dbo;
END;

IF DATABASE_PRINCIPAL_ID(N'$(MonitorLogin)') IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@MonitorLogin) + N' FOR LOGIN ' + QUOTENAME(@MonitorLogin) + N';';
    EXEC sys.sp_executesql @sql;
END;

GRANT SELECT ON sys.master_files TO MonitorObserverMasterRole;
SET @sql = N'ALTER ROLE MonitorObserverMasterRole ADD MEMBER ' + QUOTENAME(@MonitorLogin) + N';';
EXEC sys.sp_executesql @sql;

USE msdb;

IF DATABASE_PRINCIPAL_ID(N'MonitorObserverMsdbRole') IS NULL
BEGIN
    CREATE ROLE MonitorObserverMsdbRole AUTHORIZATION dbo;
END;

IF DATABASE_PRINCIPAL_ID(N'$(MonitorLogin)') IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@MonitorLogin) + N' FOR LOGIN ' + QUOTENAME(@MonitorLogin) + N';';
    EXEC sys.sp_executesql @sql;
END;

GRANT SELECT ON dbo.backupset TO MonitorObserverMsdbRole;
GRANT SELECT ON dbo.sysjobs TO MonitorObserverMsdbRole;
GRANT SELECT ON dbo.sysjobservers TO MonitorObserverMsdbRole;
SET @sql = N'ALTER ROLE MonitorObserverMsdbRole ADD MEMBER ' + QUOTENAME(@MonitorLogin) + N';';
EXEC sys.sp_executesql @sql;

/*
  Collector coverage intentionally stops at bounded read-only operational facts.
  The current Monitor snapshot query reads server identity, sys.databases /
  sys.master_files, OS/request/scheduler/I/O DMVs, max-server-memory metadata,
  Memory Manager / Buffer Manager counters, the dominant memory-clerk class,
  and the three msdb metadata tables above. It does not collect SQL text,
  execution plans, table data, BACKUP/RESTORE, SQL Agent operator rights, DDL,
  IMPERSONATE, CONTROL SERVER or sysadmin.
*/
