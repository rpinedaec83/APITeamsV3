USE [Academico]
GO
/****** Object:  StoredProcedure [dbo].[cTeamsPorSeccion]    Script Date: 7/04/2026 07:22:07 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
 --————————————————————————————————————————————————————————————————————————————                                          
--Creado por      : LARRIETA   08.10.2020                                      
--Funcionalidad   : Creación de los equipos a demanda            
--Utilizado por   : API TEAMS              
--————————————————————————————————————————————————————————————————————————————                             
/*                                      
------------------------------------------------------------------------------                                      
Nro     FECHA   USUARIO   DESCRIPCION                                      
------------------------------------------------------------------------------                
@1  08.10.2020  LARRIETA  Se creo el SP                  
@2  10.04.2021  rpineda   EsMatricula = 1            
@3 25.04.2022 Illosa   Se quita validacion de facilitadores de reemplazo para CA           
@4 24.10.2022 Illosa   Se agrega is null para cursos sin curricula          
cTeamsPorSeccion @Opcion = 16, @IdSeccion =468389            
@5 04.04.2023 jcure   Corrige la division entre 0 (reporte 36)          
@6 15.04.2023  rpineda   Nuevas Empresas CDI ITS          
@7 25.08.2023 rpineda   Opcion de borrado por aplicacion          
@8  07.08.2024  rotorres  Se agrega validacion para no mostrar equipos inactivos          
@9  07.08.2024  rotorres  Se agrega WITH(NOLOCK) a todo el sp          
@10 18.11.2024 rotorres Se agrega UNION para equipos huerfanos (Sin registro en tabla seccion)        
@11 19.11.2024 rotorres Se agrega UNION Para incluir equipos huerfanos en consultas de listar secciones             
@12 17.12.2024 rotorres se revierte cambio temporal de fecha fin      
@13 13.05.2025 rpineda se agrega las cuentas de supervision para TeamsMeets    
@14 22.07.2025 rpineda se agrega condicion de solo educacion continua para el equipo    
@15 30.07.2025 Hmora   Se agrega logica para carreras y Fc link de teams  
@16 10.08.2025 rpineda Se agrega logica para guardar el id del evento del link  
@17 19.08.2025 rpineda Se agrega logica para guardar los alumnos en el evento  
@18 25.09.2025 rpineda Regularizacion de Eventos 
@19 11.03.2026  alaureano Para la opción 16, se filtran los horarios proximos y se ordena por el numero
@20 13.03.2026 miquiroz se agrega nueva opcion 48 para la eliminacion del link de teams
@21 07.04.2026 rpineda exclusion de ids para piloto de APITeams V3
*/          
        
ALTER PROCEDURE [dbo].[cTeamsPorSeccion] @Opcion INT = NULL          
 ,@IdSeccion INT = NULL          
 ,@Sede VARCHAR(max) = NULL          
 ,@FechaMaximaAgendas VARCHAR(10) = NULL          
 ,@Email VARCHAR(100) = NULL          
 ,@idTeamsGroup VARCHAR(100) = NULL          
 ,@CodigoAlumno VARCHAR(100) = NULL          
 ,@Nombres VARCHAR(100) = NULL          
 ,@Apellidos VARCHAR(100) = NULL          
 ,@Tipo VARCHAR(100) = NULL          
 ,@CodigoSeccion VARCHAR(100) = NULL          
 ,@IsActive VARCHAR(1) = NULL          
 ,@propietario3 VARCHAR(100) = NULL          
 ,@propietario1 VARCHAR(100) = NULL          
 ,@propietario2 VARCHAR(100) = NULL          
 ,@propietario4 VARCHAR(100) = NULL          
 ,@nombreTeam VARCHAR(1000) = NULL          
 ,@descripcionTeam VARCHAR(1000) = NULL          
 ,@mailNickName VARCHAR(100) = NULL          
 ,@idSeccionSmart INT = NULL          
 ,@NroDias INT = NULL          
 ,@NumeroReunion INT = NULL          
 ,@CodigoSesion NVARCHAR(200) = NULL          
 ,@Fecha DATETIME = NULL          
 ,@Inicio NVARCHAR(50) = NULL          
 ,@Fin NVARCHAR(50) = NULL          
 ,@IdHorario INT = NULL          
 ,@IdEvento NVARCHAR(200) = NULL          
 ,@CodigoFacilitador NVARCHAR(100) = NULL          
 ,@JoinUrl NVARCHAR(400) = NULL         
 ,@TipoError NVARCHAR(20) = NULL    
 ,@ErrorLog NVARCHAR(max)=null    
 ,@Referencia NVARCHAR(max)=null    
AS          
SET NOCOUNT ON           
          
DECLARE @FechaIniDias INT = 14          
 ,@FechaFinDias INT = 14  --@12      
         
SELECT @FechaIniDias = CONVERT(INT, Valor)          
 ,@FechaFinDias = CONVERT(INT, Valor)        
FROM Parametro WITH(NOLOCK)          
WHERE Nombre = 'EsTeams'          
    
DECLARE @FechaIniDiasActivar INT = 5          
 ,@FechaFinDiasActivar INT = 5          
 ,@DiasAgenda INT = 8          
          
SELECT @FechaIniDiasActivar = CONVERT(INT, Valor)          
 ,@FechaFinDiasActivar = CONVERT(INT, Valor2)          
 ,@DiasAgenda = CONVERT(INT, Valor3)          
FROM Parametro  WITH(NOLOCK)          
WHERE Nombre = 'EsTeamsActivar'          
          
--INI @5          
declare @Empresa varchar(8);          
select top 1 @Empresa = companiasocio from Empresa WITH(NOLOCK) where IdEmpresa = 1          
declare @CodigoSede varchar(2)          
          
select @CodigoSede = case          
when @Empresa = '00002700' then 'SV'          
when @Empresa = '00002600' then 'VT'          
when @Empresa = '00002500' then 'VI'          
end          
          
DECLARE @SedeTMP TABLE (IdSede INT)          
          
IF @sede IS NULL          
BEGIN          
 INSERT INTO @SedeTMP          
 SELECT IdSede          
 FROM Sede WITH(NOLOCK)          
 WHERE Codigo = @CodigoSede          
END          
ELSE          
BEGIN          
 INSERT INTO @SedeTMP          
 SELECT IdSede       
 FROM Sede WITH(NOLOCK)          
 WHERE Codigo in (          
   SELECT value          
   FROM STRING_SPLIT(@Sede, ',')          
   );          
END          
--FIN @5          
          
IF @IdSeccion = 0          
BEGIN          
 SET @IdSeccion = NULL;          
END          

--INI @21
DECLARE @SeccionesOmitidas TABLE (IdSeccion INT PRIMARY KEY)

INSERT INTO @SeccionesOmitidas (IdSeccion)
VALUES (414994),
 (416759),
 (415002),
 (415010),
 (415018),
 (415026),
 (415034),
 (416760),
 (417429),
 (416761),
 (417431),
 (416756),
 (416758),
 (416757);

DECLARE @IdSeccionOmitida INT = COALESCE(@IdSeccion, @idSeccionSmart)

IF @IdSeccionOmitida IS NOT NULL
 AND EXISTS (
  SELECT 1
  FROM @SeccionesOmitidas SO
  WHERE SO.IdSeccion = @IdSeccionOmitida
  )
 AND @Opcion IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 18, 20, 21, 24, 30, 31, 37, 38, 45, 46, 47, 48)
BEGIN
 RETURN;
END
--FIN @21
          
IF @Opcion = - 1          
BEGIN          
 SELECT *          
 FROM AplicativosTeams WITH(NOLOCK)          
 WHERE Activo = 'A'          
  AND IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
END          
          
