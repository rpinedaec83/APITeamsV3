IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsLogOperativo]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TeamsLogOperativo](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Tipo] [nvarchar](50) NOT NULL,
        [EntidadAfectada] [nvarchar](100) NOT NULL,
        [Referencia] [nvarchar](100) NOT NULL,
        [Mensaje] [nvarchar](max) NOT NULL,
        [ContextoTecnico] [nvarchar](max) NOT NULL,
        [Severidad] [nvarchar](20) NOT NULL,
        [JobId] [nvarchar](100) NULL,
        [Fecha] [datetime2](7) NOT NULL,
        CONSTRAINT [PK_TeamsLogOperativo] PRIMARY KEY CLUSTERED ([Id] ASC)
    )
END
GO
