-- Requires SQLCMD mode (SSMS / Azure Data Studio) or sqlcmd.exe.
-- Hangfire.SqlServer does not use a single table; it installs the full [HangFire] schema.
-- Update the server/database names and the Hangfire install.sql path before execution.

:setvar HangfireInstall "C:\path\to\hangfire.sqlserver\1.8.23\tools\install.sql"

-- Zegel
:CONNECT YOUR_SQLSERVER_INSTANCE
USE [YOUR_ZEGEL_DATABASE]
GO
:r $(HangfireInstall)
GO

-- IDAT
:CONNECT YOUR_SQLSERVER_INSTANCE
USE [YOUR_IDAT_DATABASE]
GO
:r $(HangfireInstall)
GO

-- Corriente Alterna
:CONNECT YOUR_SQLSERVER_INSTANCE
USE [YOUR_CORRIENTEALTERNA_DATABASE]
GO
:r $(HangfireInstall)
GO

-- ITS
:CONNECT YOUR_SQLSERVER_INSTANCE
USE [YOUR_ITS_DATABASE]
GO
:r $(HangfireInstall)
GO

-- CDI
:CONNECT YOUR_SQLSERVER_INSTANCE
USE [YOUR_CDI_DATABASE]
GO
:r $(HangfireInstall)
GO