IF @Opcion = 0          
BEGIN          
 DELETE TeamsProgramacionGeneral          
 WHERE IdCurso = @IdSeccion          
          
 DELETE TeamsProgramacionAlumnos          
 WHERE IdCurso = @IdSeccion          
          
 INSERT INTO TeamsProgramacionGeneral          
 SELECT DISTINCT SD.IdSede          
  ,'NombreSede' = SD.Nombre          
  ,FA.IdFacultad          
  ,'NombreFacultad' = FA.Nombre    ,UN.IdUnidadNegocio          
  ,'NombreUnidadNegocio' = UN.Nombre          
  ,UA.IdUnidadAcademica          
  ,'NombreUnidadAcademica' = UA.Nombre          
  ,PE.IdPeriodo          
  ,'CodigoPeriodo' = PE.Codigo          
  ,PD.IdProducto          
  ,'NombreProducto' = PD.ProductoNombre          
  ,pr.IdPromocion          
  ,'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0')          
  ,PG.IdGrupo          
  ,PG.GrupoCodigo          
  ,'IdCurso' = SE.IdSeccion          
  ,'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) --@4          
  ,'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre          
  ,'Resumen' = CU.CursoNombre          
  ,'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailFacilitador' = ISNULL(FC.EmailInstitucion, '')          
  ,1          
  ,1          
  ,GETDATE()          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa --@4              
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1) --@4              
 /* OR (      --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    )  */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)   --@12      
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SE.IdSeccion = @IdSeccion;          
          
 INSERT INTO TeamsProgramacionAlumnos          
 SELECT DISTINCT SD.IdSede          
  ,'NombreSede' = SD.Nombre          
  ,FA.IdFacultad          
  ,'NombreFacultad' = FA.Nombre          
  ,UN.IdUnidadNegocio          
  ,'NombreUnidadNegocio' = UN.Nombre          
  ,UA.IdUnidadAcademica          
  ,'NombreUnidadAcademica' = UA.Nombre          
  ,PE.IdPeriodo          
  ,'CodigoPeriodo' = PE.Codigo          
  ,PD.IdProducto          
  ,'NombreProducto' = PD.ProductoNombre          
  ,pr.IdPromocion          
  ,'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0')          
  ,PG.IdGrupo          
  ,PG.GrupoCodigo          
  ,'IdCurso' = SE.IdSeccion          
  ,'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) --@4          
  ,'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre          
  ,'Resumen' = CU.CursoNombre          
  ,'CodigoAlumno' = AL.CodigoAnterior          
  ,'NombresAlumno' = REPLACE(REPLACE(AT.Nombres, 'Ñ', 'N'), '''', '')          
  ,'ApellidosAlumno' = REPLACE(REPLACE(AT.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(REPLACE(REPLACE(AT.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailAlumno' = AL.EmailInstitucion          
  ,'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailFacilitador' = ISNULL(FC.EmailInstitucion, '')          
  ,'Estado' = AC.estado          
  ,1          
  ,1          
  ,GETDATE()          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa --@4              
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion          
 LEFT JOIN Matricula M ON AC.IdMatricula = M.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 LEFT JOIN Alumno AL WITH (NOLOCK) ON AC.IdAlumno = AL.IdAlumno          
 LEFT JOIN Actor AT WITH (NOLOCK) ON AC.IdAlumno = AT.IdActor          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1) --@4              
 /*OR (    --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    ) */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SE.IdSeccion = @IdSeccion          
  AND AC.EsMatricula = 1          
  AND isnull(FC.CodigoAnterior, '') <> ''          
          
 DECLARE @IdTeams VARCHAR(1000)          
          
 SELECT @IdTeams = IdTeamsGroup          
 FROM TeamsEquipos WITH(NOLOCK)          
 WHERE IdSeccionSmart = @IdSeccion          
          
 SELECT DISTINCT ShortNameCurso AS MailNickname          
  ,NombreCurso + ' [' + M.NombreProducto + '][' + S.Codigo + ']' AS Nombre          
  ,'Descripcion' = CASE           
   WHEN LEN('SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador) >= 250          
   THEN SUBSTRING('SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + '->CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador, 1, 250)          
   ELSE 'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: '+ SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador          
   END          
  ,ES.Valor          
  ,ES.Valor2          
  ,M.CodigoFacilitador          
  ,M.NombresFacilitador          
  ,M.ApellidosFacilitador          
  ,M.IdSede          
  ,S.IdSeccion          
 FROM TeamsProgramacionGeneral M WITH(NOLOCK)          
 LEFT JOIN EmpresaSedeParametro ES  WITH(NOLOCK) ON (          
   ES.IdSede = M.IdSede          
   AND ES.Nombre = 'PROPIETARIOTINA'          
   AND M.IdUnidadNegocio = convert(INT, ES.Valor3)          
   )          
 LEFT JOIN Seccion S WITH(NOLOCK) ON (          
   S.IdSeccion = M.IdCurso          
   AND ISNULL(S.IdSeccion, '') <> ''          
   )          
 WHERE M.IdCurso = @IdSeccion          
  AND NOT EXISTS (          
   SELECT 1          
   FROM TeamsEquipos TE WITH(NOLOCK)          
   WHERE TE.IdSeccionSmart = M.IdCurso          
    AND TE.EstadoTeam = 'A'          
   )          
END          
          
IF @Opcion = 1          
BEGIN          
 --Se elimina la seccion si existe            
 DELETE TeamsProgramacionGeneral          
 WHERE IdCurso = @IdSeccion          
          
 DELETE TeamsProgramacionAlumnos          
 WHERE IdCurso = @IdSeccion          
          
 INSERT INTO TeamsProgramacionGeneral          
 SELECT DISTINCT SD.IdSede          
  ,'NombreSede' = SD.Nombre          
  ,FA.IdFacultad          
  ,'NombreFacultad' = FA.Nombre          
  ,UN.IdUnidadNegocio          
  ,'NombreUnidadNegocio' = UN.Nombre          
  ,UA.IdUnidadAcademica          
  ,'NombreUnidadAcademica' = UA.Nombre          
  ,PE.IdPeriodo          
  ,'CodigoPeriodo' = PE.Codigo          
  ,PD.IdProducto          
  ,'NombreProducto' = PD.ProductoNombre          
  ,pr.IdPromocion          
  ,'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0')          
  ,PG.IdGrupo          
  ,PG.GrupoCodigo          
  ,'IdCurso' = SE.IdSeccion          
  ,'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) --@4          
  ,--@2              
  'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre          
  ,'Resumen' = CU.CursoNombre          
  ,'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailFacilitador' = ISNULL(FC.EmailInstitucion, '')          
  ,1          
  ,1          
  ,GETDATE()          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa --@4              
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1) --@4              
 /*OR (      --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    )  */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
   PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SE.IdSeccion = @IdSeccion;          
          
 INSERT INTO TeamsProgramacionAlumnos          
 SELECT DISTINCT SD.IdSede          
  ,'NombreSede' = SD.Nombre          
  ,FA.IdFacultad          
  ,'NombreFacultad' = FA.Nombre          
  ,UN.IdUnidadNegocio          
  ,'NombreUnidadNegocio' = UN.Nombre          
  ,UA.IdUnidadAcademica          
  ,'NombreUnidadAcademica' = UA.Nombre          
  ,PE.IdPeriodo          
  ,'CodigoPeriodo' = PE.Codigo          
  ,PD.IdProducto          
  ,'NombreProducto' = PD.ProductoNombre          
  ,pr.IdPromocion          
  ,'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0')          
  ,PG.IdGrupo          
  ,PG.GrupoCodigo          
  ,'IdCurso' = SE.IdSeccion          
  ,'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) --@4          
  ,'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre          
  ,'Resumen' = CU.CursoNombre          
  ,'CodigoAlumno' = AL.CodigoAnterior          
  ,'NombresAlumno' = REPLACE(REPLACE(AT.Nombres, 'Ñ', 'N'), '''', '')          
  ,'ApellidosAlumno' = REPLACE(REPLACE(AT.Paterno, 'Ñ', 'N'), '''', '') + ' ' + ISNULL(REPLACE(REPLACE(AT.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailAlumno' = AL.EmailInstitucion          
  ,'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailFacilitador' = ISNULL(FC.EmailInstitucion, '')          
  ,'Estado' = AC.estado          
  ,1          
  ,1          
  ,GETDATE()          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa --@4              
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion          
 LEFT JOIN Matricula M WITH (NOLOCK) ON M.IdMatricula = AC.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 LEFT JOIN Alumno AL WITH (NOLOCK) ON AC.IdAlumno = AL.IdAlumno          
 LEFT JOIN Actor AT WITH (NOLOCK) ON AC.IdAlumno = AT.IdActor          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1) --@4              
 /*OR (      --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    )  */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SE.IdSeccion = @IdSeccion          
  AND AC.EsMatricula = 1          
  AND isnull(FC.CodigoAnterior, '') <> ''          
END          
          
IF @Opcion = 2          
BEGIN          
 BEGIN          
  --TRAE LOS GRUPOS DONDE EL FACILITADOR A SIDO CAMBIADO             
  SELECT @propietario4 = ES.Valor          
  FROM TeamsProgramacionGeneral M WITH(NOLOCK)         
  LEFT JOIN EmpresaSedeParametro ES  WITH(NOLOCK) ON (          
    ES.IdSede = M.IdSede          
    AND ES.Nombre = 'PROPIETARIOTINA'          
    AND M.IdUnidadNegocio = convert(INT, ES.Valor3)          
    )          
  LEFT JOIN Seccion S WITH(NOLOCK) ON (          
    S.IdSeccion = M.IdCurso          
    AND ISNULL(S.IdSeccion, '') <> ''          
    )          
  WHERE M.IdCurso = @IdSeccion          
   AND NOT EXISTS (          
    SELECT 1          
    FROM TeamsEquipos TE WITH(NOLOCK)          
    WHERE TE.IdSeccionSmart = M.IdCurso          
     AND TE.EstadoTeam = 'A'          
    )          
          
  UPDATE TeamsEquipos          
  SET Propietario3 = isnull(Propietario3, NULL)          
   ,Propietario4 = @propietario4          
  WHERE Propietario4 IS NULL          
   AND IdSeccionSmart = @IdSeccion          
 END          
          
 BEGIN          
  WITH dtFacilitadores          
  AS (          
   SELECT DISTINCT TE.IdTeamsGroup AS IdTeam          
    ,MPG.EmailFacilitador          
    ,MPG.CodigoFacilitador          
    ,MPG.NombresFacilitador          
    ,MPG.ApellidosFacilitador          
   FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
   LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)          
   WHERE TE.IdSeccionSmart = @IdSeccion          
    AND NOT EXISTS (          
     SELECT 1          
     FROM TeamsEquipos T WITH (NOLOCK)          
     WHERE T.IdSeccionSmart = MPG.IdCurso          
      AND Propietario3 = MPG.EmailFacilitador          
      AND EstadoTeam = 'A'          
     )          
    AND TE.EstadoTeam = 'A'          
   )          
  SELECT F.IdTeam          
   ,F.CodigoFacilitador          
   ,F.NombresFacilitador          
   ,F.ApellidosFacilitador          
   ,CASE tu.Propietario3          
    WHEN ''          
     THEN TU.Propietario3          
    ELSE SUBSTRING(TU.Propietario3, 1, CHARINDEX('@', TU.Propietario3) - 1)          
    END AS OldCodigoFacilitador          
  FROM dtFacilitadores F WITH (NOLOCK)          
  LEFT JOIN TeamsEquipos TU WITH (NOLOCK) ON (TU.IdTeamsGroup = F.IdTeam)          
  WHERE ISNULL(IdTeam, '') <> ''          
   AND TU.Propietario3 IS NOT NULL          
 END          
END          
          
IF @Opcion = 3          
BEGIN          
 --TRAE LOS CURSOS QUE FALTA ASIGNAR FACILITADOR            
 SELECT DISTINCT TE.IdTeamsGroup AS 'IdTeam'          
  ,MPG.CodigoFacilitador          
  ,MPG.NombresFacilitador          
  ,MPG.ApellidosFacilitador          
 FROM TeamsProgramacionGeneral MPG WITH (NOLOCK)          
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = MPG.IdCurso          
  AND TE.EstadoTeam = 'A'          
 WHERE TE.IdSeccionSmart = @IdSeccion          
  AND NOT EXISTS (          
   SELECT 1          
   FROM TeamsEquipos te WITH (NOLOCK)          
   WHERE te.IdSeccionSmart = @IdSeccion          
    AND te.EstadoTeam = 'A'          
    AND te.Propietario3 <> MPG.CodigoFacilitador          
   )          
END          
     
IF @Opcion = 4          
BEGIN          
 --TRAE LOS ALUMNOS QUE AUN NO HAN SIDO AGREGADOS AL TEAMS            
 SELECT TE.IdTeamsGroup          
  ,MPG.CodigoAlumno          
  ,MPG.NombresAlumno          
  ,MPG.ApellidosAlumno          
  ,MPG.EmailAlumno          
 FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)          
 WHERE TE.IdSeccionSmart = @IdSeccion          
  AND NOT EXISTS (          
   SELECT 1          
   FROM TeamsUsuarios TU WITH (NOLOCK)          
   WHERE TU.CodigoAlumno = MPG.CodigoAlumno          
    AND TU.idTeams = TE.IdTeamsGroup          
    AND TU.Tipo = 'A'          
    AND TU.Estado = 'A'          
   )          
  AND ISNULL(MPG.CodigoFacilitador, '') <> ''          
  AND TE.EstadoTeam = 'A';          
END          
          
IF @Opcion = 5          
BEGIN          
 WITH dtOldMembers          
 AS (          
  SELECT TE.IdTeamsGroup          
   ,TU.CodigoAlumno          
  FROM TeamsUsuarios TU WITH (NOLOCK)          
  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)          
  WHERE TE.IdSeccionSmart = @IdSeccion          
   AND NOT EXISTS (          
    SELECT 1          
    FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
    WHERE MPG.IdCurso = TE.IdSeccionSmart          
     AND MPG.CodigoAlumno = TU.CodigoAlumno          
    )          
   AND TU.Estado = 'A'          
   AND TU.Tipo = 'A'          
   AND TE.EstadoTeam = 'A'          
  )          
 SELECT *          
 FROM dtOldMembers          
 WHERE ISNULL(IdTeamsGroup, '') <> '';          
END          
          
IF @Opcion = 6          
BEGIN          
 --TRAE LOS TEAMS A ELIMINAR            
 SELECT TE.IdTeamsGroup          
 FROM TeamsEquipos TE WITH (NOLOCK)          
 INNER JOIN Seccion se WITH (NOLOCK) ON te.IdSeccionSmart = SE.IdSeccion          
 INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
 INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
 WHERE TE.IdSeccionSmart = @IdSeccion          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)     
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) >= CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND NOT EXISTS (          
   SELECT 1          
   FROM TeamsProgramacionGeneral TPG WITH(NOLOCK)          
   WHERE TPG.IdCurso = TE.IdSeccionSmart          
    AND EstadoTeam = 'A'          
   )          
  AND EstadoTeam = 'A'          
  UNION        
--@10 INICIO        
select TE.IdTeamsGroup from TeamsEquipos TE WITH (NOLOCK)        
where TE.IdSeccionSmart        
not in (select IdSeccion from Seccion WITH (NOLOCK) where IdSeccion = @IdSeccion)        
and TE.EstadoTeam = 'A'        
and TE.IdSeccionSmart = @IdSeccion        
AND (  left(NombreTeam,20) like '%2020%' OR left(NombreTeam,20) like '%2021%' OR left(NombreTeam,20) like '%2022%' OR left(NombreTeam,20) like '%2023%' )        
--@10 FIN        
 --union SELECT TE.IdTeamsGroup from TeamsEquipos TE left join Seccion S on TE.IdSeccionSmart=S.IdSeccion and S.IdSeccion is null where TE.EstadoTeam='A' and TE.IdSeccionSmart=@IdSeccion        
END          
          
IF @Opcion = 7          
BEGIN          
 --TRAEMOS LOS GRUPOS DONDE SE CAMBIO EL NOMBRE DEL GRUPO            
 WITH dtDetailTeam          
 AS (          
  SELECT TE.IdSeccionSmart          
   ,TE.NombreTeam          
   ,TE.DescripcionTeam          
  FROM TeamsEquipos TE WITH (NOLOCK)          
  WHERE TE.IdSeccionSmart = @IdSeccion          
            
  EXCEPT          
            
  SELECT DISTINCT M.IdCurso          
   ,NombreCurso + ' [' + M.NombreProducto + '][' + S.Codigo + ']' AS Nombre          
   ,'Descripcion' = CASE           
    WHEN LEN('SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador) >= 250          
     THEN SUBSTRING('SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador, 1, 250)          
    ELSE 'SEDE: ' + NombreSede + ' --> DIVISION: ' + NombreUnidadNegocio + ' --> PROGRAMA: ' + NombreUnidadAcademica + '-' + CodigoPeriodo + ' --> PRODUCTO: ' + NombreProducto + ' --> SEMESTRE: ' + Semestre + ' --> SECCION: ' + GrupoCodigo + ' --> CURSO: ' + SUBSTRING(NombreCurso, 1, 20) + ' - ' + CAST(M.IdCurso AS NVARCHAR) + ' --> PROFESOR: ' + CodigoFacilitador + ' - ' + NombresFacilitador          
    END          
  FROM TeamsProgramacionGeneral M WITH (NOLOCK)          
  LEFT JOIN EmpresaSedeParametro ES  WITH (NOLOCK) ON (ES.IdSede = M.IdSede)          
  LEFT JOIN Seccion S WITH (NOLOCK)  ON (S.IdSeccion = M.IdCurso)          
  WHERE ES.Nombre = 'PROPIETARIOTINA'          
   AND ISNULL(S.IdSeccion, '') <> ''          
  )          
 SELECT TE.IdTeamsGroup          
  ,DT.NombreTeam          
  ,DT.DescripcionTeam          
 FROM dtDetailTeam DT WITH (NOLOCK)          
 INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (DT.IdSeccionSmart = TE.IdSeccionSmart)          
 WHERE ISNULL(TE.IdTeamsGroup, '') <> ''          
  AND TE.EstadoTeam = 'A';          
END          
          
IF @Opcion = 8          
BEGIN          
 --Traemos los equipos creados            
 SELECT *          
 FROM TeamsEquipos WITH(NOLOCK)          
 WHERE IdSeccionSmart = @IdSeccion;          
END          
          
IF @Opcion = 9          
BEGIN          
 DECLARE @TEMPMEMBER TABLE (          
  IdTeams NVARCHAR(200)          
  ,EmailAppTeam NVARCHAR(100)          
  ,IdEvento NVARCHAR(200)          
  ,IdCurso INT          
  ,IdHorario INT          
  ,Codigo NVARCHAR(20)          
  ,NumeroReunion INT          
  ,CodigoAlumno NVARCHAR(20)          
  ,CodigoAnterior NVARCHAR(20)          
  ,EmailInstitucion NVARCHAR(100)          
  );          
          
 WITH dtOldMembers          
 AS (          
  SELECT DISTINCT TH.IdTeams          
   ,'EmailAppTeam' = TE.Propietario2          
   ,TH.IdEvento          
   ,TH.IdCurso          
   ,TH.IdHorario          
   ,TH.Codigo          
   ,TH.NumeroReunion          
   ,TH.CodigoAlumno          
   ,TH.CodigoFacilitador          
   ,TH.CorreoFacilitador          
  FROM TeamsHorarios TH WITH (NOLOCK)          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)          
  WHERE te.IdSeccionSmart = @IdSeccion          
   AND NOT EXISTS (          
    SELECT 1          
    FROM TeamsUsuarios TU WITH (NOLOCK)          
    WHERE TU.CodigoAlumno = TH.CodigoAlumno          
     AND TU.idTeams = TU.idTeams          
     AND TU.Estado = 'A'          
     AND TU.Tipo = 'A'          
    )          
   AND TH.Estado = 'A'          
  )          
 INSERT INTO @TEMPMEMBER          
 SELECT DISTINCT *          
 FROM dtOldMembers;          
          
 MERGE TeamsHorarios AS TARGET          
 USING @TEMPMEMBER AS SOURCE          
  ON (          
    TARGET.idTeams = SOURCE.IdTeams          
    AND TARGET.idEvento = SOURCE.IdEvento          
    )          
 WHEN MATCHED          
  AND TARGET.IdCurso = SOURCE.IdCurso          
  AND TARGET.IdHorario = SOURCE.IdHorario          
  AND TARGET.Codigo = SOURCE.Codigo          
  AND TARGET.NumeroReunion = SOURCE.NumeroReunion          
  AND TARGET.CodigoAlumno = SOURCE.CodigoAlumno          
  THEN          
   UPDATE          
   SET TARGET.Estado = 'I'          
    ,TARGET.UsuarioModificacion = 1          
    ,TARGET.FechaModificacion = GETDATE();          
          
 --RETORNAMOS LOS DATOS A ACTUALIZAR            
 SELECT *          
 FROM @TEMPMEMBER;          
END          
          
IF @Opcion = 10          
BEGIN          
 DECLARE @TEMP TABLE (          
  IdTeams NVARCHAR(200)          
  ,EmailAppTeam NVARCHAR(100)          
  ,IdSeccion INT          
  ,IdEvento NVARCHAR(200)          
  ,Numero INT          
  ,IdHorario INT          
  ,Codigo NVARCHAR(20)          
  ,Fecha DATETIME          
  ,Inicio INT          
  ,Fin INT          
  ,CodigoAlumno NVARCHAR(20)          
  ,Email NVARCHAR(100)          
  ,CodigoAnterior NVARCHAR(20)          
  ,EmailInstitucion NVARCHAR(100)          
  ,Estado NVARCHAR(1)          
  ,UsuarioCreacion INT          
  ,FechaCreacion DATETIME          
  );          
          
 --Traemos los alumnos que deben agregarse al evento            
 WITH dtNewMembers          
 AS (          
  SELECT DISTINCT TU.CodigoAlumno          
   ,TE.IdTeamsGroup          
   ,HS.IdHorario          
   ,HS.Numero          
   ,HS.IdSeccion          
   ,S.Codigo          
  FROM TeamsUsuarios TU WITH (NOLOCK)          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdTeamsGroup = TU.idTeams          
  INNER JOIN HorarioSesion HS WITH (NOLOCK) ON HS.IdSeccion = TE.IdSeccionSmart          
  INNER JOIN Seccion S WITH (NOLOCK) ON S.IdSeccion = TE.IdSeccionSmart          
  WHERE te.IdSeccionSmart = @IdSeccion          
   AND convert(VARCHAR, Fecha, 112) BETWEEN convert(VARCHAR, getdate(), 112)          
    AND convert(VARCHAR, @FechaMaximaAgendas, 112)          
   AND TE.EstadoTeam = 'A'          
   AND TU.Tipo = 'A'          
   AND TU.Estado = 'A'          
   AND NOT EXISTS (          
    SELECT 1          
    FROM TeamsHorarios TH WITH (NOLOCK)          
    WHERE TH.CodigoAlumno = TU.CodigoAlumno          
     AND TH.IdTeams = TE.IdTeamsGroup          
     AND TH.IdHorario = HS.IdHorario          
     AND TH.NumeroReunion = HS.Numero          
     AND TH.IdCurso = HS.IdSeccion          
     AND TH.Estado = 'A'          
    )          
  )          
  ,          
  --CREAMOS LOS EVENTOS PARA LOS NUEVOS MIEMBROS            
 dtNewEvents          
 AS (          
  SELECT DISTINCT NM.IdTeamsGroup          
   ,'EmailAppTeam' = TE.Propietario2          
   ,NM.IdSeccion          
   ,TH.IdEvento          
   ,NM.Numero          
   ,NM.IdHorario          
   ,TH.Codigo          
   ,TH.Fecha          
   ,TH.Inicio          
   ,TH.Fin          
   ,TU.CodigoAlumno          
   ,TU.Email          
   ,TH.CodigoFacilitador          
   ,TH.CorreoFacilitador          
   ,Estado = 'A'          
   ,UsuarioCreacion = 1          
   ,FechaCreacion = GETDATE()          
  FROM dtNewMembers NM WITH (NOLOCK)          
  INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON (          
    TH.IdCurso = NM.IdSeccion          
    AND TH.IdHorario = NM.IdHorario          
    AND TH.IdTeams = NM.IdTeamsGroup          
    )          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)          
  INNER JOIN TeamsUsuarios TU WITH (NOLOCK) ON (          
    TU.idTeams = NM.IdTeamsGroup          
    AND TU.CodigoAlumno = NM.CodigoAlumno          
    )          
  WHERE convert(VARCHAR, Fecha, 112) BETWEEN convert(VARCHAR, getdate(), 112)          
    AND convert(VARCHAR, @FechaMaximaAgendas, 112)          
  )          
 --GUARDAMOS LOS EVENTOS EN UNA TABLA TEMPORAL            
 INSERT INTO @TEMP          
 SELECT *          
 FROM dtNewEvents;          
          
 SELECT DISTINCT T.IdTeams          
  ,T.EmailAppTeam          
  ,T.CodigoAnterior          
  ,T.EmailInstitucion          
  ,T.IdEvento          
 FROM @TEMP T;          
          
 MERGE TeamsHorarios AS TARGET          
 USING @TEMP AS SOURCE          
  ON (          
    TARGET.CodigoAlumno = SOURCE.CodigoAlumno          
    AND TARGET.idTeams = SOURCE.IdTeams          
    AND TARGET.idEvento = SOURCE.IdEvento          
    )          
 WHEN MATCHED          
  AND TARGET.Fecha = SOURCE.Fecha          
  AND TARGET.Inicio = SOURCE.Inicio          
  AND TARGET.Fin = SOURCE.Fin          
  AND TARGET.idHorario = SOURCE.idHorario          
  THEN          
   UPDATE          
   SET TARGET.Estado = 'A'          
 WHEN NOT MATCHED BY TARGET          
  THEN          
   --INSERTAMOS LOS EVENTOS A LOS NUEVOS MIEMBROS            
   INSERT (          
    IdTeams          
    ,IdCurso          
    ,IdEvento          
    ,NumeroReunion          
    ,IdHorario          
    ,Codigo          
    ,Fecha          
    ,Inicio          
    ,Fin          
    ,CodigoAlumno          
    ,CorreoAlumno          
    ,CodigoFacilitador          
    ,CorreoFacilitador          
    ,Estado          
    ,UsuarioCreacion          
    ,FechaCreacion          
    )          
   VALUES (          
    SOURCE.IdTeams          
    ,SOURCE.IdSeccion          
    ,SOURCE.IdEvento          
    ,SOURCE.Numero          
    ,SOURCE.IdHorario          
    ,SOURCE.Codigo          
    ,SOURCE.Fecha          
    ,SOURCE.Inicio          
    ,SOURCE.Fin          
    ,SOURCE.CodigoAlumno          
    ,SOURCE.Email          
    ,SOURCE.CodigoAnterior          
    ,SOURCE.EmailInstitucion          
    ,SOURCE.Estado          
    ,SOURCE.UsuarioCreacion          
    ,SOURCE.FechaCreacion          
    );          
          
 --RETORNAMOS LOS GRUPOS QUE SE DEBEN ACTUALIZAR            
 SELECT DISTINCT T.IdTeams          
  ,T.EmailAppTeam          
  ,T.CodigoAnterior          
  ,T.EmailInstitucion          
  ,T.IdEvento          
 FROM @TEMP T;          
END          
          
IF @Opcion = 11          
BEGIN          
UPDATE SeccionHorario set UrlClaseVirtual = @JoinUrl ,IdEvento= @IdEvento --@16  
where IdSeccion = @IdSeccion    
END          
          
IF @Opcion = 12          
BEGIN          
 --ACTUALIZAMOS LOS EVENTOS DONDE LA FECHA FUE CAMBIADA            
 DECLARE @TEMPFECHAS TABLE (          
  IdTeams NVARCHAR(200)          
  ,EmailAppTeam NVARCHAR(100)          
  ,IdEvento NVARCHAR(200)          
  ,IdCurso INT          
  ,IdHorario INT          
  ,Codigo NVARCHAR(20)          
  ,NumeroReunion INT          
  ,Fecha DATETIME          
  ,Inicio INT     
  ,Fin INT          
  );          
          
 WITH dtNewDates          
 AS (          
  SELECT DISTINCT TH.IdTeams          
,'EmailAppTeam' = TE.Propietario2          
   ,TH.IdEvento          
   ,TH.IdCurso          
   ,TH.IdHorario          
   ,TH.Codigo          
   ,TH.NumeroReunion          
   ,HS.Fecha          
   ,HS.Inicio          
   ,HS.Fin          
  FROM HorarioSesion HS WITH (NOLOCK)          
  INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON TH.IdCurso = HS.IdSeccion          
   AND TH.IdHorario = HS.IdHorario          
   AND TH.NumeroReunion = HS.Numero          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)          
  WHERE HS.idseccion = @IdSeccion          
   AND convert(VARCHAR, HS.Fecha, 112) >= convert(VARCHAR, getdate(), 112)          
   AND (          
    TH.Fecha <> HS.Fecha          
    OR TH.Inicio <> HS.Inicio          
    OR TH.Fin <> HS.Fin          
    )          
   AND TH.Estado = 'A'          
  )          
 INSERT INTO @TEMPFECHAS          
 SELECT DISTINCT *          
 FROM dtNewDates;          
          
 MERGE TeamsHorarios AS TARGET          
 USING @TEMPFECHAS AS SOURCE          
  ON (          
    TARGET.idTeams = SOURCE.IdTeams          
    AND TARGET.idEvento = SOURCE.IdEvento          
    )          
 WHEN MATCHED          
  AND TARGET.IdCurso = SOURCE.IdCurso          
  AND TARGET.IdHorario = SOURCE.IdHorario          
  AND TARGET.Codigo = SOURCE.Codigo          
  AND TARGET.NumeroReunion = SOURCE.NumeroReunion          
  THEN          
   UPDATE          
   SET TARGET.Fecha = SOURCE.Fecha          
    ,TARGET.Inicio = SOURCE.Inicio          
    ,TARGET.Fin = SOURCE.Fin          
    ,TARGET.UsuarioModificacion = 1          
    ,TARGET.FechaModificacion = GETDATE();          
          
 --RETORNAMOS LOS DATOS A ACTUALIZAR            
 SELECT *          
 FROM @TEMPFECHAS;          
END          
          
IF @Opcion = 13          
BEGIN          
 --ACTUALIZAMOS LOS EVENTOS DONDE EL FACILITADOR FUE CAMBIADO            
 DECLARE @TEMPFACILITADOR TABLE (          
  IdTeams NVARCHAR(200)          
  ,EmailAppTeam NVARCHAR(100)          
  ,IdEvento NVARCHAR(200)          
  ,IdCurso INT          
  ,IdHorario INT          
  ,Codigo NVARCHAR(20)          
  ,NumeroReunion INT          
  ,CodigoAnterior NVARCHAR(50)          
  ,EmailInstitucion NVARCHAR(200)          
  );          
          
 WITH dtNewFacilitador          
 AS (          
  SELECT DISTINCT TH.IdTeams          
   ,'EmailAppTeam' = TE.Propietario2          
   ,TH.IdEvento          
   ,TH.IdCurso          
   ,TH.IdHorario          
   ,TH.Codigo          
   ,TH.NumeroReunion          
   ,F.CodigoAnterior          
   ,F.EmailInstitucion          
  FROM HorarioSesion HS WITH (NOLOCK)          
  INNER JOIN TeamsHorarios TH WITH (NOLOCK) ON (          
    TH.IdCurso = HS.IdSeccion          
    AND TH.IdHorario = HS.IdHorario          
    AND TH.NumeroReunion = HS.Numero          
    )          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TH.IdTeams)          
  INNER JOIN Actor A WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = A.IdActor          
  INNER JOIN Facilitador F WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = F.IdFacilitador          
  WHERE HS.idseccion = @IdSeccion          
   AND convert(VARCHAR, HS.Fecha, 112) >= convert(VARCHAR, getdate(), 112)          
   AND F.CodigoAnterior <> TH.CodigoFacilitador          
   AND TH.Estado = 'A'          
  )          
 INSERT INTO @TEMPFACILITADOR          
 SELECT *          
 FROM dtNewFacilitador;          
          
 MERGE TeamsHorarios AS TARGET          
 USING @TEMPFACILITADOR AS SOURCE          
  ON (          
    TARGET.idTeams = SOURCE.IdTeams          
    AND TARGET.idEvento = SOURCE.IdEvento          
    )          
 WHEN MATCHED          
  AND TARGET.IdCurso = SOURCE.IdCurso          
  AND TARGET.IdHorario = SOURCE.IdHorario          
  AND TARGET.Codigo = SOURCE.Codigo          
  AND TARGET.NumeroReunion = SOURCE.NumeroReunion          
  THEN          
   UPDATE          
   SET TARGET.CodigoFacilitador = SOURCE.CodigoAnterior          
    ,TARGET.CorreoFacilitador = SOURCE.EmailInstitucion          
    ,TARGET.UsuarioModificacion = 1          
    ,TARGET.FechaModificacion = GETDATE();          
          
 --RETORNAMOS LOS DATOS A ACTUALIZAR            
 SELECT *          
 FROM @TEMPFACILITADOR;          
END          
          
IF @Opcion = 14          
BEGIN          
 SELECT *          
 FROM TeamsEquipos TE WITH (NOLOCK)          
 INNER JOIN Seccion SE WITH (NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 WHERE IdSeccionSmart = @IdSeccion          
  AND EstadoTeam = 'A'          
  AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )          
END          
          
IF @Opcion = 15          
BEGIN          
 SELECT TOP 1 propietario2          
  ,'Total' = count(1)          
 FROM AplicativosTeams A WITH (NOLOCK)          
 LEFT JOIN TeamsEquipos te WITH (NOLOCK) ON te.Propietario2 = a.UsernameApp
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = TE.IdSeccionSmart
   )
  --FIN @21          
 WHERE A.Activo = 'A'          
 GROUP BY Propietario2          
 ORDER BY Total ASC          
END          
          
IF @Opcion = 16          
    
BEGIN     
 WITH dtEvents          
 AS (          
  SELECT TOP 1 HS.IdSeccion          
   ,HS.IdHorario          
   ,HS.Numero          
   ,S.Codigo          
  FROM HorarioSesion HS WITH (NOLOCK)          
  INNER JOIN Seccion S WITH (NOLOCK) ON HS.IdSeccion = S.IdSeccion          
  INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = HS.IdSeccion          
  INNER JOIN TeamsUsuarios TU WITH (NOLOCK) ON TU.idTeams = TE.IdTeamsGroup          
  WHERE HS.idseccion = @IdSeccion       
    AND HS.Estado <> 'X'--@19
   AND HS.Fecha >= CONVERT(DATE,GETDATE()) --@19   
   AND ISNULL(TU.CodigoAlumno, '') <> ''          
   AND TU.Tipo = 'A'          
   AND not EXISTS (          
    SELECT top 1 1        
    FROM SeccionHorario  TH WITH (NOLOCK)          
    WHERE TH.idseccion = @IdSeccion    
 and TH.UrlClaseVirtual <> ''         
    )         
    ORDER BY HS.Numero --@19
  )
  
 SELECT top 1  TE.IdTeamsGroup          
  ,'EmailAppTeam' = TE.Propietario2          
  ,HS.IdHorario          
  ,S.Codigo          
  ,E.Numero          
  ,HS.Fecha          
  ,HS.Inicio          
  ,HS.Fin          
  ,MPG.IdCurso          
  ,A.NombreCompleto          
  ,FA.CodigoAnterior          
  ,FA.EmailInstitucion          
  ,MPG.NombresFacilitador          
  ,MPG.EmailFacilitador          
  ,MPG.Resumen AS Content          
  ,TE.NombreTeam AS SubjectMeet          
  ,TE.DescripcionTeam          
  ,TE.MailNickName          
  ,TE.IsActive    
  ,UN.IdUnidadNegocio    
  ,'CuentaCarreras' =PE1.Valor2 --@13    
  ,'CuentaExtension' = PE2.Valor4 --@13    
      
 FROM dtEvents E WITH (NOLOCK)          
 INNER JOIN HorarioSesion HS WITH (NOLOCK) ON      
   HS.IdSeccion = E.IdSeccion          
   AND HS.IdHorario = E.IdHorario          
     
 INNER JOIN Seccion S WITH (NOLOCK) ON S.IdSeccion = E.IdSeccion          
 INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON TE.IdSeccionSmart = E.IdSeccion          
 INNER JOIN Actor A WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = A.IdActor          
 INNER JOIN Facilitador FA WITH (NOLOCK) ON ISNULL(HS.IdActorReemplazo, HS.IdActorProgramado) = FA.IdFacilitador          
 INNER JOIN TeamsProgramacionAlumnos MPG WITH (NOLOCK) ON (MPG.IdCurso = S.IdSeccion)       
 INNER JOIN Promocion PR WITH (NOLOCK) ON S.IdPromocion = PR.IdPromocion --@13   
 INNER JOIN PromocionGrupo PG on PG.IdPromocion=S.IdPromocion and PG.IdGrupo=S.IdGrupo --@14  
 INNER JOIN Periodo PE on PR.IdPeriodo=PE.IdPeriodo --@14  
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio  --@13    
 LEFT JOIN ParametroEmpresa PE1 WITH (NOLOCK) ON UN.IdUnidadNegocio =CONVERT(int, PE1.Valor) and PE1.Nombre = 'TEAMSMEET' --@13    
 LEFT JOIN ParametroEmpresa PE2 WITH (NOLOCK) ON UN.IdUnidadNegocio =CONVERT(int, PE2.Valor3) and PE2.Nombre = 'TEAMSMEET' --@13    
 WHERE TE.EstadoTeam = 'A'  
 --@14        
 and PG.Estado='A'   
 AND CONVERT(VARCHAR,GETDATE(),112) > = CASE  
  WHEN un.TipoServicio = 'C'  THEN  
       CONVERT(VARCHAR,dateadd(D,-@FechaIniDias,PE.Inicio),112)  
  ELSE   
    CONVERT(VARCHAR,dateadd(D,-@FechaIniDias,S.FechaInicio),112)    
  END  
 AND CONVERT(VARCHAR,GETDATE(),112) < = CASE  
  WHEN un.TipoServicio = 'C'  THEN  
       CONVERT(VARCHAR,dateadd(D,@FechaFinDias,PE.Fin),112)   
  ELSE  
    CONVERT(VARCHAR,dateadd(D,@FechaFinDias,S.FechaFin),112)    
  END    
 --@14    
 end    
IF @Opcion = 17          
BEGIN          
 SELECT DISTINCT te.NombreTeam          
  ,te.Propietario3          
  ,TH.NumeroReunion          
  ,th.Fecha          
  ,th.Inicio          
  ,th.fin          
  ,th.JoinUrl          
 FROM TeamsHorarios TH WITH (NOLOCK)          
 INNER JOIN TeamsEquipos TE WITH (NOLOCK) ON th.IdTeams = te.IdTeamsGroup          
 WHERE te.IdSeccionSmart = @IdSeccion          
  AND te.EstadoTeam = 'A'          
  AND Th.Estado = 'A'          
 ORDER BY 3          
  ,4          
END          
          
IF @Opcion = 18          
BEGIN          
 --TRAE LOS ALUMNOS QUE AUN NO HAN SIDO AGREGADOS AL TEAMS            
 SELECT TE.IdTeamsGroup          
  ,MPG.CodigoAlumno          
  ,MPG.NombresAlumno          
  ,MPG.ApellidosAlumno          
  ,'Existe' = isnull(tu.idUsuario, 0)          
 FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)          
 LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams          
  AND MPG.CodigoAlumno = tu.CodigoAlumno          
  AND tu.Tipo = 'A'          
 WHERE TE.IdSeccionSmart = @IdSeccion          
  AND TE.EstadoTeam = 'A'          
END          
          
IF @Opcion = 19          
BEGIN          
 -- TRAE TODOS LOS CURSOS DE SMART            
 DECLARE @tmp AS TABLE (IdCurso INT)          
          
 INSERT INTO @tmp          
 SELECT DISTINCT SE.IdSeccion          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1)          
 /* OR (    --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    )  */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )          
           
 INSERT INTO @tmp          
 SELECT IdSeccionSmart          
 FROM TeamsEquipos TE WITH (NOLOCK)          
 INNER JOIN Seccion SE WITH (NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 WHERE EstadoTeam = 'A'          
  AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = TE.IdSeccionSmart
   )          
--@11 INICIO        
UNION        
 select TE.IdSeccionSmart from TeamsEquipos TE WITH (NOLOCK)          
 LEFT JOIN Seccion S WITH (NOLOCK)   on S.IdSeccion = TE.IdSeccionSmart        
 Where          
 TE.EstadoTeam = 'A'        
 AND S.IdSeccion is null        
 AND NOT EXISTS (
  SELECT 1
  FROM @SeccionesOmitidas SO
  WHERE SO.IdSeccion = TE.IdSeccionSmart
  )
  --FIN @21        
--@11 FIN        
 SELECT DISTINCT *          
 FROM @tmp          
END          
          
IF @Opcion = 20          
BEGIN          
 IF ISNULL(@Email, '') <> ''          
 BEGIN          
  DECLARE @existUser INT          
          
  SET @existUser = (          
    SELECT COUNT(1)          
    FROM TeamsUsuarios WITH (NOLOCK)          
    WHERE idTeams = @idTeamsGroup          
     AND CodigoAlumno = @CodigoAlumno          
    );          
          
  IF @existUser > 0          
  BEGIN          
   UPDATE TeamsUsuarios          
   SET Nombres = @Nombres          
    ,Apellidos = @Apellidos          
    ,Email = @Email          
    ,Estado = 'A'          
    ,UsuarioModificacion = 1          
    ,FechaModificacion = GETDATE()          
   WHERE CodigoAlumno = @CodigoAlumno          
    AND idTeams = @idTeamsGroup          
  END          
  ELSE          
  BEGIN          
   --INSERTA LOS NUEVOS ALUMNOS            
   INSERT INTO TeamsUsuarios (          
    idTeams          
    ,CodigoAlumno          
    ,Nombres          
    ,Apellidos          
    ,Email          
    ,Tipo          
    ,Estado          
    ,UsuarioCreacion          
    ,FechaCreacion          
    )          
   VALUES (          
    @idTeamsGroup          
    ,@CodigoAlumno          
    ,@Nombres          
    ,@Apellidos          
    ,@Email          
    ,@Tipo          
    ,'A'          
    ,1          
    ,GETDATE()          
    );          
END          
 END          
END          
          
IF @Opcion = 21          
BEGIN          
 --ELIMINA MIEMBROS DEL EQUIPOS            
 UPDATE TeamsUsuarios          
 SET Estado = 'I'          
  ,UsuarioModificacion = 1          
  ,FechaModificacion = GETDATE()          
 WHERE CodigoAlumno = @CodigoAlumno          
  AND idTeams = @idTeamsGroup;          
END          
          
IF @Opcion = 22          
BEGIN          
 SELECT DISTINCT 'Sede' = SD.Nombre          
  ,'Division' = UN.Nombre          
  ,'Programa' = UA.Nombre          
  ,'Periodo' = Pe.Codigo          
  ,'Producto' = PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,'Seccion' = PG.GrupoCodigo          
  ,'CursoCodigo' = SE.Codigo          
  ,'CursoNombre' = cu.CursoNombre          
  ,'EstadoCursoHorario' = se.Estado          
  ,'InicioPeriodo' = CONVERT(VARCHAR, pg.FechaInicio, 104)          
  ,'FinPeriodo' = CONVERT(VARCHAR, pg.FechaFin, 104)          
  ,'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, '')          
  ,'FacilitadorNombre' = ISNULL(AR.NombreCompleto, '')          
  ,'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion)          
  ,'FechaSesion' = CONVERT(VARCHAR, HS.Fecha, 104)          
  ,'HoraInicio' = DBO.gHhMmStr(HS.Inicio)          
  ,'HoraFin' = DBO.gHhMmStr(HS.Fin)          
  ,'Horas' = CAST(ISNULL((CONVERT(INT, (datediff(MINUTE, dbo.gHhMmStr(HS.Inicio), dbo.gHhMmStr(HS.Fin))))), '0') AS DECIMAL) / 60          
  ,te.NombreTeam          
  ,te.Propietario2          
 FROM HorarioSesion HS WITH (NOLOCK)          
 INNER JOIN Seccion SE WITH (NOLOCK) ON HS.IdSeccion = SE.IdSeccion          
 INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
 INNER JOIN AlumnoCurso AC WITH (NOLOCK) ON ac.IdSeccion = SE.IdSeccion          
 INNER JOIN Matricula M ON AC.IdMatricula = M.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 INNER JOIN Alumno a ON a.IdAlumno = ac.IdAlumno          
 INNER JOIN actor act ON a.IdAlumno = act.IdActor          
 INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
 INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
  AND SP.EsResponsable = 1          
 LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
 LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
 LEFT JOIN TeamsEquipos TE ON te.IdSeccionSmart = se.IdSeccion          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND convert(VARCHAR, HS.Fecha, 112) BETWEEN convert(VARCHAR, getdate(), 112)          
   AND convert(VARCHAR, getdate() + @NroDias, 112)          
  AND NOT EXISTS (          
   SELECT 1          
   FROM Feriado F WITH (NOLOCK)          
   WHERE F.IdEmpresa = PR.IdEmpresa          
    AND F.IdSede = PR.IdSede          
    AND F.Fecha = HS.Fecha          
    )          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )
  --FIN @21          
  ORDER BY FechaSesion          
  ,HoraInicio          
  ,CursoNombre          
END          
          
IF @Opcion = 23          
BEGIN          
 SELECT DISTINCT SD.IdSede          
  ,'NombreSede' = SD.Nombre          
  ,FA.IdFacultad          
  ,'NombreFacultad' = FA.Nombre          
  ,UN.IdUnidadNegocio          
  ,'NombreUnidadNegocio' = UN.Nombre          
  ,UA.IdUnidadAcademica          
  ,'NombreUnidadAcademica' = UA.Nombre          
  ,PE.IdPeriodo          
  ,'CodigoPeriodo' = PE.Codigo          
  ,PD.IdProducto          
  ,'NombreProducto' = PD.ProductoNombre          
  ,pr.IdPromocion          
  ,'Semestre' = ISNULL(mtr.Nombre, 'MODULO 0')          
  ,PG.IdGrupo          
  ,PG.GrupoCodigo          
  ,'IdCurso' = SE.IdSeccion          
  ,'ShortNameCurso' = PD.ProductoCodigo + '.' + isnull(CAST(PR.IdCurricula AS VARCHAR(10)), '00') + '.' + CAST(CU.IdCurso AS VARCHAR(10)) + '.' + REPLACE(PE.Codigo, '-', '') + '-' + CAST(SE.IdSeccion AS VARCHAR(10)) --@4          
  ,--@2            
  'NombreCurso' = PG.GrupoCodigo + ' ' + CU.CursoNombre          
  ,'Resumen' = CU.CursoNombre ----&gt; información obtenida de Smart.<p/>'            
  ,'CodigoFacilitador' = ISNULL(FC.CodigoAnterior, '')          
  ,'NombresFacilitador' = ISNULL(REPLACE(REPLACE(AT2.Nombres, 'Ñ', 'N'), '''', ''), '') + ',' + ISNULL(REPLACE(REPLACE(AT2.Paterno, 'Ñ', 'N'), '''', ''), '') + ' ' + ISNULL(REPLACE(REPLACE(AT2.Materno, 'Ñ', 'N'), '''', ''), '')          
  ,'EmailFacilitador' = ISNULL(FC.EmailInstitucion, '')          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa --@4            
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN Facultad FA WITH (NOLOCK) ON PR.IdFacultad = FA.IdFacultad          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PD WITH (NOLOCK) ON PR.IdProducto = PD.IdProducto          
 LEFT JOIN Curriculamodulo CM WITH (NOLOCK) ON PR.IdModulo = CM.IdModulo          
  AND PR.IdCurricula = CM.IdCurricula          
 LEFT JOIN MaestroTablaRegistro MTR WITH (NOLOCK) ON CM.IdTipoModulo = MTR.IdMaestroRegistro          
 LEFT JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 LEFT JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 LEFT JOIN Ambiente AM WITH (NOLOCK) ON SE.IdAmbiente = AM.IdAmbiente          
 LEFT JOIN Actor CO WITH (NOLOCK) ON PG.IdCoordinador = CO.IdActor          
 LEFT JOIN Turno TU WITH (NOLOCK) ON PG.IdTurno = TU.IdTurno          
 LEFT JOIN MaestroTablaRegistro MTR2 WITH (NOLOCK) ON TU.IdTipoTurno = mtr2.IdMaestroRegistro          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SP.IdSeccion = SE.IdSeccion          
  AND (SP.EsResponsable = 1) --@4            
 /* OR (     --@3          
    SP.EsResponsable = 4            
    AND EM.CompaniaSocio = '00002600'            
    )  */          
 LEFT JOIN Actor AT2 WITH (NOLOCK) ON SP.IdActor = AT2.IdActor          
 LEFT JOIN Facilitador FC WITH (NOLOCK) ON SP.IdActor = FC.IdFacilitador          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND SE.Codigo = @CodigoSeccion          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )
  --FIN @21          
  AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
END          
          
IF @Opcion = 24          
BEGIN          
 SELECT 'Sede' = SD.Nombre          
  ,'Division' = UN.Nombre          
  ,'Programa' = UA.Nombre          
  ,'Periodo' = Pe.Codigo          
  ,'Producto' = PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,'Seccion' = PG.GrupoCodigo          
  ,'CursoCodigo' = SE.Codigo          
  ,'CursoNombre' = cu.CursoNombre          
  ,'EstadoCursoHorario' = se.Estado          
  ,'Inicio' = CONVERT(VARCHAR, pg.FechaInicio, 104)          
  ,'Fin' = CONVERT(VARCHAR, pg.FechaFin, 104)          
  ,'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, '')          
  ,'FacilitadorNombre' = ISNULL(AR.NombreCompleto, '')          
  ,'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion)          
  ,'TotalAlumnos' = COUNT(AC.IdAlumno)          
  ,'IdTeamsGroup' = te.IdTeamsGroup          
  ,te.NombreTeam          
  ,SE.IdSeccion          
 FROM Seccion SE WITH (NOLOCK)          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
  AND SP.EsResponsable = 1          
 LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
 LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 INNER JOIN Curso CU WITH (NOLOCK) ON SE.IdCurso = CU.IdCurso          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo          
 INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
 LEFT JOIN AlumnoCurso AC WITH (NOLOCK) ON SE.IdSeccion = AC.IdSeccion          
  AND AC.EsMatricula = 1          
 INNER JOIN Matricula M ON AC.IdMatricula = M.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON SE.IdSeccion = TE.IdSeccionSmart          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND ISNULL(@IdSeccion, SE.IdSeccion) = SE.IdSeccion          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )
  --FIN @21          
 GROUP BY SD.Nombre          
  ,UN.Nombre          
  ,UA.Nombre          
  ,Pe.Codigo          
  ,PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,PG.GrupoCodigo          
  ,SE.Codigo          
  ,cu.CursoNombre          
  ,se.Estado          
  ,CONVERT(VARCHAR, pg.FechaInicio, 104)          
  ,CONVERT(VARCHAR, pg.FechaFin, 104)          
  ,ISNULL(FA.CodigoAnterior, '')          
  ,ISNULL(AR.NombreCompleto, '')          
  ,dbo.gFrecuenciaSeccionHorario(SE.IdSeccion)          
  ,te.IdTeamsGroup          
  ,te.NombreTeam          
  ,se.IdSeccion          
 ORDER BY SD.Nombre          
  ,UN.Nombre          
  ,UA.Nombre          
  ,Pe.Codigo          
  ,PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,PG.GrupoCodigo          
  ,CU.CursoNombre          
