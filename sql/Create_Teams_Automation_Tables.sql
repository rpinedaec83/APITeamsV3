-- =============================================
-- Author:      APITeamsV3 Automation
-- Create date: 2026-04-01
-- Description: Create automation staging tables in Smart DB
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsEquipos]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[TeamsEquipos](
	[IdTeamsGroup] [varchar](100) NOT NULL, -- Microsoft Graph ID (Guid)
	[Propietario1] [varchar](200) NOT NULL, -- Facilitator Email
	[Propietario2] [varchar](200) NULL,     -- Optional Admin
	[Propietario3] [varchar](100) NULL,     -- Facilitator Code (Smart)
	[Propietario4] [varchar](100) NULL,     -- New Owner / Migration logic
	[NombreTeam] [varchar](200) NOT NULL,
	[DescripcionTeam] [varchar](500) NULL,
	[MailNickName] [varchar](100) NOT NULL,
	[EstadoTeam] [char](1) NOT NULL CONSTRAINT [DF_TeamsEquipos_EstadoTeam]  DEFAULT ('A'),
	[IdSeccionSmart] [int] NOT NULL,
	[IsActive] [char](1) NOT NULL CONSTRAINT [DF_TeamsEquipos_IsActive]  DEFAULT ('A'),
	[FechaCreacion] [datetime] NOT NULL CONSTRAINT [DF_TeamsEquipos_FechaCreacion]  DEFAULT (GETDATE()),
	[FechaModificacion] [datetime] NULL,
 CONSTRAINT [PK_TeamsEquipos] PRIMARY KEY CLUSTERED ([IdTeamsGroup] ASC)
)
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsUsuarios]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[TeamsUsuarios](
	[IdUsuario] [int] IDENTITY(1,1) NOT NULL,
	[IdTeams] [varchar](100) NOT NULL, -- FK to TeamsEquipos
	[CodigoAlumno] [varchar](20) NULL,
	[Nombres] [varchar](100) NULL,
	[Apellidos] [varchar](100) NULL,
	[Email] [varchar](200) NOT NULL,
	[Tipo] [char](1) NOT NULL CONSTRAINT [DF_TeamsUsuarios_Tipo]  DEFAULT ('A'),
	[Estado] [char](1) NOT NULL CONSTRAINT [DF_TeamsUsuarios_Estado]  DEFAULT ('A'),
	[FechaCreacion] [datetime] NOT NULL CONSTRAINT [DF_TeamsUsuarios_FechaCreacion]  DEFAULT (GETDATE()),
	[FechaModificacion] [datetime] NULL,
 CONSTRAINT [PK_TeamsUsuarios] PRIMARY KEY CLUSTERED ([IdUsuario] ASC)
)
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsHorarios]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[TeamsHorarios](
	[IdTeams] [varchar](100) NOT NULL, -- FK to TeamsEquipos
	[IdEvento] [varchar](200) NOT NULL, -- Microsoft Graph Event ID
	[IdHorario] [int] NULL,
	[IdCurso] [int] NULL, -- Section ID
	[NumeroReunion] [int] NULL,
	[Codigo] [varchar](100) NULL,
	[Fecha] [datetime] NULL,
	[Inicio] [int] NULL,
	[Fin] [int] NULL,
	[JoinUrl] [nvarchar](max) NULL,
	[CodigoFacilitador] [varchar](50) NULL,
	[Estado] [char](1) NOT NULL CONSTRAINT [DF_TeamsHorarios_Estado]  DEFAULT ('A'),
	[FechaCreacion] [datetime] NOT NULL CONSTRAINT [DF_TeamsHorarios_FechaCreacion]  DEFAULT (GETDATE()),
	[FechaModificacion] [datetime] NULL,
 CONSTRAINT [PK_TeamsHorarios] PRIMARY KEY CLUSTERED ([IdTeams] ASC, [IdEvento] ASC)
)
END
GO
