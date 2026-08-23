-- =========================================================================
-- CREACIÓN DE TABLA dbo.TeamsSeccionesPiloto E INSERCIÓN DE SECCIONES PILOTO
-- =========================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TeamsSeccionesPiloto]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TeamsSeccionesPiloto] (
        [IdSeccion] [int] NOT NULL,
        [Periodo] [varchar](50) NULL,
        [EsActivo] [bit] NOT NULL CONSTRAINT [DF_TeamsSeccionesPiloto_EsActivo] DEFAULT ((1)),
        [FechaCreacion] [datetime] NOT NULL CONSTRAINT [DF_TeamsSeccionesPiloto_FechaCreacion] DEFAULT (getdate()),
        [FechaModificacion] [datetime] NULL,
        [Observacion] [varchar](250) NULL,
        CONSTRAINT [PK_TeamsSeccionesPiloto] PRIMARY KEY CLUSTERED ([IdSeccion] ASC)
    );
END
GO

-- Población inicial con secciones piloto existentes
INSERT INTO dbo.TeamsSeccionesPiloto (IdSeccion, Periodo, EsActivo, FechaCreacion, Observacion)
SELECT 
    s.IdSeccion,
    pe.Codigo AS Periodo,
    1 AS EsActivo,
    GETDATE() AS FechaCreacion,
    'Sembrado inicial de Secciones Piloto' AS Observacion
FROM dbo.Seccion s WITH(NOLOCK)
INNER JOIN dbo.PromocionGrupo pg WITH(NOLOCK) ON s.IdGrupo = pg.IdGrupo AND s.IdPromocion = pg.IdPromocion
INNER JOIN dbo.Promocion p WITH(NOLOCK) ON pg.IdPromocion = p.IdPromocion
INNER JOIN dbo.Periodo pe WITH(NOLOCK) ON p.IdPeriodo = pe.IdPeriodo
WHERE pe.Codigo IN ('2026-IIIA') AND pe.EsTeams = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.TeamsSeccionesPiloto x WHERE x.IdSeccion = s.IdSeccion);
GO