END          
          
IF @Opcion = 25          
BEGIN          
 SELECT *          
 FROM TeamsEquipos WITH (NOLOCK)          
 WHERE EstadoTeam = 'A'          
  AND IsActive = 'I'
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = TeamsEquipos.IdSeccionSmart
   )          
END          
          
IF @Opcion = 26          
BEGIN          
 --ACTUALIZAMOS LOS EQUIPOS ACTIVADOS            
 UPDATE TeamsEquipos          
 SET IsActive = @IsActive          
 WHERE IdTeamsGroup = @idTeamsGroup;          
END          
          
IF @Opcion = 27          
BEGIN          
 --ACTUALIZA EL FACILITADOR            
 UPDATE TeamsEquipos          
 SET Propietario3 = @propietario3          
  ,FechaModificacion = GETDATE()          
  ,UsuarioModificacion = 1          
 WHERE IdTeamsGroup = @idTeamsGroup          
END          
          
IF @Opcion = 28          
BEGIN          
 --DESACTIVAMOS AL ANTIGUO PROPIETARIO            
 UPDATE TeamsUsuarios          
 SET Estado = 'I'          
  ,FechaModificacion = GETDATE()          
  ,UsuarioModificacion = 1          
 WHERE Email = @propietario1          
  AND idTeams = @idTeamsGroup          
  AND Tipo = 'F';          
          
 --SI YA EXISTE EL REGISTRO LO ACTUALIZAMOS            
 --SI NO LO REGISTRAMOS            
 DECLARE @exist INT = (          
   SELECT COUNT(1)          
   FROM TeamsUsuarios          
   WHERE Email = @Email          
    AND idTeams = @idTeamsGroup          
    AND Tipo = 'F'          
   );          
          
 IF ISNULL(@Email, '') <> ''          
 BEGIN          
  IF @exist > 0          
  BEGIN          
   UPDATE TeamsUsuarios          
   SET Email = @Email          
    ,Nombres = @Nombres          
    ,Apellidos = @Apellidos          
    ,Estado = 'A'          
    ,FechaModificacion = GETDATE()          
    ,UsuarioModificacion = 1          
   WHERE Email = @Email          
    AND idTeams = @idTeamsGroup          
    AND Tipo = 'F';          
  END          
  ELSE          
  BEGIN          
   --INSERTA FACILITADOR            
   INSERT INTO TeamsUsuarios (          
    idTeams          
    ,CodigoAlumno          
    ,Nombres          
    ,Apellidos          
    ,Email          
    ,Tipo          
    ,Estado          
    ,UsuarioCreacion          
    ,FechaCreacion          
    )          
   VALUES (          
    @idTeamsGroup          
    ,@CodigoAlumno          
    ,@Nombres          
    ,@Apellidos          
    ,@Email          
    ,'F'          
    ,'A'          
    ,1          
    ,GETDATE()          
    );          
  END          
 END          
