SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @UsuarioSistema INT = 1;
    DECLARE @Hoy DATE = CONVERT(date, GETDATE());

    DECLARE @IdActorNuevo INT;
    DECLARE @CodigoNuevo VARCHAR(50);
    DECLARE @EmailNuevo VARCHAR(200);
    DECLARE @NombresNuevo VARCHAR(200);
    DECLARE @PaternoNuevo VARCHAR(200);
    DECLARE @MaternoNuevo VARCHAR(200);

    DECLARE @rHorarioSesion INT = 0,
            @rSeccionProfesor INT = 0,
            @rProgGeneral INT = 0,
            @rProgAlumnos INT = 0,
            @rTeamsEquipos INT = 0,
            @rTeamsHorarios INT = 0;

    DECLARE @Secciones TABLE (IdSeccion INT PRIMARY KEY);
    INSERT INTO @Secciones (IdSeccion) VALUES (415868), (416734);

    -- 1) Resolver docente destino
    SELECT TOP (1)
        @IdActorNuevo = F.IdFacilitador,
        @CodigoNuevo = F.CodigoAnterior,
        @EmailNuevo = F.EmailInstitucion,
        @NombresNuevo = A.Nombres,
        @PaternoNuevo = A.Paterno,
        @MaternoNuevo = A.Materno
    FROM Facilitador F WITH (NOLOCK)
    LEFT JOIN Actor A WITH (NOLOCK) ON A.IdActor = F.IdFacilitador
    WHERE F.CodigoAnterior = 'fpprofesor'
       OR F.EmailInstitucion = 'fpprofesor@zegel.pe'
    ORDER BY CASE WHEN F.CodigoAnterior = 'fpprofesor' THEN 0 ELSE 1 END;

    IF @IdActorNuevo IS NULL
        THROW 51000, 'No se encontró facilitador fpprofesor en Facilitador.', 1;

    -- 2) HorarioSesion (solo sesiones futuras válidas)
    UPDATE HS
    SET HS.IdActorProgramado = @IdActorNuevo,
        HS.UsuarioModificacion = @UsuarioSistema,
        HS.FechaModificacion = GETDATE()
    FROM HorarioSesion HS
    INNER JOIN @Secciones S ON S.IdSeccion = HS.IdSeccion
    WHERE HS.Estado <> 'X'
      AND CONVERT(date, HS.Fecha) >= @Hoy;

    SET @rHorarioSesion = @@ROWCOUNT;

    -- 3) SeccionProfesor responsable (si ya existe fila del nuevo actor, la pone responsable;
    --    si no existe, reemplaza una fila existente de la sección)
    ;WITH SeccionesConNuevo AS
    (
        SELECT DISTINCT SP.IdSeccion
        FROM SeccionProfesor SP
        INNER JOIN @Secciones S ON S.IdSeccion = SP.IdSeccion
        WHERE SP.IdActor = @IdActorNuevo
    )
    UPDATE SP
    SET SP.EsResponsable = CASE WHEN SP.IdActor = @IdActorNuevo THEN 1 ELSE 0 END
    FROM SeccionProfesor SP
    INNER JOIN SeccionesConNuevo X ON X.IdSeccion = SP.IdSeccion;

    SET @rSeccionProfesor += @@ROWCOUNT;

    ;WITH CTE AS
    (
        SELECT
            SP.IdSeccion,
            SP.IdActor,
            ROW_NUMBER() OVER
            (
                PARTITION BY SP.IdSeccion
                ORDER BY CASE WHEN SP.EsResponsable = 1 THEN 0 ELSE 1 END, SP.IdActor
            ) AS rn
        FROM SeccionProfesor SP
        INNER JOIN @Secciones S ON S.IdSeccion = SP.IdSeccion
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM SeccionProfesor X
            WHERE X.IdSeccion = SP.IdSeccion
              AND X.IdActor = @IdActorNuevo
        )
    )
    UPDATE SP
    SET SP.IdActor = @IdActorNuevo,
        SP.EsResponsable = 1
    FROM SeccionProfesor SP
    INNER JOIN CTE
        ON CTE.IdSeccion = SP.IdSeccion
       AND CTE.IdActor = SP.IdActor
       AND CTE.rn = 1;

    SET @rSeccionProfesor += @@ROWCOUNT;

    -- 4) TeamsProgramacionGeneral
    UPDATE TPG
    SET TPG.CodigoFacilitador = @CodigoNuevo,
        TPG.NombresFacilitador = ISNULL(REPLACE(REPLACE(@NombresNuevo, 'Ñ', 'N'), '''', ''), ''),
        TPG.ApellidosFacilitador = ISNULL(REPLACE(REPLACE(@PaternoNuevo, 'Ñ', 'N'), '''', ''), '')
                                 + ' ' +
                                 ISNULL(REPLACE(REPLACE(@MaternoNuevo, 'Ñ', 'N'), '''', ''), ''),
        TPG.EmailFacilitador = @EmailNuevo,
        TPG.UsuarioModificacion = @UsuarioSistema,
        TPG.FechaModificacion = GETDATE()
    FROM TeamsProgramacionGeneral TPG
    INNER JOIN @Secciones S ON S.IdSeccion = TPG.IdCurso;

    SET @rProgGeneral = @@ROWCOUNT;

    -- 5) TeamsProgramacionAlumnos
    UPDATE TPA
    SET TPA.CodigoFacilitador = @CodigoNuevo,
        TPA.NombresFacilitador = ISNULL(REPLACE(REPLACE(@NombresNuevo, 'Ñ', 'N'), '''', ''), ''),
        TPA.ApellidosFacilitador = ISNULL(REPLACE(REPLACE(@PaternoNuevo, 'Ñ', 'N'), '''', ''), '')
                                 + ' ' +
                                 ISNULL(REPLACE(REPLACE(@MaternoNuevo, 'Ñ', 'N'), '''', ''), ''),
        TPA.EmailFacilitador = @EmailNuevo,
        TPA.UsuarioModificacion = @UsuarioSistema,
        TPA.FechaModificacion = GETDATE()
    FROM TeamsProgramacionAlumnos TPA
    INNER JOIN @Secciones S ON S.IdSeccion = TPA.IdCurso;

    SET @rProgAlumnos = @@ROWCOUNT;

    -- 6) TeamsEquipos (owner lógico local)
    UPDATE TE
    SET TE.Propietario1 = @EmailNuevo,
        TE.Propietario3 = @CodigoNuevo,
        TE.Propietario4 = @CodigoNuevo,
        TE.UsuarioModificacion = @UsuarioSistema,
        TE.FechaModificacion = GETDATE()
    FROM TeamsEquipos TE
    INNER JOIN @Secciones S ON S.IdSeccion = TE.IdSeccionSmart
    WHERE TE.EstadoTeam = 'A';

    SET @rTeamsEquipos = @@ROWCOUNT;

    -- 7) TeamsHorarios (solo sesiones futuras activas)
    UPDATE TH
    SET TH.CodigoFacilitador = @CodigoNuevo,
        TH.CorreoFacilitador = @EmailNuevo,
        TH.UsuarioModificacion = @UsuarioSistema,
        TH.FechaModificacion = GETDATE()
    FROM TeamsHorarios TH
    INNER JOIN TeamsEquipos TE ON TE.IdTeamsGroup = TH.IdTeams
    INNER JOIN @Secciones S ON S.IdSeccion = TE.IdSeccionSmart
    WHERE TH.Estado = 'A'
      AND CONVERT(date, TH.Fecha) >= @Hoy;

    SET @rTeamsHorarios = @@ROWCOUNT;

    COMMIT;

    SELECT
        @IdActorNuevo AS IdActorNuevo,
        @CodigoNuevo AS CodigoNuevo,
        @EmailNuevo AS EmailNuevo,
        @rHorarioSesion AS RowsHorarioSesion,
        @rSeccionProfesor AS RowsSeccionProfesor,
        @rProgGeneral AS RowsTeamsProgramacionGeneral,
        @rProgAlumnos AS RowsTeamsProgramacionAlumnos,
        @rTeamsEquipos AS RowsTeamsEquipos,
        @rTeamsHorarios AS RowsTeamsHorarios;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
