# Plan de Implementación Futura: Tabla de Secciones Piloto en Base de Datos de Empresa y Modificación de `cTeamsPorSeccion`

Este documento contiene el plan técnico completo para migrar las secciones piloto desde listas hardcoded dentro del Stored Procedure `[dbo].[cTeamsPorSeccion]` hacia una tabla dedicada en la base de datos de cada empresa (Smart DB: IDAT / ZEGEL) con sincronización automática.

---

## 1. Estructura de la Nueva Tabla `[dbo].[TeamsSeccionesPiloto]`

Se creará la tabla `[dbo].[TeamsSeccionesPiloto]` en la base de datos de cada empresa con la siguiente estructura:

```sql
CREATE TABLE [dbo].[TeamsSeccionesPiloto] (
    [IdSeccion] [int] NOT NULL,
    [Periodo] [varchar](50) NULL,
    [EsActivo] [bit] NOT NULL CONSTRAINT [DF_TeamsSeccionesPiloto_EsActivo] DEFAULT ((1)),
    [FechaCreacion] [datetime] NOT NULL CONSTRAINT [DF_TeamsSeccionesPiloto_FechaCreacion] DEFAULT (getdate()),
    [FechaModificacion] [datetime] NULL,
    [Observacion] [varchar](250) NULL,
    CONSTRAINT [PK_TeamsSeccionesPiloto] PRIMARY KEY CLUSTERED ([IdSeccion] ASC)
);
GO
```

---

## 2. Modificación del Stored Procedure `[dbo].[cTeamsPorSeccion]`

En el archivo `[dbo].[cTeamsPorSeccion].sql`, se reemplazará la inserción manual de IDs quemados por la lectura desde la nueva tabla:

```sql
-- Reemplazo en cTeamsPorSeccion.sql
DECLARE @SeccionesOmitidas TABLE (IdSeccion INT PRIMARY KEY)

INSERT INTO @SeccionesOmitidas (IdSeccion)
SELECT IdSeccion 
FROM dbo.TeamsSeccionesPiloto WITH (NOLOCK)
WHERE EsActivo = 1;
```

---

## 3. Mantenimiento y Sincronización Automática con `dbo.Seccion`

### 3.1. Sincronización al Eliminar en `dbo.Seccion`
Cuando una sección se borre o desactive de `dbo.Seccion`, el Trigger/Proceso de sincronización actualizará el estado en `dbo.TeamsSeccionesPiloto`:

```sql
CREATE TRIGGER [dbo].[TR_Seccion_AfterDelete_PilotSync]
ON [dbo].[Seccion]
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE p
    SET p.EsActivo = 0,
        p.FechaModificacion = GETDATE(),
        p.Observacion = 'Desactivado por eliminación en dbo.Seccion'
    FROM dbo.TeamsSeccionesPiloto p
    INNER JOIN deleted d ON p.IdSeccion = d.IdSeccion;
END;
GO
```

### 3.2. Sincronización al Insertar en `dbo.Seccion`
Al crearse una nueva sección perteneciente a un periodo marcado en la regla piloto (ej. `2026-IIIA`), se agregará automáticamente:

```sql
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
    INNER JOIN PromocionGrupo pg WITH (NOLOCK) ON i.IdGrupo = pg.IdGrupo AND i.IdPromocion = pg.IdPromocion
    INNER JOIN Promocion p WITH (NOLOCK) ON pg.IdPromocion = p.IdPromocion
    INNER JOIN Periodo pe WITH (NOLOCK) ON p.IdPeriodo = pe.IdPeriodo
    WHERE pe.Codigo IN ('2026-IIIA') -- Periodos de prueba o piloto configurados
      AND NOT EXISTS (SELECT 1 FROM dbo.TeamsSeccionesPiloto x WHERE x.IdSeccion = i.IdSeccion);
END;
GO
```

---

## 4. Integración en la Capa Backend (EF Core & MediatR)

1. **Entidad Domain**: `TeamsSeccionesPiloto.cs` mapeada en `ISmartDbContext`.
2. **Controller de Administración**: Actualizar `PilotManagementController.cs` para administrar los registros en `dbo.TeamsSeccionesPiloto` por empresa.