END          
          
IF @Opcion = 29          
BEGIN          
 --ELIMINA LOS EQUIPOS            
 UPDATE TeamsEquipos          
 SET EstadoTeam = 'I'          
  ,UsuarioModificacion = 1          
  ,FechaModificacion = GETDATE()          
 WHERE IdTeamsGroup = @idTeamsGroup;          
          
 UPDATE TeamsUsuarios          
 SET Estado = 'I'          
  ,UsuarioModificacion = 1          
  ,FechaModificacion = GETDATE()          
 WHERE IdTeams = @idTeamsGroup;          
          
 UPDATE TeamsHorarios          
 SET Estado = 'I'          
  ,UsuarioModificacion = 1          
  ,FechaModificacion = GETDATE()          
 WHERE IdTeams = @idTeamsGroup;          
END          
          
IF @Opcion = 30          
BEGIN          
 WITH dtOldMembers          
 AS (          
  SELECT TE.IdTeamsGroup          
   ,TU.CodigoAlumno          
  FROM TeamsUsuarios TU WITH (NOLOCK)          
  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)          
  WHERE TU.CodigoAlumno NOT IN (          
    SELECT MPG.CodigoAlumno          
    FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
    WHERE MPG.IdCurso = TE.IdSeccionSmart          
    )          
   AND TE.IdSeccionSmart = @IdSeccion          
   AND TU.Estado = 'A'          
   AND TU.Tipo = 'A'          
   AND TE.EstadoTeam = 'A'          
  )          
 SELECT *          
 FROM dtOldMembers          
 WHERE ISNULL(IdTeamsGroup, '') <> '';          
