-- =========================================================================
-- TRIGGERS DE SINCRONIZACIÓN AUTOMÁTICA EN dbo.Seccion
-- =========================================================================

-- 1. Trigger al eliminar en dbo.Seccion (desactiva en TeamsSeccionesPiloto)
IF EXISTS (SELECT * FROM sys.triggers WHERE name = N'TR_Seccion_AfterDelete_PilotSync')
    DROP TRIGGER [dbo].[TR_Seccion_AfterDelete_PilotSync];
GO

CREATE TRIGGER [dbo].[TR_Seccion_AfterDelete_PilotSync]
ON [dbo].[Seccion]
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE p
    SET p.EsActivo = 0,
        p.FechaModificacion = GETDATE(),
        p.Observacion = 'Desactivado automáticamente por eliminación en dbo.Seccion'
    FROM dbo.TeamsSeccionesPiloto p
    INNER JOIN deleted d ON p.IdSeccion = d.IdSeccion;
END;
GO

-- 2. Trigger al insertar en dbo.Seccion (agrega automáticamente si pertenece al periodo piloto)
IF EXISTS (SELECT * FROM sys.triggers WHERE name = N'TR_Seccion_AfterInsert_PilotSync')
    DROP TRIGGER [dbo].[TR_Seccion_AfterInsert_PilotSync];
GO

CREATE TRIGGER [dbo].[TR_Seccion_AfterInsert_PilotSync]
ON [dbo].[Seccion]
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.TeamsSeccionesPiloto (IdSeccion, Periodo, EsActivo, FechaCreacion, Observacion)
    SELECT 
        i.IdSeccion,
        pe.Codigo,
        1,
        GETDATE(),
        'Agregado automáticamente por Trigger de Inserción'
    FROM inserted i
    INNER JOIN dbo.PromocionGrupo pg WITH (NOLOCK) ON i.IdGrupo = pg.IdGrupo AND i.IdPromocion = pg.IdPromocion
    INNER JOIN dbo.Promocion p WITH (NOLOCK) ON pg.IdPromocion = p.IdPromocion
    INNER JOIN dbo.Periodo pe WITH (NOLOCK) ON p.IdPeriodo = pe.IdPeriodo
    WHERE pe.Codigo IN ('2026-IIIA')
      AND NOT EXISTS (SELECT 1 FROM dbo.TeamsSeccionesPiloto x WHERE x.IdSeccion = i.IdSeccion);
END;
GO
