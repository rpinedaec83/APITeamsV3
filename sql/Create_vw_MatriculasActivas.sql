CREATE
OR ALTER VIEW [dbo].[vw_MatriculasActivas] AS
SELECT SE.IdSeccion,
    SE.Codigo,
    SE.IdCurso,
    SE.IdPromocion,
    PE.IdPeriodo,
    SE.FechaInicio,
    SE.FechaFin,
    -- Extended Info
    CU.CursoNombre,
    PD.ProductoCodigo,
    PD.ProductoNombre,
    SD.Nombre AS SedeNombre,
    UN.Nombre AS UnidadNegocioNombre,
    UA.Nombre AS UnidadAcademicaNombre,
    PE.Codigo AS CodigoPeriodo,
    PG.GrupoCodigo,
    -- Date Filter Logic
    PR.TipoServicio,
    PE.Inicio AS PeriodoInicio,
    PE.Fin AS PeriodoFin,
    -- Facilitador
    ISNULL(FC.CodigoAnterior, '') AS CodigoFacilitador,
    ISNULL(
        REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', '') + ' ' + REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(
            REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''),
            ''
        ),
        ''
    ) AS NombresFacilitador,
    ISNULL(FC.EmailInstitucion, '') AS EmailFacilitador,
    -- Flags
    CAST(ISNULL(PE.EsTeams, 0) AS BIT) AS EsTeams,
    -- Calculated Placeholders (EF requires them if mapped)
    CAST('' AS NVARCHAR(100)) AS CalculatedMailNickname,
    CAST('' AS NVARCHAR(100)) AS CalculatedDisplayName
FROM Seccion SE WITH (NOLOCK)
    INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion
    INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede
    INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio
    INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica
    INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo
    INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto
    LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion
    AND SE.IdGrupo = PG.IdGrupo
    LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso -- Facilitator
    LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion
    AND SP.EsResponsable = 1
    LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor
    LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador
WHERE PE.EsTeams = 1 
    AND SD.Activo = 1;