END          
          
IF @Opcion = 31          
BEGIN          
 --CREA LOS NUEVOS GRUPOS            
 INSERT INTO TeamsEquipos (          
  IdTeamsGroup          
  ,Propietario1          
  ,Propietario2          
  ,Propietario3          
  ,Propietario4          
  ,NombreTeam          
  ,DescripcionTeam          
  ,MailNickName          
  ,EstadoTeam          
  ,IdSeccionSmart          
  ,IsActive          
  ,UsuarioCreacion          
  ,FechaCreacion          
  )          
 VALUES (          
  @idTeamsGroup          
  ,@propietario1          
  ,@propietario2          
  ,@propietario3          
  ,@propietario4          
  ,@nombreTeam          
  ,@descripcionTeam          
  ,@mailNickName          
  ,'A'          
  ,@idSeccionSmart          
  ,'I'          
  ,1          
  ,GETDATE()          
  );          
END          
          
IF @Opcion = 32          
BEGIN          
 SELECT DISTINCT 'Sede' = SD.Nombre          
  ,'Division' = UN.Nombre          
  ,'Programa' = UA.Nombre          
  ,'Periodo' = Pe.Codigo          
  ,'Producto' = PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,'Seccion' = PG.GrupoCodigo          
  ,'CursoCodigo' = SE.Codigo          
  ,'CursoNombre' = cu.CursoNombre          
  ,'EstadoCursoHorario' = se.Estado          
  ,'InicioPeriodo' = CONVERT(VARCHAR, pg.FechaInicio, 104)          
  ,'FinPeriodo' = CONVERT(VARCHAR, pg.FechaFin, 104)          
  ,'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, '')          
  ,'FacilitadorNombre' = ISNULL(AR.NombreCompleto, '')          
  ,'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion)          
  ,te.NombreTeam          
  ,te.Propietario2          
  ,a.CodigoAnterior          
  ,'NombresAlumno' = act.Nombres          
  ,'ApellidosAlumno' = act.Paterno + ' ' + act.Materno          
  ,tu.CodigoAlumno          
  ,ac.FechaModificacion          
  ,u.Nombres          
  ,SE.IdSeccion          
  ,te.IdTeamsGroup          
  ,SE.FechaInicio          
  ,'Existe' = isnull(tu.idUsuario, 0)          
 FROM AlumnoCurso AC WITH (NOLOCK)          
 LEFT JOIN Matricula M ON AC.IdMatricula = M.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 LEFT JOIN usuario U ON ac.UsuarioModificacion = U.IdUsuario          
 INNER JOIN Alumno a WITH (NOLOCK) ON a.IdAlumno = ac.IdAlumno          
 INNER JOIN actor act WITH (NOLOCK) ON a.IdAlumno = act.IdActor          
 INNER JOIN Seccion SE WITH (NOLOCK) ON AC.IdSeccion = SE.IdSeccion          
 INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
 INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
 INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
  AND SP.EsResponsable = 1          
 LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
 LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON te.IdSeccionSmart = se.IdSeccion          
  AND TE.EstadoTeam = 'A'          
 LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams          
  AND a.CodigoAnterior = tu.CodigoAlumno          
  AND tu.Tipo = 'A'          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
   )          
  AND aC.EsMatricula = 1          
  AND m.EsMatricula = 1          
  AND ISNULL(@IdSeccion, SE.IdSeccion) = SE.IdSeccion          
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )          
  --and ISNULL(tu.CodigoAlumno,'') = ''             
