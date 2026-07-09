IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsReunionAsistencia]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TeamsReunionAsistencia] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdSeccion] INT NOT NULL,
        [IdTeamsGroup] NVARCHAR(100) NULL,
        [MeetingId] NVARCHAR(250) NOT NULL,
        [MeetingReportId] NVARCHAR(250) NOT NULL,
        [MeetingStartDateTime] DATETIME2(7) NOT NULL,
        [MeetingEndDateTime] DATETIME2(7) NOT NULL,
        [TotalParticipantCount] INT NOT NULL,
        [FechaSincronizacion] DATETIME2(7) NOT NULL CONSTRAINT [DF_TeamsReunionAsistencia_FechaSincronizacion] DEFAULT (GETDATE()),
        CONSTRAINT [PK_TeamsReunionAsistencia] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UK_TeamsReunionAsistencia_ReportId] UNIQUE NONCLUSTERED ([MeetingReportId] ASC)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsReunionAsistenciaDetalle]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TeamsReunionAsistenciaDetalle] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [IdReunionAsistencia] INT NOT NULL,
        [EmailAddress] NVARCHAR(256) NULL,
        [DisplayName] NVARCHAR(256) NULL,
        [Role] NVARCHAR(50) NULL,
        [TotalAttendanceInSeconds] INT NOT NULL,
        [FirstJoinDateTime] DATETIME2(7) NULL,
        [LastLeaveDateTime] DATETIME2(7) NULL,
        CONSTRAINT [PK_TeamsReunionAsistenciaDetalle] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_TeamsReunionAsistenciaDetalle_TeamsReunionAsistencia] FOREIGN KEY ([IdReunionAsistencia]) 
            REFERENCES [dbo].[TeamsReunionAsistencia] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Safe Alter statements for existing databases
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsReunionAsistenciaDetalle]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TeamsReunionAsistenciaDetalle]') AND name = 'FirstJoinDateTime')
    BEGIN
        ALTER TABLE [dbo].[TeamsReunionAsistenciaDetalle] ADD [FirstJoinDateTime] DATETIME2(7) NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TeamsReunionAsistenciaDetalle]') AND name = 'LastLeaveDateTime')
    BEGIN
        ALTER TABLE [dbo].[TeamsReunionAsistenciaDetalle] ADD [LastLeaveDateTime] DATETIME2(7) NULL;
    END
END
GO

