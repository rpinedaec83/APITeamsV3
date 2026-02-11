# Migration Map: [dbo].[cTeamsPorSeccion] -> .NET 8 Clean Architecture

| @Opcion | Función Legacy | Nuevo Use Case / Query (CQRS) | Implementación Recomendada | Notas |
| :--- | :--- | :--- | :--- | :--- |
| **0, 1** | Generar Programación (Initial/Reset) | `GenerateSectionScheduleCommand` | EF Core / Hybrid | Logic to populate `TeamsProgramacionGeneral` and `TeamsProgramacionAlumnos`. Use `GenerateProgramacionForSection` raw SQL if massive, or EF Batch. |
| **2** | Update Facilitators (On Change) | `SyncTeamFacilitatorsCommand` | EF Core | Update `TeamsEquipos` owners and `TeamsUsuarios` facilitators. |
| **3** | Get Courses missing Facilitator | `GetCoursesMissingFacilitatorQuery` | EF Core (Read-only) | Filter `TeamsProgramacionGeneral` joined with `TeamsEquipos`. |
| **4, 18** | Get Missing Students in Teams | `GetMissingStudentsQuery` | EF Core (Read-only) | Compare `TeamsProgramacionAlumnos` vs `TeamsUsuarios`. |
| **5, 30** | Get Old Students to Remove | `GetObsoleteStudentsQuery` | EF Core (Read-only) | |
| **6** | Get Expired Teams to Delete | `GetExpiredTeamsQuery` | EF Core (Read-only) | Based on Term end date policies. |
| **7** | Get Teams with Changed Names | `GetRenamedTeamsQuery` | EF Core (Read-only) | Compare Naming Policy vs Actual DB Name. |
| **8, 34** | Get Created Teams | `GetTeamsBySectionQuery` | EF Core (Read-only) | Simple select by `IdSeccionSmart`. |
| **9, 10, 37** | Sync Schedule Members (Upsert) | `SyncSessionRosterCommand` | EF Core / BulkMerge | **Complex**. Sync `TeamsHorarios` based on roster changes. |
| **11** | Update JoinUrl in Section | `UpdateSectionJoinUrlCommand` | EF Core | Updates `SeccionHorario`. |
| **12** | Sync Schedule Dates | `SyncSessionDatesCommand` | EF Core / BulkMerge | Updates `TeamsHorarios` when `HorarioSesion` changes. |
| **13** | Sync Schedule Facilitator | `SyncSessionFacilitatorCommand` | EF Core | Updates `TeamsHorarios` when teacher changes. |
| **16** | Get Next Meeting to Create | `GetNextSessionToProvisionQuery` | EF Core / SQL View | **Critical**. Selects data for Graph OnlineMeeting creation. |
| **19** | Get All Sections from Smart | `GetAllSmartSectionsQuery` | SQL View / Raw SQL | Performance critical. Use View `vw_MatriculasActivas`. |
| **20, 21** | Sync User Info (Upsert/Delete) | `SyncUserCommand` / `RemoveUserCommand` | EF Core | Syncs `TeamsUsuarios`. |
| **22, 23, 24** | Get Section/Course Details | `GetSectionDetailsQuery` | EF Core | For UI/Reporting. |
| **29** | Delete Team (Soft) | `SoftDeleteTeamCommand` | EF Core | Sets status to 'I'. |
| **31** | Create Team Record | `CreateTeamRecordCommand` | EF Core | Inserts into `TeamsEquipos`. |
| **32, 35** | Detailed Student Sync Status | `GetStudentSyncStatusQuery` | EF Core | For detailed UI monitoring. |
| **36** | KPIs / Stats Report | `GetTenancyStatsQuery` | Dapper / Raw SQL | **Rewrite**. Avoid cursor/while loop. Use Window Functions. |

## Strategy
1. **Read-Heavy Operations**: Use EF Core `AsNoTracking()` projecting to DTOs.
2. **Write Operations**: Use EF Core for single updates. Use `ExecuteUpdate`/`ExecuteDelete` for bulk.
3. **Legacy Views**: Rely on existing views or create new optimized views (e.g., `vw_Smart_Secciones`, `vw_Smart_Horarios`).
4. **Idempotency**: All Sync commands must be idempotent.