END          
          
IF @Opcion = 33          
BEGIN          
 SELECT *          
 FROM TeamsEquipos WITH (NOLOCK)          
 WHERE EstadoTeam = 'A'          
  AND IsActive = 'I'
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = TeamsEquipos.IdSeccionSmart
   )
  --FIN @21          
 ORDER BY FechaCreacion DESC          
END          
        
IF @Opcion = 34          
BEGIN          
 SELECT TE.*          
 FROM TeamsEquipos TE WITH (NOLOCK)          
 INNER JOIN Seccion SE WITH (NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Empresa EM WITH (NOLOCK) ON PR.IdEmpresa = EM.IdEmpresa          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 WHERE EstadoTeam = 'A'          
  AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = TE.IdSeccionSmart
   )          
     -- INICIO @11        
        
   UNION        
 select TE.* from TeamsEquipos TE WITH (NOLOCK)          
 LEFT JOIN Seccion S WITH (NOLOCK)   on S.IdSeccion = TE.IdSeccionSmart        
 Where          
 TE.EstadoTeam = 'A'        
 AND S.IdSeccion is null        
 AND NOT EXISTS (
  SELECT 1
  FROM @SeccionesOmitidas SO
  WHERE SO.IdSeccion = TE.IdSeccionSmart
  )
  --FIN @21        
   -- FIN @11        
        
 ORDER BY FechaCreacion DESC          
END          
          
