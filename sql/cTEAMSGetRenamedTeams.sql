CREATE OR ALTER PROCEDURE cTeamsGetRenamedTeams
    @IdSeccion INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH dtDetailTeam AS (
        SELECT TE.IdSeccionSmart,
            TE.NombreTeam,
            TE.DescripcionTeam
        FROM TeamsEquipos TE WITH (NOLOCK)
        WHERE TE.IdSeccionSmart = @IdSeccion
        EXCEPT
        SELECT DISTINCT M.IdCurso,
            NombreCurso + ' [' + M.NombreProducto + '][' + S.Codigo + ']' AS Nombre,
            'Descripcion' = CASE
                WHEN LEN(
                    'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador
                ) >= 250 THEN SUBSTRING(
                    'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador,
                    1,
                    250
                )
                ELSE 'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador
            END
        FROM TeamsProgramacionGeneral M WITH (NOLOCK)
            LEFT JOIN EmpresaSedeParametro ES WITH (NOLOCK) ON (ES.IdSede = M.IdSede)
            LEFT JOIN Seccion S WITH (NOLOCK) ON (S.IdSeccion = M.IdCurso)
        WHERE ES.Nombre = 'PROPIETARIOTINA'
            AND ISNULL(S.IdSeccion, '') <> ''
    )
    SELECT TE.IdTeamsGroup,
        DT.NombreTeam,
        DT.DescripcionTeam
    FROM dtDetailTeam DT WITH (NOLOCK)
        INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (DT.IdSeccionSmart = TE.IdSeccionSmart)
    WHERE ISNULL(TE.IdTeamsGroup, '') <> ''
        AND TE.EstadoTeam = 'A';
END
GO
