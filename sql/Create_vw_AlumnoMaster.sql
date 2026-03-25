CREATE
OR ALTER VIEW [dbo].[vw_AlumnoMaster] AS
SELECT A.IdAlumno,
    A.CodigoAnterior AS Codigo,
    A.EmailInstitucion,
    -- EmailPersonal might not exist in Alumno, check if exists or output null
    CAST(NULL AS VARCHAR(100)) AS EmailPersonal,
    -- Concatenate Name
    LTRIM(
        RTRIM(
            ISNULL(AC.Nombres, '') + ' ' + ISNULL(AC.Paterno, '') + ' ' + ISNULL(AC.Materno, '')
        )
    ) AS Nombre
FROM Alumno A WITH(NOLOCK)
    INNER JOIN Actor AC WITH(NOLOCK) ON A.IdAlumno = AC.IdActor;