IF @Opcion = 35          
BEGIN          
 SELECT DISTINCT 'IdCurso' = SE.IdSeccion          
  ,'Sede' = SD.Nombre          
  ,'Division' = UN.Nombre          
  ,'Programa' = UA.Nombre          
  ,'Periodo' = Pe.Codigo          
  ,'Producto' = PO.ProductoNombre          
  ,PR.PromocionCodigo          
  ,'Seccion' = PG.GrupoCodigo          
  ,'CursoCodigo' = SE.Codigo          
  ,'CursoNombre' = cu.CursoNombre          
  ,'EstadoCursoHorario' = se.Estado          
  ,'InicioPeriodo' = CONVERT(VARCHAR, pg.FechaInicio, 104)          
  ,'FinPeriodo' = CONVERT(VARCHAR, pg.FechaFin, 104)          
  ,'FacilitadorCodigo' = ISNULL(FA.CodigoAnterior, '-SIN FACILITADOR-')          
  ,'FacilitadorNombre' = ISNULL(AR.NombreCompleto, '-SIN FACILITADOR-')          
  ,'Frecuencia' = dbo.gFrecuenciaSeccionHorario(SE.IdSeccion)          
  ,te.NombreTeam          
  ,te.Propietario2          
  ,a.CodigoAnterior          
  ,act.NombreCompleto          
  ,ISNULL(tu.CodigoAlumno, 'No Sincronizado') AS CodigoTeams          
  ,ac.FechaModificacion          
  ,u.Nombres          
  ,SE.IdSeccion          
  ,te.IdTeamsGroup          
  ,SE.FechaInicio          
 --ac.*            
 FROM AlumnoCurso AC WITH (NOLOCK)          
 LEFT JOIN Matricula M ON AC.IdMatricula = M.IdMatricula          
  AND M.EsMatricula = 1 --@2            
 LEFT JOIN usuario U ON ac.UsuarioModificacion = U.IdUsuario          
 INNER JOIN Alumno a WITH (NOLOCK) ON a.IdAlumno = ac.IdAlumno          
 INNER JOIN actor act WITH (NOLOCK) ON a.IdAlumno = act.IdActor          
 INNER JOIN Seccion SE WITH (NOLOCK) ON AC.IdSeccion = SE.IdSeccion          
 INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
 INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
  AND SE.IdGrupo = PG.IdGrupo          
 INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
 INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
 LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
  AND SP.EsResponsable = 1          
 LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
 LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
 LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON te.IdSeccionSmart = se.IdSeccion          
 LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams          
  AND a.CodigoAnterior = tu.CodigoAlumno          
  AND tu.Tipo = 'A'          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
  )          
  AND a.CodigoAnterior = @CodigoAlumno          
  --INI @21
  AND NOT EXISTS (
   SELECT 1
   FROM @SeccionesOmitidas SO
   WHERE SO.IdSeccion = SE.IdSeccion
   )
  --FIN @21          
  AND aC.EsMatricula = 1          
  AND TE.EstadoTeam='A' --@8          
END          
          
IF @Opcion = 36          
          
BEGIN          
 DECLARE @tmpReporte AS TABLE (          
  id INT identity          
  ,IdPeriodo INT          
  ,Sede varchar(10)          
  ,Periodos VARCHAR(10)          
  ,UnidadNegocio VARCHAR(100)          
  ,Programa VARCHAR(100)          
  ,Equipos INT          
  ,EquiposActivos INT          
  ,PorEquiposActivos DECIMAL(5, 2)          
  ,Docentes INT          
  ,NoDocentes INT          
  ,PorDocente DECIMAL(5, 2)          
  ,CursoxAlumnos INT          
  ,CursoxTeams INT          
  ,PorCursoxAlumnos DECIMAL(5, 2)          
  ,Alumnos INT          
  ,EnTeams INT          
  ,PorAlumnos DECIMAL(5, 2)          
  )          
          
 INSERT INTO @tmpReporte          
 SELECT DISTINCT 'IdPeriodo' = PE.IdPeriodo          
  ,'Sede' = SD.Codigo          
  ,'Periodo' = Pe.Codigo          
  ,'UnidadNegocio' = UN.Nombre          
  ,'Programa' = UA.Nombre          
  ,'Equipos' = count(PO.IdProducto)          
  ,0          
  ,0.0          
  ,0          
  ,0          
  ,0          
  ,0          
  ,0          
  ,0          
  ,0          
  ,0          
  ,0          
 FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
 INNER JOIN Sede SD WITH (NOLOCK) ON PR.IdSede = SD.IdSede          
 INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
 INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
 INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
 INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
 WHERE PE.EsTeams = 1          
  AND (          
   (          
    (          
     PR.TipoServicio = 'P'          
     OR PR.TipoServicio = 'L'          
     )          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
    )          
   OR (          
    PR.TipoServicio = 'C'          
    AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
     AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
    )          
  )          
   AND SD.IdSede IN (          
   SELECT IdSede          
   FROM @SedeTMP          
   )          
   --INI @21
   AND NOT EXISTS (
    SELECT 1
    FROM @SeccionesOmitidas SO
    WHERE SO.IdSeccion = SE.IdSeccion
    )
   --FIN @21          
   --and Pr.IdPeriodo = 4609          
 GROUP BY pe.IdPeriodo          
  ,PE.Codigo          
  ,UA.Nombre          
  ,UN.Nombre          
  ,SD.Codigo          
 ORDER BY 1          
          
 DECLARE @CurID INT          
  ,@MaxID INT          
  ,@IdPeriodoActual INT          
  ,@EquiposActivos INT          
  ,@Docentes INT          
  ,@NoDocentes INT          
  ,@CursoxAlumnos INT          
  ,@CursoxEnTeams INT          
  ,@Alumnos INT          
  ,@Enteams INT          
          
 SELECT @CurID = MIN(Id)          
  ,@MaxID = MAX(Id)          
 FROM @tmpReporte          
          
 WHILE @MaxID >= @CurID          
 BEGIN          
  SELECT @IdPeriodoActual = IdPeriodo          
  FROM @tmpReporte          
  WHERE Id = @CurID          
          
  SET @EquiposActivos = 0          
  SET @Docentes = 0          
  SET @NoDocentes = 0          
  SET @Alumnos = 0          
  SET @EnTeams = 0          
          
  SELECT @EquiposActivos = sum(CASE           
     WHEN TE.IsActive = 'A'          
      THEN 1          
     ELSE 0          
     END) * 1.0          
   ,@Docentes = sum(CASE           
     WHEN TE.Propietario3 IS NULL          
      THEN 0          
     ELSE 1          
     END) * 1.0          
   ,@NoDocentes = sum(CASE           
     WHEN TE.Propietario3 IS NULL          
      THEN 1          
     ELSE 0          
     END) * 1.0          
  FROM TeamsEquipos TE WITH (NOLOCK)          
  INNER JOIN Seccion SE WITH (NOLOCK) ON TE.IdSeccionSmart = SE.IdSeccion          
  INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
  INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
  WHERE PE.EsTeams = 1          
   AND (          
    (          
     (          
      PR.TipoServicio = 'P'          
      OR PR.TipoServicio = 'L'          
      )          
     AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
      AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
     )          
    OR (          
     PR.TipoServicio = 'C'          
     AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
      AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
     )          
    )          
   AND TE.EstadoTeam = 'A'          
   AND PE.IdPeriodo = @IdPeriodoActual          
   --INI @21
   AND NOT EXISTS (
    SELECT 1
    FROM @SeccionesOmitidas SO
    WHERE SO.IdSeccion = TE.IdSeccionSmart
    )
   --FIN @21          
            
  SELECT @CursoxEnTeams = sum(CASE           
     WHEN TU.CodigoAlumno IS NULL          
      THEN 0       
     ELSE 1          
     END)          
   ,@CursoxAlumnos = sum(CASE           
     WHEN A.CodigoAnterior IS NULL          
      THEN 0          
     ELSE 1          
     END)          
  FROM Alumno a WITH (NOLOCK)          
  INNER JOIN actor act WITH (NOLOCK) ON a.IdAlumno = act.IdActor          
  INNER JOIN AlumnoCurso AC WITH (NOLOCK) ON a.IdAlumno = ac.IdAlumno          
  INNER JOIN Seccion SE WITH (NOLOCK) ON AC.IdSeccion = SE.IdSeccion          
  INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
  INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
  INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
   AND SE.IdGrupo = PG.IdGrupo          
  INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
  INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede          
  INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
  INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
  INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
  LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
   AND SP.EsResponsable = 1          
  LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
  LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON te.IdSeccionSmart = se.IdSeccion          
   AND TE.EstadoTeam = 'A'          
  LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams          
   AND a.CodigoAnterior = tu.CodigoAlumno          
   AND tu.Tipo = 'A'          
  WHERE PE.EsTeams = 1          
   AND (          
    (          
     (          
      PR.TipoServicio = 'P'          
      OR PR.TipoServicio = 'L'          
      )          
     AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
      AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
     )          
    OR (          
     PR.TipoServicio = 'C'          
     AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
      AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
     )          
    )          
   AND PE.IdPeriodo = @IdPeriodoActual          
   AND aC.EsMatricula = 1          
   --INI @21
   AND NOT EXISTS (
    SELECT 1
    FROM @SeccionesOmitidas SO
    WHERE SO.IdSeccion = SE.IdSeccion
    )
   --FIN @21          
            
  SELECT @Enteams = COUNT(enteams)          
   ,@Alumnos = count(total)          
  FROM (          
   SELECT DISTINCT TU.CodigoAlumno enTeams          
    ,A.CodigoAnterior total          
   FROM Alumno a WITH (NOLOCK)          
   INNER JOIN actor act WITH (NOLOCK) ON a.IdAlumno = act.IdActor          
   INNER JOIN AlumnoCurso AC WITH (NOLOCK) ON a.IdAlumno = ac.IdAlumno          
   INNER JOIN Seccion SE WITH (NOLOCK) ON AC.IdSeccion = SE.IdSeccion          
   INNER JOIN curso cu WITH (NOLOCK) ON SE.idCurso = cu.idCurso          
   INNER JOIN Promocion Pr WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion          
   INNER JOIN PromocionGrupo PG WITH (NOLOCK) ON SE.IdPromocion = PG.IdPromocion          
    AND SE.IdGrupo = PG.IdGrupo          
   INNER JOIN periodo pe WITH (NOLOCK) ON PR.idPeriodo = pe.idperiodo          
   INNER JOIN sede SD WITH (NOLOCK) ON PR.idSede = SD.idSede          
   INNER JOIN UnidadNegocio UN WITH (NOLOCK) ON PR.IdUnidadNegocio = UN.IdUnidadNegocio          
   INNER JOIN UnidadAcademica UA WITH (NOLOCK) ON PR.IdUnidadAcademica = UA.IdUnidadAcademica          
   INNER JOIN Producto PO WITH (NOLOCK) ON PR.IdProducto = PO.IdProducto          
   LEFT JOIN SeccionProfesor SP WITH (NOLOCK) ON SE.IdSeccion = SP.IdSeccion          
    AND SP.EsResponsable = 1          
   LEFT JOIN Facilitador FA WITH (NOLOCK) ON SP.IdActor = FA.IdFacilitador          
   LEFT JOIN Actor AR WITH (NOLOCK) ON FA.IdFacilitador = AR.IdActor          
   LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON te.IdSeccionSmart = se.IdSeccion          
    AND TE.EstadoTeam = 'A'          
   LEFT JOIN TeamsUsuarios TU WITH (NOLOCK) ON te.IdTeamsGroup = TU.IdTeams          
    AND a.CodigoAnterior = tu.CodigoAlumno          
    AND tu.Tipo = 'A'          
   WHERE PE.EsTeams = 1          
    AND (          
     (          
      (          
       PR.TipoServicio = 'P'          
       OR PR.TipoServicio = 'L'          
       )          
      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, SE.FechaInicio), 112)          
       AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, se.FechaFin), 112)          
      )          
     OR (          
      PR.TipoServicio = 'C'          
      AND CONVERT(VARCHAR, GETDATE(), 112) BETWEEN CONVERT(VARCHAR, DATEADD(DAY, @FechaIniDias * - 1, PE.Inicio), 112)          
       AND CONVERT(VARCHAR, DateADD(DAY, @FechaFinDias, PE.Fin), 112)          
      )          
     )          
    AND PE.IdPeriodo = @IdPeriodoActual          
    AND aC.EsMatricula = 1          
    --INI @21
    AND NOT EXISTS (
     SELECT 1
     FROM @SeccionesOmitidas SO
     WHERE SO.IdSeccion = SE.IdSeccion
     )
    --FIN @21          
   ) AS tempo          
          
  UPDATE @tmpReporte          
  SET EquiposActivos = @EquiposActivos          
   ,Docentes =   @Docentes          
   ,NoDocentes =  @NoDocentes          
   ,CursoxAlumnos = isnull(@CursoxAlumnos,1) --@5          
   ,CursoxTeams =  isnull(@CursoxEnTeams,1) --@5          
   ,Alumnos =   @Alumnos          
   ,EnTeams =   @EnTeams          
  WHERE id = @CurID          
            
  UPDATE @tmpReporte          
  SET PorEquiposActivos = (CAST(EquiposActivos AS REAL) / CAST(Equipos AS REAL)) * 100          
   ,PorDocente = (CAST(Docentes AS REAL) / CAST(Equipos AS REAL)) * 100          
   ,PorAlumnos = (CAST(EnTeams AS REAL) / CAST(NULLIF(Alumnos,0) AS REAL)) * 100 --@5          
   ,PorCursoxAlumnos = (CAST(CursoxTeams AS REAL) / CAST(NULLIF(CursoxAlumnos,0) AS REAL)) * 100--@5          
  WHERE id = @CurID          
          
  UPDATE @tmpReporte          
  SET PorAlumnos = isnull(PorAlumnos,0) --@5          
             
  WHERE id = @CurID          
          
  SET @CurID = @CurID + 1          
 END          
          
 SELECT *          
 FROM @tmpReporte          
