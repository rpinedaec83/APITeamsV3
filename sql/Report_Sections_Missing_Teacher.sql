/*
  REPORTE: SECCIONES ELEGIBLES PARA TEAMS BLOQUEADAS POR FALTA DE DOCENTE
  
  Este reporte identifica las secciones que:
  1. No tienen un equipo ya creado en la tabla TeamsEquipos (o está inactivo).
  2. No tienen un correo electrónico asignado para el docente (Facilitador) en TeamsProgramacionGeneral.
  3. Deberían haber sido procesadas pero están bloqueadas por la nueva regla de negocio.
*/

SELECT 
    PG.IdSede,
    PG.NombreSede,
    PG.NombreUnidadNegocio AS Division,
    PG.NombreUnidadAcademica AS Programa,
    PG.CodigoPeriodo AS Periodo,
    PG.IdCurso AS IdSeccion,
    PG.NombreCurso,
    PG.GrupoCodigo AS Seccion,
    PG.CodigoFacilitador,
    PG.NombresFacilitador AS Docente,
    'BLOQUEADO: Falta Email Facilitador' AS EstadoActual
FROM TeamsProgramacionGeneral PG WITH(NOLOCK)
LEFT JOIN TeamsEquipos TE WITH(NOLOCK) ON TE.IdSeccionSmart = PG.IdCurso AND TE.EstadoTeam = 'A'
WHERE (PG.EmailFacilitador IS NULL OR PG.EmailFacilitador = '')
  AND TE.IdTeamsGroup IS NULL -- No tiene equipo activo
ORDER BY PG.NombreSede, PG.NombreUnidadNegocio, PG.NombreCurso;
