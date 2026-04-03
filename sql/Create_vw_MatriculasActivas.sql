CREATE OR ALTER VIEW [dbo].[vw_MatriculasActivas] AS
SELECT
    SE.IdSeccion,
    SE.Codigo,
    SE.IdCurso,
    SE.IdPromocion,
    ISNULL(PR.IdCurricula, ISNULL(SE.IdCurricula, 0)) AS IdCurricula,
    PE.IdPeriodo,
    SE.FechaInicio,
    SE.FechaFin,
    CU.CursoNombre,
    PD.ProductoCodigo,
    PD.ProductoNombre,
    SD.Nombre AS SedeNombre,
    UN.Nombre AS UnidadNegocioNombre,
    UA.Nombre AS UnidadAcademicaNombre,
    PE.Codigo AS CodigoPeriodo,
    PG.GrupoCodigo,
    PR.TipoServicio,
    PE.Inicio AS PeriodoInicio,
    PE.Fin AS PeriodoFin,
    ISNULL(FC.CodigoAnterior, '') AS CodigoFacilitador,
    ISNULL(
        REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', '') + ' ' +
        REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', '') + ' ' +
        ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), ''),
        ''
    ) AS NombresFacilitador,
    ISNULL(FC.EmailInstitucion, '') AS EmailFacilitador,
    CAST(ISNULL(PE.EsTeams, 0) AS BIT) AS EsTeams,
    CAST('' AS NVARCHAR(100)) AS CalculatedMailNickname,
    CAST('' AS NVARCHAR(100)) AS CalculatedDisplayName
FROM dbo.Seccion SE WITH (NOLOCK)
INNER JOIN dbo.Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
INNER JOIN dbo.Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
INNER JOIN dbo.UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
INNER JOIN dbo.UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
INNER JOIN dbo.Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
INNER JOIN dbo.Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
LEFT JOIN dbo.PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
    AND SE.IdGrupo = PG.IdGrupo
LEFT JOIN dbo.Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso
LEFT JOIN dbo.SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
    AND SP.EsResponsable = 1
LEFT JOIN dbo.Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
LEFT JOIN dbo.Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
WHERE PE.EsTeams = 1
  AND SD.Activo = 1;
GO