END          
          
          
IF @Opcion = 37          
BEGIN          
 DECLARE @TEMPHORARIO TABLE (          
  IdTeams NVARCHAR(200)          
  ,IdCurso INT          
  ,IdEvento NVARCHAR(200)          
  ,NumeroReunion INT          
  ,IdHorario INT          
  ,Codigo NVARCHAR(50)          
  ,Fecha DATETIME          
  ,Inicio INT          
  ,Fin INT          
  ,CodigoAlumno NVARCHAR(20)          
  ,CorreoAlumno NVARCHAR(100)          
  ,CodigoFacilitador NVARCHAR(100)          
  ,CorreoFacilitador NVARCHAR(100)          
  ,JoinUrl NVARCHAR(400)          
  );          
          
 WITH dtNewHorario          
 AS (          
  SELECT TE.IdTeamsGroup          
   ,TE.IdSeccionSmart          
   ,'IdEvento' = @IdEvento          
   ,'NumeroReunion' = @NumeroReunion          
   ,'IdHorario' = @IdHorario          
   ,'Codigo' = @CodigoSesion          
   ,'Fecha' = @Fecha          
   ,'Inicio' = @Inicio          
   ,'Fin' = @Fin          
   ,'CodigoAlumno' = TU.CodigoAlumno          
   ,'CorreoAlumno' = TU.Email          
   ,'CodigoFacilitador' = @CodigoFacilitador          
   ,'CorreoFacilitador' = @propietario1          
   ,'JoinUrl' = @JoinUrl          
  FROM TeamsUsuarios TU WITH (NOLOCK)          
  LEFT JOIN TeamsEquipos TE WITH (NOLOCK) ON (TE.IdTeamsGroup = TU.idTeams)          
  WHERE TE.IdSeccionSmart = @idSeccionSmart          
   AND TU.Estado = 'A'          
   AND TE.IdTeamsGroup = @idTeamsGroup          
   AND TU.Tipo = 'A'          
  )          
 INSERT INTO @TEMPHORARIO          
 SELECT *          
 FROM dtNewHorario;          
          
 MERGE TeamsHorarios AS TARGET          
 USING @TEMPHORARIO AS SOURCE          
  ON (          
    TARGET.idTeams = SOURCE.IdTeams          
    AND TARGET.idEvento = SOURCE.IdEvento          
    )          
 WHEN MATCHED          
  AND TARGET.IdCurso = SOURCE.IdCurso          
  AND TARGET.IdHorario = SOURCE.IdHorario          
  AND TARGET.Codigo = SOURCE.Codigo          
  AND TARGET.NumeroReunion = SOURCE.NumeroReunion          
  THEN          
   UPDATE          
   SET TARGET.Estado = 'A'          
    ,TARGET.CodigoFacilitador = SOURCE.CodigoFacilitador          
    ,TARGET.CorreoFacilitador = SOURCE.CorreoFacilitador          
    ,TARGET.Fecha = SOURCE.Fecha          
    ,TARGET.Inicio = SOURCE.Inicio          
    ,TARGET.Fin = SOURCE.Fin          
    ,TARGET.JoinUrl = SOURCE.JoinUrl          
    ,TARGET.UsuarioModificacion = 1          
    ,TARGET.FechaModificacion = GETDATE()          
 WHEN NOT MATCHED          
  THEN          
   INSERT (          
    IdTeams          
    ,IdCurso          
    ,IdEvento          
    ,NumeroReunion          
    ,IdHorario          
    ,Codigo          
    ,Fecha          
    ,Inicio          
    ,Fin          
    ,CodigoAlumno          
    ,CorreoAlumno          
    ,CodigoFacilitador          
    ,CorreoFacilitador          
    ,JoinUrl          
    ,Estado          
    ,UsuarioCreacion          
    ,FechaCreacion          
    )          
   VALUES (          
    SOURCE.IdTeams          
    ,SOURCE.IdCurso          
    ,SOURCE.IdEvento          
    ,SOURCE.NumeroReunion          
    ,SOURCE.IdHorario          
    ,SOURCE.Codigo          
    ,SOURCE.Fecha          
    ,SOURCE.Inicio          
    ,SOURCE.Fin          
    ,SOURCE.CodigoAlumno          
    ,SOURCE.CorreoAlumno          
    ,SOURCE.CodigoFacilitador          
    ,SOURCE.CorreoFacilitador          
    ,SOURCE.JoinUrl          
    ,'A'          
    ,1          
    ,GETDATE()          
    );          
END          
  IF @Opcion = 38          
BEGIN          
 --TRAE LOS GRUPOS DONDE EL FACILITADOR A SIDO CAMBIADO             
 WITH dtFacilitadores          
 AS (          
  SELECT DISTINCT TE.IdTeamsGroup AS IdTeam          
   ,MPG.EmailFacilitador          
   ,MPG.CodigoFacilitador          
   ,MPG.NombresFacilitador          
   ,MPG.ApellidosFacilitador          
   ,ES.Valor          
  FROM TeamsProgramacionAlumnos MPG WITH (NOLOCK)          
  LEFT JOIN TeamsEquipos TE  WITH (NOLOCK) ON (TE.IdSeccionSmart = MPG.IdCurso)          
  LEFT JOIN EmpresaSedeParametro ES WITH (NOLOCK) ON (          
    ES.IdSede = MPG.IdSede          
    AND ES.Nombre = 'PROPIETARIOTINA'          
    AND MPG.IdUnidadNegocio = convert(INT, ES.Valor3)          
    )          
  WHERE TE.IdSeccionSmart = @IdSeccion          
   AND TE.EstadoTeam = 'A'          
  )          
 SELECT F.IdTeam          
  ,SUBSTRING(F.Valor, 1, CHARINDEX('@', F.Valor) - 1) AS CodigoFacilitador          
  ,F.NombresFacilitador          
  ,F.ApellidosFacilitador          
  ,CASE tu.Propietario3          
   WHEN ''          
    THEN TU.Propietario3          
   ELSE SUBSTRING(TU.Propietario3, 1, CHARINDEX('@', TU.Propietario3) - 1)          
   END AS OldCodigoFacilitador          
 FROM dtFacilitadores F          
 LEFT JOIN TeamsEquipos TU WITH (NOLOCK) ON (TU.IdTeamsGroup = F.IdTeam)          
 WHERE TU.EstadoTeam = 'A'          
END          
          
IF @Opcion = 39          
BEGIN          
 --ACTUALIZA EL FACILITADOR            
 UPDATE TeamsEquipos          
 SET Propietario1 = @propietario3          
  ,FechaModificacion = GETDATE()          
  ,UsuarioModificacion = 1          
 WHERE IdTeamsGroup = @idTeamsGroup          
END          
          
IF @Opcion = 40          
BEGIN          
 DELETE TeamsUsuarios          
 WHERE IdTeams = @idTeamsGroup          
          
 DELETE TeamsEquipos          
 WHERE IdTeamsGroup = @idTeamsGroup          
END          
--@7          
IF @Opcion = 41          
BEGIN          
 select @IdSeccion = IdSeccion from Seccion WITH (NOLOCK) where Codigo= @CodigoSeccion          
 --INI @21
 IF EXISTS (
  SELECT 1
  FROM @SeccionesOmitidas SO
  WHERE SO.IdSeccion = @IdSeccion
  )
 BEGIN
  RETURN;
 END
 --FIN @21
 select @IdTeamsGroup = IdTeamsGroup from TeamsEquipos WITH (NOLOCK) where IdSeccionSmart=@IdSeccion          
 delete from TeamsUsuarios where idTeams=@IdTeamsGroup          
 delete from TeamsEquipos where IdTeamsGroup=@IdTeamsGroup          
END    
    
IF @Opcion = 42    
BEGIN    
 Insert into TeamsLog(Tipo, Valor, Referencia)    
 values(@TipoError,@ErrorLog, @Referencia);    
END    
    
IF @Opcion = 43    
BEGIN    
 select * from TeamsLog where Estado = 'Generado';    
END    
IF @Opcion = 44    
BEGIN    
 update TeamsLog set Estado = 'Enviado', UsuarioModificacion = 1, FechaModificacion = GETDATE() where Estado = 'Generado';    
END  
--ini @16  
IF @Opcion = 45  
begin  
 select top 1 * from SeccionHorario where IdSeccion = @IdSeccion;   
end  
--fin @16  
  
-- ini @17  
IF @Opcion = 46  
begin  
 INSERT INTO TeamsHorarios (IdTeams , IdEvento , IdHorario , IdCurso , NumeroReunion , Codigo , Fecha , Inicio , Fin , CodigoAlumno , CorreoAlumno , CodigoFacilitador , CorreoFacilitador , Estado , JoinUrl , UsuarioCreacion , FechaCreacion)  
 VALUES (@IdTeamsGroup ,@IdEvento ,@IdHorario ,@IdSeccion ,1 ,@CodigoSesion ,@Fecha ,@Inicio ,@Fin ,@CodigoAlumno ,@Email ,@CodigoFacilitador ,@propietario4 ,'A' ,@JoinUrl ,1 ,GETDATE())   
end  
-- fin @17  
--ini @18  
IF @Opcion=47  
begin  
select distinct SH.IdEvento, TE.IdTeamsGroup,  
TE.Propietario3,  
TE.NombreTeam,  
TU.Email,  
SH.UrlClaseVirtual  
from SeccionHorario SH WITH (NOLOCK)  
inner join TeamsEquipos TE WITH (NOLOCK) on TE.IdSeccionSmart = SH.IdSeccion  
inner join TeamsUsuarios TU WITH (NOLOCK) on TU.idTeams = TE.IdTeamsGroup  
left join TeamsHorarios TH WITH (NOLOCK) on TE.IdTeamsGroup = TH.IdTeams  
where SH.IdSeccion = @IdSeccion  
and TH.IdHorarioTeams is null  
end  
--fin @18  
--@20 Inicio
IF @Opcion = 48    
begin    
 UPDATE SeccionHorario set UrlClaseVirtual = '' ,IdEvento= NULL     
 where IdSeccion = @IdSeccion     
end 
--@20 Fin
